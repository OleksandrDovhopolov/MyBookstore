using UnityEngine;

namespace UIShared
{
    public delegate GameObject PoolInstantiateCommand(GameObject prefab, Transform parent, bool worldPositionStays = false);
    
    public class InstantiateCommand
    {
        public static PoolInstantiateCommand Default { get; } = Object.Instantiate;

        private static PoolInstantiateCommand _override;

        public static void Set(PoolInstantiateCommand command) => _override = command;

        public static PoolInstantiateCommand Get() => _override ?? Default;
    }
}
