namespace Game.UI
{
    public interface IUISortingController
    {
        void Apply(IWindowController controller, WindowLayer layer);
        void Release(IWindowController controller);
    }
}
