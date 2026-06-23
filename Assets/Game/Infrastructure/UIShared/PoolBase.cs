using System;
using UnityEngine;

namespace UIShared
{
    [Serializable]
    public abstract class PoolBase
    {
        public GameObject Prefab => prefab;
        [SerializeField] private GameObject prefab;

        public Transform Parent => parent;
        [SerializeField] private Transform parent;

        protected void InitPool(GameObject prefab, Transform parent)
        {
            this.prefab = prefab;
            this.parent = parent;
        }

        protected GameObject InstantiatePrefab()
        {
            return InstantiateCommand.Get().Invoke(prefab, parent);
        }
    }
}
