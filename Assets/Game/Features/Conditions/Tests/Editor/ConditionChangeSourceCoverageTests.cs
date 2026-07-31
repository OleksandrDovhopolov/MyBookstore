using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Conditions.API;
using NUnit.Framework;

namespace Game.Conditions.Tests.Editor
{
    public sealed class ConditionChangeSourceCoverageTests
    {
        private static readonly Dictionary<string, string> StaticFactories = new(StringComparer.Ordinal)
        {
            ["Game.Conditions.Services.ManualConditionFactory"] =
                "manual is always never-met and has no runtime data source.",
            ["Game.SalesStats.Conditions.SoldGenreConditionFactory"] =
                "legacy direct subscription to ISalesStatsService.Changed.",
            ["Game.SalesStats.Conditions.SoldTotalConditionFactory"] =
                "legacy direct subscription to ISalesStatsService.Changed.",
            ["Game.SalesStats.Conditions.SoldGenreAtLocationConditionFactory"] =
                "legacy direct subscription to ISalesStatsService.Changed.",
            ["Game.SalesStats.Conditions.SoldGenreInSingleDayConditionFactory"] =
                "legacy direct subscription to ISalesStatsService.Changed.",
            ["Game.SalesStats.Conditions.ActivePickGenreConditionFactory"] =
                "legacy direct subscription to ISalesStatsService.Changed.",
            ["Game.Inventory.Conditions.HaveItemConditionFactory"] =
                "legacy direct subscription to IInventoryService.Changed.",
            ["Game.Decor.Conditions.DecorEquippedConditionFactory"] =
                "legacy direct subscription to IDecorPlacementService.PlacementChanged.",
            ["Game.DayCycle.Conditions.DayAtLeastConditionFactory"] =
                "legacy direct subscription to IDayProgressService.PhaseChanged.",
            ["Game.DayCycle.Conditions.WeatherIsConditionFactory"] =
                "legacy direct subscription to IDayProgressService.PhaseChanged.",
            ["Game.Tutorial.Conditions.TutorialCompletedConditionFactory"] =
                "tutorial completion is pushed through TutorialService/ITutorialReevaluationGate."
        };

        [Test]
        public void RuntimeConditionFactories_DeclareChangeSource_OrAreExplicitlyAllowlisted()
        {
            var missing = RuntimeConditionFactoryTypes()
                .Where(t => !typeof(IConditionChangeSource).IsAssignableFrom(t))
                .Where(t => !StaticFactories.ContainsKey(t.FullName))
                .Select(t => t.FullName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            if (missing.Length == 0) return;

            Assert.Fail(
                "Condition factories must declare their runtime change source via IConditionChangeSource, " +
                "or be added to StaticFactories with a reason. Missing: " + string.Join(", ", missing));
        }

        private static IEnumerable<Type> RuntimeConditionFactoryTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (string.IsNullOrEmpty(name)) continue;
                if (name.IndexOf("Tests", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                foreach (var type in GetTypesSafely(assembly))
                {
                    if (type == null || type.IsAbstract || type.IsInterface) continue;
                    if (!typeof(IConditionFactory).IsAssignableFrom(type)) continue;
                    yield return type;
                }
            }
        }

        private static IEnumerable<Type> GetTypesSafely(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }
    }
}
