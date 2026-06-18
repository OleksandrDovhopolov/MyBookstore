using System;

namespace Game.UI
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class WindowAttribute : Attribute
    {
        public string PrefabAddress { get; }
        public WindowType Type { get; }
        public bool KeepInCache { get; }
        public int Priority { get; }

        public WindowAttribute(string prefabAddress, WindowType type, bool keepInCache = false, int priority = 0)
        {
            PrefabAddress = prefabAddress;
            Type = type;
            KeepInCache = keepInCache;
            Priority = priority;
        }
    }
}
