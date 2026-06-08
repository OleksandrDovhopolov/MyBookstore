namespace Game.Commands {
    public class FakeProgressSettings : ProgressSettings {
        public const int FAKE_STEP_DEFAULT = 1;
        public const int FAKE_TIME_MS = 400;

        /// <summary>На сколько менять прогресс на каждое срабатывание таймера (например, 1%).</summary>
        public int FakeStep { get; }

        /// <summary>Период срабатывания фейкового таймера, мс.</summary>
        public int FakeTime { get; }

        public FakeProgressSettings(int percents = CALC_AUTO, int fakeTime = FAKE_TIME_MS, int fakeStep = FAKE_STEP_DEFAULT)
            : base(percents) {
            FakeStep = fakeStep;
            FakeTime = fakeTime;
        }
    }
}
