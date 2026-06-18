using System;
using cheatModule;

namespace Game.Cheat
{
    public interface ICheatsContainer
    {
        void AddItem<T>(Action<T> initializer) where T : CheatItem;
    }
}