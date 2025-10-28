using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.IntegrationTests;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Dataset;
using Content.Shared.Store.Components;
using Content.Server.Traitor.Uplink.SurplusBundle;
using NUnit.Framework;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.IntegrationTests.Tests._Starlight.SurplusCrate;

[TestFixture, TestOf(typeof(SurplusCrateCountTests))]
public sealed class SurplusCrateCountTests
{
    [Test]
    public async Task TestExpectedValues()
    {
        {
            var testContext = new NUnitTestContextWrap(TestContext.CurrentContext, TestContext.Out);
            var testOut = testContext.Out;

            await using var pair = await PoolManager.GetServerClient();
            var server = pair.Server;
            var testMap = await pair.CreateTestMap();
            var coordinates = testMap.GridCoords;
            await server.WaitIdleAsync();

            Dictionary<string, int> counts = new();

            var entMan = server.ResolveDependency<IEntityManager>();
            var surplusSystem = entMan.System<SurplusBundleSystem>();
            EntityUid? crate = null;
            await server.WaitAssertion(() => {
                    crate = entMan.SpawnEntity("CrateSyndicateSurplusBundle", coordinates);
            });
            Assert.That(crate, Is.Not.Null);
            if (crate is null) {
                return;
            }
            Entity<SurplusBundleComponent, StoreComponent> compd = (crate.Value, entMan.GetComponent<SurplusBundleComponent>(crate.Value), entMan.GetComponent<StoreComponent>(crate.Value));

            var iterationCount = 10000;

            await server.WaitAssertion(() => {
                for (int i=0;i<iterationCount;i++) {
                    var items = surplusSystem.GetRandomContent(compd);
                    foreach (var item in items) {
                        if (!counts.ContainsKey(item.Name)) {
                            counts[item.Name] = 1;
                        } else {
                            counts[item.Name]++;
                        }
                    }
                }
            });
            foreach (var kvp in counts) {
                testOut.WriteLine("" + (kvp.Value/(double)iterationCount) + ": " + kvp.Key);
            }
        }
        Assert.That(false);
    }
}
