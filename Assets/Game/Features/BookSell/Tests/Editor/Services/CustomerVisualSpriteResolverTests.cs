using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class CustomerVisualSpriteResolverTests
    {
        [Test]
        public void Character_UsesPortraitKeyForFigureAndAvatar()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[]
            {
                new CharacterConfig { Id = "eddi", PortraitKey = "eddi_portrait" }
            });
            var resolver = new CustomerVisualSpriteResolver(configs);
            var customer = new Customer("c1", new ICustomerStep[] { new ApproachStep() }, characterId: "eddi");

            Assert.AreEqual("eddi_portrait", resolver.ResolveFigureSpriteKey(customer));
            Assert.AreEqual("eddi_portrait_avatar", resolver.ResolveAvatarSpriteKey(customer));
        }

        [Test]
        public void Character_UsesCharacterIdWhenPortraitKeyMissing()
        {
            var resolver = new CustomerVisualSpriteResolver(new FakeConfigsService());
            var customer = new Customer("c1", new ICustomerStep[] { new ApproachStep() }, characterId: "eddi");

            Assert.AreEqual("eddi", resolver.ResolveFigureSpriteKey(customer));
            Assert.AreEqual("eddi_avatar", resolver.ResolveAvatarSpriteKey(customer));
        }

        [Test]
        public void Npc_UsesCustomerVisualConfigKeys()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[]
            {
                new CustomerVisualConfig
                {
                    Id = "npc_01",
                    FigureSpriteKey = "npc_01_front",
                    AvatarSpriteKey = "npc_01_avatar",
                    Weight = 1f
                }
            });
            var resolver = new CustomerVisualSpriteResolver(configs);
            var customer = new Customer("c1", new ICustomerStep[] { new ApproachStep() }, npcVisualId: "npc_01");

            Assert.AreEqual("npc_01_front", resolver.ResolveFigureSpriteKey(customer));
            Assert.AreEqual("npc_01_avatar", resolver.ResolveAvatarSpriteKey(customer));
        }

        [Test]
        public void MissingNpcConfig_ReturnsNull()
        {
            var resolver = new CustomerVisualSpriteResolver(new FakeConfigsService());
            var customer = new Customer("c1", new ICustomerStep[] { new ApproachStep() }, npcVisualId: "npc_missing");

            Assert.IsNull(resolver.ResolveFigureSpriteKey(customer));
            Assert.IsNull(resolver.ResolveAvatarSpriteKey(customer));
        }
    }
}
