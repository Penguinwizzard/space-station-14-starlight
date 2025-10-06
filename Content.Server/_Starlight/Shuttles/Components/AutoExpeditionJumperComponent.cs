using Content.Server.Shuttles.Systems;

namespace Content.Server.Shuttles.Components;

/// <summary>
/// Causes the grid this is on to make automatic expedition jumps periodically
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(AutoExpeditionJumperSystem))]
public sealed partial class AutoExpeditionJumperComponent : Component
{
    /// <summary>
    /// Duration for which the grid will stay on an expedition
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public TimeSpan ExpeditionDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Duration for which the grid will be on its original map between expeditions
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public TimeSpan RestDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Next jump time - starts as first jump time
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoPausedField]
    public TimeSpan NextJumpTime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Station to return to; auto-initialized on creation.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntityUid? ReturnTo = default;
}
