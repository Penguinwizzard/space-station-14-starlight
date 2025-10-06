using System.Linq;
using System.Numerics;
using Content.Server.Salvage;
using Content.Server.Salvage.Expeditions;
using Content.Server.Shuttles.Components;
using Content.Shared.Procedural;
using Content.Shared.Random.Helpers;
using Content.Shared.Salvage.Expeditions;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Shuttles.Systems;

/// <summary>
/// A system that causes the entity's grid to automatically go on expeditions
/// </summary>
public sealed class AutoExpeditionJumperSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SalvageSystem _salvage = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AutoExpeditionJumperComponent, ComponentInit>(OnComponentInit);
    }

    public override void Update(float frameTime)
    {
        // Generic missions
        var query = EntityQueryEnumerator<AutoExpeditionJumperComponent, TransformComponent>();

        // Run the basic timers (e.g. announcements, auto-FTL)
        // This actually sends us on salvage missions, so the salvage mission machinery sends us back
        while (query.MoveNext(out var uid, out var comp, out var transform))
        {
            var remaining = comp.NextJumpTime - _timing.CurTime;

            // Auto-FTL
            if (remaining < TimeSpan.FromSeconds(_shuttle.DefaultStartupTime) + TimeSpan.FromSeconds(0.5))
            {
                Log.Info("Attempting scheduled expedition run");
                var ftlTime = (float)remaining.TotalSeconds;

                if (remaining < TimeSpan.FromSeconds(_shuttle.DefaultStartupTime))
                {
                    ftlTime = MathF.Max(0, (float)remaining.TotalSeconds - 0.5f);
                }

                var thisGrid = transform.GridUid;
                if (thisGrid == null) {
                    Log.Warning("Jumper wasn't on a grid, skipping");
                    continue;
                }

                ftlTime = MathF.Min(ftlTime, _shuttle.DefaultStartupTime);
                var shuttleQuery = AllEntityQuery<ShuttleComponent, TransformComponent>();

                if (TryComp<ShuttleDestinationCoordinatesComponent>(uid, out var destCoords)) {
                    if (destCoords.Destination.HasValue) {
                        // Set the time remaining on the expedition - it's a property on the destination map,
                        // so we need to look that up from the target entity's transform
                        if (TryComp<SalvageExpeditionComponent>(Transform(destCoords.Destination.Value).MapUid, out var expedition)) {
                            expedition.EndTime = _timing.CurTime + comp.ExpeditionDuration;
                        }
                        // Schedule all shuttles on the grid for FTL
                        HashSet<Entity<ShuttleComponent>> shuttlesToFTL = new();
                        _lookup.GetGridEntities(thisGrid.Value, shuttlesToFTL);
                        foreach (var shuttle in shuttlesToFTL) {
                            if (!HasComp<FTLComponent>(shuttle)) {
                                Log.Info($"Scheduling shuttle {shuttle} for FTL");
                                _shuttle.FTLToCoordinates(shuttle, shuttle.Comp, new EntityCoordinates(destCoords.Destination.Value, Vector2.Zero), Angle.Zero, ftlTime);
                            }
                        }
                    }
                    else
                    {
                        Log.Warning("Dest coords destination had no value");
                    }
                }
                else
                {
                    Log.Warning("Jumper somehow didn't have a destination coordinates component, skipping");
                }

                RollNextExpedition(uid, comp);
                comp.NextJumpTime = _timing.CurTime + comp.ExpeditionDuration + comp.RestDuration;
            }
        }
    }

    private void RollNextExpedition(EntityUid uid, AutoExpeditionJumperComponent comp) {
        Log.Debug("Rolling an expedition for a jumper anchor");
        var difficulties = _prototypeManager.GetInstances<SalvageDifficultyPrototype>();

        var available = difficulties
#if !DEBUG
            .Where(d => d.Value.Delay <= _timing.CurTime)
#endif
            .ToDictionary(x => x.Value.ID, x => x.Value.Probability);

        if (!comp.ReturnTo.HasValue) {
            Log.Warning("Skipped expedition roll because ReturnTo wasn't initialized");
            return;
        }

        _salvage.SpawnMission(new SalvageMissionParams
        {
            Index = 0,
            Seed = _random.Next(),
            Difficulty = _random.Pick(available),
        },
        comp.ReturnTo.Value,
        uid);
    }

    private void OnComponentInit(Entity<AutoExpeditionJumperComponent> jumper, ref ComponentInit args) {
        Log.Debug("Running component init for a jumper");
        if (!jumper.Comp.ReturnTo.HasValue) {
            Log.Warning("Had no ReturnTo value, initializing");
            jumper.Comp.ReturnTo = _entityManager.SpawnAtPosition("StationReturnPoint", new EntityCoordinates(jumper.Owner, default));
        }
        RollNextExpedition(jumper.Owner, jumper);
    }
}
