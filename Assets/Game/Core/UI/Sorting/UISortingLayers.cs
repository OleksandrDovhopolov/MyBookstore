namespace Game.UI
{
    public static class UISortingLayers
    {
        public const string Hud = "UI_Hud";
        public const string Main = "UI_Main";
        public const string Additional = "UI_Additional";
        public const string System = "UI_System";
        public const string Develop = "UI_Develop";

        public const int OrderStep = 10;

        public static string For(WindowLayer layer) => layer switch
        {
            WindowLayer.Main => Main,
            WindowLayer.Additional => Additional,
            WindowLayer.System => System,
            WindowLayer.Develop => Develop,
            _ => Main,
        };
    }
}
