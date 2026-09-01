using System;
using NUnit.Framework;
using Save.Editor;

namespace Save.Tests.Editor
{
    public sealed class PlayerSaveResetRequestTests
    {
        private const string Base = "https://server.example.com";

        [Test]
        public void BuildUrl_PlainBaseUrl_BuildsAdminResetPath()
        {
            var url = PlayerSaveResetRequest.BuildUrl(Base, "p1");

            Assert.That(url, Is.EqualTo("https://server.example.com/api/admin/test/player/p1/save/reset"));
        }

        [Test]
        public void BuildUrl_TrailingSlashesOnBaseUrl_AreTrimmed()
        {
            var url = PlayerSaveResetRequest.BuildUrl(Base + "///", "p1");

            Assert.That(url, Is.EqualTo("https://server.example.com/api/admin/test/player/p1/save/reset"));
        }

        [Test]
        public void BuildUrl_WhitespaceAroundBaseUrlAndPlayerId_IsTrimmed()
        {
            var url = PlayerSaveResetRequest.BuildUrl("  " + Base + "/  ", "  p1  ");

            Assert.That(url, Is.EqualTo("https://server.example.com/api/admin/test/player/p1/save/reset"));
        }

        [Test]
        public void BuildUrl_PlayerIdWithUnsafeCharacters_IsEscaped()
        {
            var url = PlayerSaveResetRequest.BuildUrl(Base, "a b/c?d");

            Assert.That(url, Does.EndWith("/api/admin/test/player/a%20b%2Fc%3Fd/save/reset"));
        }

        [Test]
        public void BuildUrl_GuidPlayerId_IsPreservedVerbatim()
        {
            var id = Guid.NewGuid().ToString("N");

            var url = PlayerSaveResetRequest.BuildUrl(Base, id);

            Assert.That(url, Does.EndWith($"/api/admin/test/player/{id}/save/reset"));
        }

        [Test]
        public void BuildUrl_EmptyBaseUrl_Throws()
        {
            Assert.That(() => PlayerSaveResetRequest.BuildUrl("  ", "p1"), Throws.ArgumentException);
        }

        [Test]
        public void BuildUrl_EmptyPlayerId_Throws()
        {
            Assert.That(() => PlayerSaveResetRequest.BuildUrl(Base, "  "), Throws.ArgumentException);
        }

        [Test]
        public void BuildPlayerUrl_BuildsReadOnlyAdminPlayerPath()
        {
            var url = PlayerSaveResetRequest.BuildPlayerUrl(Base + "/", "p1");

            Assert.That(url, Is.EqualTo("https://server.example.com/api/admin/player/p1"));
        }

        [Test]
        public void BuildPlayerUrl_EscapesPlayerId()
        {
            var url = PlayerSaveResetRequest.BuildPlayerUrl(Base, "a b/c");

            Assert.That(url, Is.EqualTo("https://server.example.com/api/admin/player/a%20b%2Fc"));
        }

        [Test]
        public void BuildPlayerUrl_EmptyArguments_Throw()
        {
            Assert.That(() => PlayerSaveResetRequest.BuildPlayerUrl("", "p1"), Throws.ArgumentException);
            Assert.That(() => PlayerSaveResetRequest.BuildPlayerUrl(Base, ""), Throws.ArgumentException);
        }

        [Test]
        public void BuildPlayerUrl_IsNotTheResetEndpoint()
        {
            // Test Connection must stay read-only: a copy-paste slip into the reset path would wipe data.
            var probe = PlayerSaveResetRequest.BuildPlayerUrl(Base, "p1");

            Assert.That(probe, Does.Not.Contain("/save/reset"));
            Assert.That(probe, Does.Not.Contain("/test/"));
        }

        [Test]
        public void BuildBody_ContainsExactConfirmToken()
        {
            var body = PlayerSaveResetRequest.BuildBody();

            Assert.That(body, Is.EqualTo("{\"confirm\":\"RESET_PLAYER_SAVE\"}"));
            Assert.That(PlayerSaveResetRequest.ConfirmToken, Is.EqualTo("RESET_PLAYER_SAVE"));
        }

        [Test]
        public void TryParseResponse_SuccessEnvelope_ReadsSuccessFlag()
        {
            var parsed = PlayerSaveResetRequest.TryParseResponse("{\"success\":true}", out var response);

            Assert.That(parsed, Is.True);
            Assert.That(response.Success, Is.True);
            Assert.That(response.ErrorCode, Is.Null);
            Assert.That(response.ErrorMessage, Is.Null);
        }

        [Test]
        public void TryParseResponse_ErrorEnvelope_ReadsCodeAndMessage()
        {
            const string json = "{\"success\":false,\"errorCode\":\"RESET_DISABLED\",\"errorMessage\":\"Disabled\"}";

            var parsed = PlayerSaveResetRequest.TryParseResponse(json, out var response);

            Assert.That(parsed, Is.True);
            Assert.That(response.Success, Is.False);
            Assert.That(response.ErrorCode, Is.EqualTo("RESET_DISABLED"));
            Assert.That(response.ErrorMessage, Is.EqualTo("Disabled"));
        }

        [Test]
        public void TryParseResponse_MissingFields_DefaultsToFailure()
        {
            var parsed = PlayerSaveResetRequest.TryParseResponse("{}", out var response);

            Assert.That(parsed, Is.True);
            Assert.That(response.Success, Is.False);
        }

        [Test]
        public void TryParseResponse_NonJsonBody_ReturnsFalse()
        {
            // A proxy HTML error page must not throw — the window falls back to the raw body.
            Assert.That(PlayerSaveResetRequest.TryParseResponse("<html>502</html>", out var response), Is.False);
            Assert.That(response, Is.Null);
        }

        [Test]
        public void TryParseResponse_EmptyBody_ReturnsFalse()
        {
            Assert.That(PlayerSaveResetRequest.TryParseResponse("", out _), Is.False);
            Assert.That(PlayerSaveResetRequest.TryParseResponse(null, out _), Is.False);
        }
    }
}
