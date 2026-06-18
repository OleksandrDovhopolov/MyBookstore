using System;

namespace Game.UI
{
    public interface IUiWindowFilter
    {
        bool CanBeShown(Type controllerType);
    }
}
