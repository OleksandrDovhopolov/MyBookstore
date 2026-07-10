using System;
using Infrastructure.ResourceAnimations;
using NUnit.Framework;

namespace Game.Core.UI.Tests.Editor.ResourceAnimations
{
    public sealed class ResourceAnimationServiceTests
    {
        [Test]
        public void ResolveParticleCount_ZeroAmount_ReturnsZero()
        {
            Assert.AreEqual(0, ResourceAnimationRequestRules.ResolveParticleCount(0, 6));
        }

        [Test]
        public void ResolveParticleCount_ClampsToMaxParticles()
        {
            Assert.AreEqual(6, ResourceAnimationRequestRules.ResolveParticleCount(100, 6));
        }

        [Test]
        public void ResolveParticleCount_NegativeAmount_UsesAbsoluteValue()
        {
            Assert.AreEqual(4, ResourceAnimationRequestRules.ResolveParticleCount(-4, 6));
        }

        [Test]
        public void ResolveSpriteId_DefaultsToResourceId()
        {
            var request = new ResourceAnimationRequest(
                "Gold",
                5,
                ResourceAnimationEndpoint.ScreenPoint(default),
                ResourceAnimationEndpoint.ScreenPoint(default));

            Assert.AreEqual("Gold", ResourceAnimationRequestRules.ResolveSpriteId(request));
        }

        [Test]
        public void ResolveSpriteId_UsesExplicitSpriteId()
        {
            var request = new ResourceAnimationRequest(
                "Gold",
                5,
                ResourceAnimationEndpoint.ScreenPoint(default),
                ResourceAnimationEndpoint.ScreenPoint(default),
                spriteId: "CoinIcon");

            Assert.AreEqual("CoinIcon", ResourceAnimationRequestRules.ResolveSpriteId(request));
        }

        [Test]
        public void OnParticleArrived_DefaultsToNull()
        {
            var request = new ResourceAnimationRequest(
                "Gold",
                5,
                ResourceAnimationEndpoint.ScreenPoint(default),
                ResourceAnimationEndpoint.ScreenPoint(default));

            Assert.IsNull(request.OnParticleArrived);
        }

        [Test]
        public void OnParticleArrived_IsStoredOnRequest()
        {
            Action callback = () => { };
            var request = new ResourceAnimationRequest(
                "Gold",
                5,
                ResourceAnimationEndpoint.ScreenPoint(default),
                ResourceAnimationEndpoint.ScreenPoint(default),
                onParticleArrived: callback);

            Assert.AreSame(callback, request.OnParticleArrived);
        }
    }
}
