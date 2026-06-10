namespace Game.Bootstrap.Loading
{
    // Настройки лоадера, прокинутые из BootstrapInstaller (Game.Bootstrap asmdef).
    // Живёт в Loading-сборке, чтобы entry point не зависел от Game.Bootstrap
    // (это создавало бы циклическую asmdef-зависимость).
    public sealed class LoadingSettings
    {
        public string GameplaySceneName { get; }

        public LoadingSettings(string gameplaySceneName)
        {
            GameplaySceneName = gameplaySceneName;
        }
    }
}
