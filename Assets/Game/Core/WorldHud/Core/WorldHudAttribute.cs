using System;

namespace Game.WorldHud
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class WorldHudAttribute : Attribute
    {
        public string PrefabAddress { get; }

        public WorldHudAttribute(string prefabAddress)
        {
            PrefabAddress = prefabAddress;
        }
    }
}
