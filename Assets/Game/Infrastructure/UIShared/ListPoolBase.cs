using System.Collections.Generic;
using UnityEngine;

namespace UIShared
{
    public abstract class ListPoolBase<T> : PoolBase
    {
        private List<T> _pool;
        
        protected List<T> Pool
        {
            get
            {
                if (_pool != null) return _pool;
                
                _pool = new List<T>();
                base.InitPool(Prefab, Parent);

                return _pool;
            }
        }
        
        protected void InitPool(GameObject prefab, Transform parent, int startSize = 10)
        {
            base.InitPool(prefab, parent);

            AddObject(startSize);
        }
        
        protected abstract void AddObject(int count);
        public abstract void DisableNonActive();
        public abstract void DisableAll();
    }
}
