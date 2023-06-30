using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests;

[TestFixture]
public sealed class ResearchTest
{
    [Test]
    public async Task DisciplineValidTierPrerequesitesTest()
    {
        await using var pairTracker = await PoolManager.GetServerClient(new PoolSettings {NoClient = true});
        var server = pairTracker.Pair.Server;

        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var allTechs = protoManager.EnumeratePrototypes<TechnologyPrototype>().ToList();

            foreach (var discipline in protoManager.EnumeratePrototypes<TechDisciplinePrototype>())
            {
                foreach (var tech in allTechs)
                {
                    if (tech.Discipline != discipline.ID)
                        continue;

                    // we ignore these, anyways
                    if (tech.Tier == 1)
                        continue;

                    Assert.That(tech.Tier, Is.GreaterThan(0), $"Technology {tech} has invalid tier {tech.Tier}.");

                    Assert.That(discipline.TierPrerequisites.ContainsKey(tech.Tier),
                        $"Discipline {discipline.ID} does not have a TierPrerequisites definition for tier {tech.Tier}");
                }
            }
        });

        await pairTracker.CleanReturnAsync();
    }

    [Test]
    public async Task AllTechPrintableTest()
    {
        await using var pairTracker = await PoolManager.GetServerClient(new PoolSettings {NoClient = true});
        var server = pairTracker.Pair.Server;

        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var allEnts = protoManager.EnumeratePrototypes<EntityPrototype>();
            var allLathes = new HashSet<LatheComponent>();
            foreach (var proto in allEnts)
            {
                if (proto.Abstract)
                    continue;

                if (!proto.TryGetComponent<LatheComponent>(out var lathe))
                    continue;

                allLathes.Add(lathe);
            }

            var latheTechs = new HashSet<string>();
            foreach (var lathe in allLathes)
            {
                if (lathe.DynamicRecipes == null)
                    continue;

                foreach (var recipe in lathe.DynamicRecipes)
                {
                    latheTechs.Add(recipe);
                }
            }

            foreach (var tech in protoManager.EnumeratePrototypes<TechnologyPrototype>())
            {
                foreach (var recipe in tech.RecipeUnlocks)
                {
                    Assert.That(latheTechs, Does.Contain(recipe), $"Recipe \"{recipe}\" cannot be unlocked on any lathes.");
                }
            }
        });

        await pairTracker.CleanReturnAsync();
    }
}
