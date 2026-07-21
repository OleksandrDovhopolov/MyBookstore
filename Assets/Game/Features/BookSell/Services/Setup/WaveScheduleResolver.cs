using System;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;

namespace Book.Sell.Services
{
    public static class WaveScheduleResolver
    {
        private const string LogPrefix = "[Sales.Setup]";

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
            if (day?.WaveGapSeconds == null) return 0f;
            if (day.WaveGapSeconds.Value >= 0f) return day.WaveGapSeconds.Value;

            Debug.LogWarning($"{LogPrefix} day '{day.Id}' has negative waveGapSeconds={day.WaveGapSeconds.Value}; using 0.");
            return 0f;
        }
    }
}
