using Analytics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnalyticsTests.Editor
{
    /// <summary>
    /// REL-12: debug logging and the reported environment derive from the build type rather than from
    /// a flag someone has to remember to flip before a release build.
    ///
    /// <see cref="Debug.isDebugBuild"/> is always true inside the Editor, so these tests can only
    /// exercise the development branch — which is exactly the point: existing Editor behaviour must
    /// not change. The release branch (no debug logging, environment "production") is unreachable from
    /// EditMode and is verified by making a player build with "Development Build" unticked.
    /// </summary>
    public sealed class AnalyticsConfigSOTests
    {
        [Test]
        public void EditorIsTreatedAsDevelopmentBuild()
        {
            Assert.That(AnalyticsBuildContext.IsDevelopmentBuild, Is.True,
                "Every other assertion in this fixture assumes the Editor is a development build.");
        }

        [Test]
        public void IsDebugLoggingEnabled_InEditor_FollowsSerializedFlag()
        {
            Assert.That(CreateConfig(debugLogging: true, environment: "development").IsDebugLoggingEnabled, Is.True);
            Assert.That(CreateConfig(debugLogging: false, environment: "development").IsDebugLoggingEnabled, Is.False);
        }

        [Test]
        public void Environment_InEditor_FollowsSerializedField()
        {
            Assert.That(CreateConfig(debugLogging: true, environment: "staging").Environment, Is.EqualTo("staging"));
        }

        [Test]
        public void Environment_InEditor_FallsBackToDevelopment_WhenBlank()
        {
            Assert.That(CreateConfig(debugLogging: true, environment: "   ").Environment,
                Is.EqualTo(AnalyticsBuildContext.DevelopmentEnvironment));
        }

        [Test]
        public void DefaultConfig_MirrorsTheSameBuildTypeSwitch()
        {
            // The Default* config is the silent fallback when the SO is not assigned on
            // BootstrapInstaller — it must not resurrect debug logging in a release build.
            var config = new DefaultAnalyticsConfig();

            Assert.That(config.IsDebugLoggingEnabled, Is.EqualTo(AnalyticsBuildContext.IsDevelopmentBuild));
            Assert.That(config.Environment, Is.EqualTo(AnalyticsBuildContext.DevelopmentEnvironment));
        }

        private static AnalyticsConfigSO CreateConfig(bool debugLogging, string environment)
        {
            var config = ScriptableObject.CreateInstance<AnalyticsConfigSO>();
            var serialized = new SerializedObject(config);
            serialized.FindProperty("_isDebugLoggingEnabled").boolValue = debugLogging;
            serialized.FindProperty("_environment").stringValue = environment;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }
    }
}
