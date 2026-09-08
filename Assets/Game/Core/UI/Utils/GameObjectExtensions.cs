using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    public static class GameObjectExtensions
    {
     public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component == null)
            {
                component = go.AddComponent<T>();
            }

            return component;
        }

        public static void DisposePointerHandlerHelper(this GameObject go)
        {
            var pointerHandlerHelper = go.GetComponent<PointerHandlerHelper>();
            if (pointerHandlerHelper != null)
            {
                pointerHandlerHelper.Dispose();
            }
        }

        #region OnPointerUp

        public static void OnPointerUpAsObservable(this GameObject go, Action action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnPointerUp += action;
        }

        public static void OnPointerUpAsObservable(this GameObject go, Action<PointerEventData> action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnPointerUpData += action;
        }

        public static void UnsubscribeOnPointerUpAsObservable(this GameObject go, Action action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnPointerUp -= action;
        }

        public static void UnsubscribeOnPointerUpAsObservable(this GameObject go, Action<PointerEventData> action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnPointerUpData -= action;
        }

        #endregion
        
        #region OnPointerDown

        public static void OnPointerDownAsObservable(this GameObject go, Action<PointerEventData> action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnPointerDown += action;
        }

        public static void UnsubscribeOnPointerDownAsObservable(this GameObject go, Action<PointerEventData> action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnPointerDown -= action;
        }

        #endregion

        #region OnBeginDrag

        public static void OnBeginDragAsObservable(this GameObject go, Action action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnBeginDrag += action;
        }

        public static void OnBeginDragAsObservable(this GameObject go, Action<PointerEventData> action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnBeginDragData += action;
        }

        public static void UnsubscribeOnBeginDragAsObservable(this GameObject go, Action action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnBeginDrag -= action;
        }

        public static void UnsubscribeOnBeginDragAsObservable(this GameObject go, Action<PointerEventData> action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnBeginDragData -= action;
        }

        #endregion

        #region OnDrag

        public static void OnDragAsObservable(this GameObject go, Action<PointerEventData> action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnDrag += action;
        }

        public static void UnsubscribeOnDragAsObservable(this GameObject go, Action<PointerEventData> action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnDrag -= action;
        }

        #endregion

        #region OnEndDrag

        public static void OnEndDragAsObservable(this GameObject go, Action action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnEndDrag += action;
        }

        public static void OnEndDragAsObservable(this GameObject go, Action<PointerEventData> action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnEndDragData += action;
        }

        public static void UnsubscribeOnEndDragAsObservable(this GameObject go, Action action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnEndDrag -= action;
        }

        public static void UnsubscribeOnEndDragAsObservable(this GameObject go, Action<PointerEventData> action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnEndDragData -= action;
        }

        #endregion

        #region OnPointerClick

        public static void OnPointerClickAsObservable(this GameObject go, Action action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnPointerClick += action;
        }

        public static void OnPointerClickAsObservable(this GameObject go, Action<PointerEventData> action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnPointerClickData += action;
        }

        public static void UnsubscribeOnPointerClickAsObservable(this GameObject go, Action action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnPointerClick -= action;
        }

        public static void UnsubscribeOnPointerClickAsObservable(this GameObject go, Action<PointerEventData> action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnPointerClickData -= action;
        }

        #endregion

        #region OnDisable

        public static void OnDisableAsObservable(this GameObject go, Action action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnDisable += action;
        }

        public static void UnsubscribeOnDisableAsObservable(this GameObject go, Action action)
        {
            var pointerHandlerHelper = go.gameObject.GetOrAddComponent<PointerHandlerHelper>();
            pointerHandlerHelper.ActionOnDisable -= action;
        }

        #endregion
    }
}
