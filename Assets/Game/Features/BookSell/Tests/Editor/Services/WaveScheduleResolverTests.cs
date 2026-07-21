using System;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class WaveScheduleResolverTests
    {
        [Test]
        public void Resolve_ValidWaveSchedule_ReturnsConfiguredValues()
        {
            var configs = ConfigsWith(new DayConfig
            {
                Id = "d1",
                DayIndex = 1,
                WaveSizes = new[] { 1, 3 },
                WaveGapSeconds = 2.5f
            });

            var result = WaveScheduleResolver.Resolve(configs, 1);

            CollectionAssert.AreEqual(new[] { 1, 3 }, result.waveSizes);
            Assert.AreEqual(2.5f, result.gapSeconds);
        }

        [Test]
        public void Resolve_MissingDay_ReturnsSingleWaveSchedule()
        {
            var result = WaveScheduleResolver.Resolve(ConfigsWith(), 1);

            Assert.IsNull(result.waveSizes);
            Assert.AreEqual(0f, result.gapSeconds);
        }

        [Test]
        public void Resolve_NullOrEmptyWaveSizes_ReturnsSingleWaveSchedule()
        {
            var nullResult = WaveScheduleResolver.Resolve(
                ConfigsWith(new DayConfig { Id = "d1", DayIndex = 1, WaveSizes = null }),
                1);
            var emptyResult = WaveScheduleResolver.Resolve(
                ConfigsWith(new DayConfig { Id = "d2", DayIndex = 2, WaveSizes = Array.Empty<int>() }),
                2);

            Assert.IsNull(nullResult.waveSizes);
            Assert.IsNull(emptyResult.waveSizes);
        }

        [Test]
        public void Resolve_InvalidWaveSizes_WarnsAndReturnsSingleWaveSchedule()
        {
            var configs = ConfigsWith(new DayConfig { Id = "d1", DayIndex = 1, WaveSizes = new[] { 1, 0 } });

            LogAssert.Expect(LogType.Warning, "[Sales.Setup] day 'd1' has invalid waveSizes; using one wave.");
            var result = WaveScheduleResolver.Resolve(configs, 1);

            Assert.IsNull(result.waveSizes);
        }

        [Test]
        public void Resolve_NegativeGap_WarnsAndReturnsZeroGap()
        {
            var configs = ConfigsWith(new DayConfig { Id = "d1", DayIndex = 1, WaveGapSeconds = -1f });

            LogAssert.Expect(LogType.Warning, "[Sales.Setup] day 'd1' has negative waveGapSeconds=-1; using 0.");
            var result = WaveScheduleResolver.Resolve(configs, 1);

            Assert.AreEqual(0f, result.gapSeconds);
        }

        private static FakeConfigsService ConfigsWith(params DayConfig[] days)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(days);
            return configs;
        }
    }
}
