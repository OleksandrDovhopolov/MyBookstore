namespace Game.UI
{
    public class WindowArgs
    {
        public WindowLayer? LayerOverride { get; private set; }
        public IWindowController ParentWindow { get; private set; }
        public int PrimaryKey { get; set; }

        public WindowArgs AsMain() { LayerOverride = WindowLayer.Main; return this; }
        public WindowArgs AsAdditional() { LayerOverride = WindowLayer.Additional; return this; }
        public WindowArgs AsSystem() { LayerOverride = WindowLayer.System; return this; }
        public WindowArgs AsDevelop() { LayerOverride = WindowLayer.Develop; return this; }

        public WindowArgs WithParent(IWindowController parent)
        {
            ParentWindow = parent;
            return this;
        }

        public virtual int GetPrimaryKey() => PrimaryKey;
    }
}
