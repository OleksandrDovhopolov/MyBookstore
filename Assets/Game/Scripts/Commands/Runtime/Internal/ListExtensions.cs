using System.Collections.Generic;

namespace Game.Commands {
    internal static class ListExtensions {
        public static T GetAndRemove<T>(this IList<T> list, int index) {
            var item = list[index];
            list.RemoveAt(index);
            return item;
        }
    }
}
