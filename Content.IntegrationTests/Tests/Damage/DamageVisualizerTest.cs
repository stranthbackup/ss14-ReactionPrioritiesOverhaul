﻿using System.Linq;
using System.Threading.Tasks;
using Content.Client.GameObjects.Components.Damage;
using Content.Shared.Damage;
using Content.Shared.GameObjects.Components.Damage;
using NUnit.Framework;
using Robust.Server.Interfaces.GameObjects;
using Robust.Server.Interfaces.Player;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Map;
using SpriteComponent = Robust.Client.GameObjects.SpriteComponent;

namespace Content.IntegrationTests.Tests.Damage
{
    [TestFixture]
    [TestOf(typeof(DamageVisualizer))]
    public class DamageVisualizerTest : ContentIntegrationTest
    {
        private static readonly string TestSingleLayerDummyId = "DamageVisualizerSingleLayerTestDummy";

        private static readonly string TestDualLayerDummyId = "DamageVisualizerDualLayerTestDummy";

        private static readonly string Prototypes = @$"
- type: entity
  id: {TestSingleLayerDummyId}
  name: {TestSingleLayerDummyId}
  components:
  - type: Sprite
  - type: Damageable
  - type: Appearance
    visuals:
    - type: DamageVisualizer
      states:
      - damage: 0
        sprite: Test/Damage/metal.rsi
        state: metal0

- type: entity
  id: {TestDualLayerDummyId}
  name: {TestDualLayerDummyId}
  components:
  - type: Sprite
  - type: Damageable
  - type: Appearance
    visuals:
    - type: DamageVisualizer
      states:
      - damage: 0
        sprite: Test/Damage/metal.rsi
        state: metal0
      - damage: 10
        sprite: Test/Damage/wood.rsi
        state: wood0
";

        [Test]
        public async Task DamageVisualizerSingleTest()
        {
            var cOptions = new ClientContentIntegrationOption {ExtraPrototypes = Prototypes};
            var sOptions = new ServerContentIntegrationOption {ExtraPrototypes = Prototypes};
            var (client, server) = await StartConnectedServerClientPair(cOptions, sOptions);

            await server.WaitIdleAsync();

            var sMapManager = server.ResolveDependency<IMapManager>();
            var sEntityManager = server.ResolveDependency<IEntityManager>();
            var sPlayerManager = server.ResolveDependency<IPlayerManager>();

            IEntity sEntity = null;
            ISpriteRenderableComponent sSprite = null;

            await server.WaitPost(() =>
            {
                if (!sMapManager.HasMapEntity(MapId.Nullspace))
                {
                    sMapManager.CreateNewMapEntity(MapId.Nullspace);
                }

                var player = sPlayerManager.GetAllPlayers().Single();
                var coordinates = player.AttachedEntity!.Transform.Coordinates;

                sEntity = sEntityManager.SpawnEntity(TestSingleLayerDummyId, coordinates);
                sSprite = sEntity.GetComponent<ISpriteRenderableComponent>();
            });

            await RunTicksSync(client, server, 10);

            var cEntityManager = client.ResolveDependency<IEntityManager>();

            IEntity cEntity = null;
            SpriteComponent cSprite = null;

            await client.WaitPost(() =>
            {
                cEntity = cEntityManager.GetEntity(sEntity.Uid);
                cSprite = cEntity.GetComponent<SpriteComponent>();
            });

            await client.WaitAssertion(() =>
            {
                Assert.That(cSprite.LayerGetActualRSI(0)!.Path!.ToString(), Is.EqualTo("/Textures/Test/Damage/metal.rsi"));
                Assert.That(cSprite.LayerGetState(0).Name, Is.EqualTo("metal0"));
            });
        }

        [Test]
        public async Task DamageVisualizerTwoStatesTest()
        {
            var cOptions = new ClientContentIntegrationOption {ExtraPrototypes = Prototypes};
            var sOptions = new ServerContentIntegrationOption {ExtraPrototypes = Prototypes};
            var (client, server) = await StartConnectedServerClientPair(cOptions, sOptions);

            await server.WaitIdleAsync();

            var sMapManager = server.ResolveDependency<IMapManager>();
            var sEntityManager = server.ResolveDependency<IEntityManager>();
            var sPlayerManager = server.ResolveDependency<IPlayerManager>();

            IEntity sEntity = null;
            ISpriteRenderableComponent sSprite = null;

            await server.WaitPost(() =>
            {
                if (!sMapManager.HasMapEntity(MapId.Nullspace))
                {
                    sMapManager.CreateNewMapEntity(MapId.Nullspace);
                }

                var player = sPlayerManager.GetAllPlayers().Single();
                var coordinates = player.AttachedEntity!.Transform.Coordinates;

                sEntity = sEntityManager.SpawnEntity(TestSingleLayerDummyId, coordinates);
                sSprite = sEntity.GetComponent<ISpriteRenderableComponent>();
            });

            await RunTicksSync(client, server, 10);

            var cEntityManager = client.ResolveDependency<IEntityManager>();

            IEntity cEntity = null;
            SpriteComponent cSprite = null;

            await client.WaitPost(() =>
            {
                cEntity = cEntityManager.GetEntity(sEntity.Uid);
                cSprite = cEntity.GetComponent<SpriteComponent>();
            });

            await client.WaitAssertion(() =>
            {
                Assert.That(cSprite.LayerGetActualRSI(0)!.Path!.ToString(), Is.EqualTo("/Textures/Test/Damage/metal.rsi"));
                Assert.That(cSprite.LayerGetState(0).Name, Is.EqualTo("metal0"));
            });

            await server.WaitPost(() =>
            {
                sEntity.GetComponent<IDamageableComponent>().ChangeDamage(DamageClass.Brute, 10, true);
            });

            await RunTicksSync(client, server, 10);

            await client.WaitAssertion(() =>
            {
                Assert.That(cSprite.LayerGetActualRSI(0)!.Path!.ToString(),
                    Is.EqualTo("/Textures/Test/Damage/wood.rsi"));
                Assert.That(cSprite.LayerGetState(0).Name, Is.EqualTo("wood0"));
            });
        }
    }
}
