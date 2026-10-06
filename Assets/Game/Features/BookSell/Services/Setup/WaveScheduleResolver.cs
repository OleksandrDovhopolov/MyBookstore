using System;
using System.Globalization;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;

namespace Book.Sell.Services
{
    public static class WaveScheduleResolver
    {
        private const string LogPrefix = "[Sales.Setup]";

        /// <summary>
        /// Gap used when a day does not author <c>waveGapSeconds</c> — days.json only covers the scripted
        /// opening days, everything past them falls back here. Matches what day 1 and day 2 author.
        /// </summary>
        public const float DefaultWaveGapSeconds = 0.5f;

        public static (int[] waveSizes, float gapSeconds) Resolve(IConfigsService configs, int dayIndex)
        {
            if (configs == null) throw new ArgumentNullException(nameof(configs));

            var day = FindDayConfig(configs, dayIndex);
            var waveSizes = ResolveWaveSizes(day);
            var gapSeconds = ResolveWaveGapSeconds(day);
            return (waveSizes, gapSeconds);
        }

        private static DayConfig FindDayConfig(IConfigsService configs, int dayIndex)
        {
            foreach (var day in configs.GetAll<DayConfig>())
            {
                if (day?.DayIndex == dayIndex) return day;
            }

            return null;
        }

        private static int[] ResolveWaveSizes(DayConfig day)
        {
            var waveSizes = day?.WaveSizes;
            if (waveSizes == null || waveSizes.Length == 0) return null;

            for (var i = 0; i < waveSizes.Length; i++)
            {
                if (waveSizes[i] <= 0)
                {
                    Debug.LogWarning($"{LogPrefix} day '{day.Id}' has invalid waveSizes; using one wave.");
                    return null;
                }
            }

            return waveSizes;
        }

        private static float ResolveWaveGapSeconds(DayConfig day)
        {
            if (day?.WaveGapSeconds == null) return DefaultWaveGapSeconds;
            if (day.WaveGapSeconds.Value >= 0f) return day.WaveGapSeconds.Value;

            // Invariant formatting: the editor runs under a locale that writes 0,5 for 0.5, which would make
            // the message (and the test asserting it) depend on the machine.
            Debug.LogWarning(
                $"{LogPrefix} day '{day.Id}' has negative waveGapSeconds=" +
                $"{day.WaveGapSeconds.Value.ToString(CultureInfo.InvariantCulture)}; " +
                $"using {DefaultWaveGapSeconds.ToString(CultureInfo.InvariantCulture)}.");
            return DefaultWaveGapSeconds;
        }
    }
}
