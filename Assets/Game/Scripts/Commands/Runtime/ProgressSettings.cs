namespace Game.Commands {
    public class ProgressSettings : IProgressSettings {
        public const int CALC_AUTO = -1;
        public static ProgressSettings AUTO => new ProgressSettings();
        public static ProgressSettings ZERO => new ProgressSettings(0);

        public int Percents { get; set; }

        public ProgressSettings(int percents = CALC_AUTO) {
            Percents = percents;
        }
    }
}
