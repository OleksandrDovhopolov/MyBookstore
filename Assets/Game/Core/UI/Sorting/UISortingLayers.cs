namespace Game.UI
{
    // Sort Order ranges per logical layer. Used instead of Unity SortingLayers because
    // nested Canvases under a Screen Space Overlay parent silently ignore sortingLayer
    // changes — only sortingOrder is honored. Each layer gets a 1000-wide segment.
    public static class UISortingLayers
    {
        public const int OrderStep = 10;

        public const int MainBase = 1000;
        public const int AdditionalBase = 2000;
        public const int SystemBase = 3000;
        public const int DevelopBase = 4000;

        public static int BaseOrderFor(WindowLayer layer) => layer switch
        {
            WindowLayer.Main => MainBase,
            WindowLayer.Additional => AdditionalBase,
            WindowLayer.System => SystemBase,
            WindowLayer.Develop => DevelopBase,
            _ => MainBase,
        };
    }
}
