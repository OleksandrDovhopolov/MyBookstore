using System;
using NUnit.Framework;
using Save.Identity;
using UnityEngine;

namespace Save.Tests.Editor
{
    public sealed class AuthPlayerIdTests
    {
        [SetUp]
        [TearDown]
        public void ClearKey()
        {
            PlayerPrefs.DeleteKey(AuthPlayerId.PlayerPrefsKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void PlayerPrefsKey_IsTheAgreedAuthKey()
        {
            // The auth flow and the editor tool must agree on this exact string.
            Assert.That(AuthPlayerId.PlayerPrefsKey, Is.EqualTo("auth.player_id.v1"));
        }

        [Test]
        public void IsValid_GuidWithoutDashes_ReturnsTrue()
        {
            Assert.That(AuthPlayerId.IsValid(Guid.NewGuid().ToString("N")), Is.True);
        }

        [Test]
        public void IsValid_GuidWithDashes_ReturnsFalse()
        {
            // Only the "N" format is accepted, matching what the server issues.
            Assert.That(AuthPlayerId.IsValid(Guid.NewGuid().ToString("D")), Is.False);
        }

        [Test]
        public void IsValid_BracedOrUppercaseHyphenatedForms_ReturnFalse()
        {
            Assert.That(AuthPlayerId.IsValid(Guid.NewGuid().ToString("B")), Is.False);
            Assert.That(AuthPlayerId.IsValid(Guid.NewGuid().ToString("P")), Is.False);
        }

        [Test]
        public void IsValid_ManualTestIds_ReturnFalse()
        {
            // "p1" is fine for manual mode but must never pass as an auto-resolved auth id.
            Assert.That(AuthPlayerId.IsValid("p1"), Is.False);
            Assert.That(AuthPlayerId.IsValid("not-a-guid"), Is.False);
        }

        [Test]
        public void IsValid_NullOrWhitespace_ReturnsFalse()
        {
            Assert.That(AuthPlayerId.IsValid(null), Is.False);
            Assert.That(AuthPlayerId.IsValid(""), Is.False);
            Assert.That(AuthPlayerId.IsValid("   "), Is.False);
        }

        [Test]
        public void TryRead_KeyMissing_ReturnsFalse()
        {
            Assert.That(AuthPlayerId.TryRead(out var playerId), Is.False);
            Assert.That(playerId, Is.Empty);
        }

        [Test]
        public void TryRead_KeyHoldsInvalidValue_ReturnsFalseButExposesRawValue()
        {
            PlayerPrefs.SetString(AuthPlayerId.PlayerPrefsKey, "garbage");
            PlayerPrefs.Save();

            Assert.That(AuthPlayerId.TryRead(out var playerId), Is.False);
            Assert.That(playerId, Is.EqualTo("garbage"));
            Assert.That(AuthPlayerId.ReadRaw(), Is.EqualTo("garbage"));
        }

        [Test]
        public void SeedIfMissing_KeyEmpty_WritesLegacyId()
        {
            var legacy = Guid.NewGuid().ToString("N");

            AuthPlayerId.SeedIfMissing(legacy);

            Assert.That(AuthPlayerId.ReadRaw(), Is.EqualTo(legacy));
        }

        [Test]
        public void SeedIfMissing_KeyAlreadySet_DoesNotOverwrite()
        {
            // The load-bearing guard: once a real auth flow stores a server-issued id, seeding must
            // never replace it with the local install id.
            var serverIssued = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(AuthPlayerId.PlayerPrefsKey, serverIssued);
            PlayerPrefs.Save();

            AuthPlayerId.SeedIfMissing(Guid.NewGuid().ToString("N"));

            Assert.That(AuthPlayerId.ReadRaw(), Is.EqualTo(serverIssued));
        }

        [Test]
        public void SeedIfMissing_KeyHoldsGarbage_DoesNotOverwrite()
        {
            // Garbage is still someone's decision to inspect, not ours to silently replace.
            PlayerPrefs.SetString(AuthPlayerId.PlayerPrefsKey, "garbage");
            PlayerPrefs.Save();

            AuthPlayerId.SeedIfMissing(Guid.NewGuid().ToString("N"));

            Assert.That(AuthPlayerId.ReadRaw(), Is.EqualTo("garbage"));
        }

        [Test]
        public void SeedIfMissing_InvalidLegacyId_WritesNothing()
        {
            AuthPlayerId.SeedIfMissing("p1");
            AuthPlayerId.SeedIfMissing(null);
            AuthPlayerId.SeedIfMissing("   ");

            Assert.That(AuthPlayerId.ReadRaw(), Is.Empty);
        }

        [Test]
        public void SeedIfMissing_IsIdempotent()
        {
            var legacy = Guid.NewGuid().ToString("N");

            AuthPlayerId.SeedIfMissing(legacy);
            AuthPlayerId.SeedIfMissing(legacy);

            Assert.That(AuthPlayerId.ReadRaw(), Is.EqualTo(legacy));
            Assert.That(AuthPlayerId.TryRead(out var read), Is.True);
            Assert.That(read, Is.EqualTo(legacy));
        }

        [Test]
        public void TryRead_KeyHoldsValidGuid_ReturnsTrue()
        {
            var id = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(AuthPlayerId.PlayerPrefsKey, id);
            PlayerPrefs.Save();

            Assert.That(AuthPlayerId.TryRead(out var playerId), Is.True);
            Assert.That(playerId, Is.EqualTo(id));
        }
    }
}
