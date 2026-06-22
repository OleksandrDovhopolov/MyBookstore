using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UIShared
{
    [Serializable]
    public class UIListPool<T> : ListPoolBase<T> where T : MonoBehaviour
    {
        private int LastActiveIndex { get; set; } = -1;

        private int Length => Pool?.Count ?? 0;

        public UIListPool(){ }

        public UIListPool(GameObject prefab, Transform parent, int count = 10)
        {
            InitPool(prefab, parent, count);
        }

        public void CheckSize(int size)
        {
            if (Length >= size) return;
            size += size / 3;
            AddObject(size - Length);
        }

        protected override void AddObject(int count)
        {
            for (; count > 0; count--)
            {
                Pool.Add(SpawnNewElem());
            }
        }

        private T SpawnNewElem()
        {
            var newElem = InstantiatePrefab().GetComponent<T>();
            newElem.gameObject.SetActive(false);

            return newElem;
        }

       public T GetNext()
       {
           var curIndex = LastActiveIndex + 1;
           CheckSize(curIndex + 1);
       
           var elem = Pool[curIndex];
           if (elem == null || IsNull(elem))
           {
               elem = SpawnNewElem();
               elem.transform.SetSiblingIndex(curIndex);
               Pool[curIndex] = elem;
           }

           CleanupElement(elem);
       
           if (!elem.gameObject.activeSelf)
               elem.gameObject.SetActive(true);
       
           LastActiveIndex = curIndex;
           return elem;
       }

        public void Return(T element)
        {
            if (element == null || IsNull(element) || Pool == null || Pool.Count == 0) return;

            var elementIndex = Pool.IndexOf(element);
            if (elementIndex < 0) return;
            if (!element.gameObject.activeSelf) return;

            if (elementIndex <= LastActiveIndex)
            {
                var lastIndex = LastActiveIndex;
                var lastActiveElement = Pool[lastIndex];

                if (elementIndex != lastIndex && lastActiveElement != null && !IsNull(lastActiveElement))
                {
                    Pool[elementIndex] = lastActiveElement;
                    lastActiveElement.transform.SetSiblingIndex(elementIndex);
                    Pool[lastIndex] = element;
                    element.transform.SetSiblingIndex(lastIndex);
                }

                LastActiveIndex--;
            }

            CleanupElement(element);
            element.gameObject.SetActive(false);
        }

        public override void DisableNonActive()
        {
            if (Pool == null) return;

            for (var i = LastActiveIndex + 1; i < Pool.Count; i++)
            {
                if (IsNull(Pool[i])) continue;
                CleanupElement(Pool[i]);
                Pool[i].gameObject.SetActive(false);
            }

            LastActiveIndex = -1;
        }

        public override void DisableAll()
        {
            LastActiveIndex = -1;
            DisableNonActive();
        }

        public IEnumerable<T> ActiveElements()
        {
            foreach (var poolElem in Pool)
            {
                if (!poolElem.gameObject.activeSelf) break;

                yield return (T)poolElem;
            }
        }

        public bool IsEmpty() => !Pool.Any(poolElem => poolElem != null && poolElem.gameObject.activeSelf);
        public int Count() => Pool.Count(poolElem => poolElem != null && poolElem.gameObject.activeSelf);
        
        private static void CleanupElement(T element)
        {
            if (element is ICleanup cleanup)
            {
                cleanup.Cleanup();
            }
        }
        
        
        public bool IsNull(Object o) => o == null || !o;
    }
}
