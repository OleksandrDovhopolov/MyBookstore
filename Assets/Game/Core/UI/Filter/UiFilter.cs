using System;
using System.Collections.Generic;

namespace Game.UI
{
    public interface IUiFilter
    {
        bool CanBeShown(Type controllerType);
        void AddFilter(IUiWindowFilter filter);
        void RemoveFilter(IUiWindowFilter filter);
    }

    public sealed class UiFilter : IUiFilter
    {
        private readonly List<IUiWindowFilter> _filters = new();

        public bool CanBeShown(Type controllerType)
        {
            foreach (var filter in _filters)
            {
                if (!filter.CanBeShown(controllerType)) return false;
            }
            return true;
        }

        public void AddFilter(IUiWindowFilter filter)
        {
            if (filter != null && !_filters.Contains(filter)) _filters.Add(filter);
        }

        public void RemoveFilter(IUiWindowFilter filter) => _filters.Remove(filter);
    }
}
