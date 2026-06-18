using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Infrastructure;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Game.UI
{
    public sealed class AddressablesWindowFactory : IWindowFactory
    {
        private const string Tag = "[WindowFactory]";

        private readonly IObjectResolver _resolver;
        private readonly Dictionary<IWindowController, string> _addressByController = new();

        public AddressablesWindowFactory(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public async UniTask<T> CreateAsync<T>(Transform parent, CancellationToken ct)
            where T : class, IWindowController, new()
        {
            var attribute = typeof(T).GetCustomAttribute<WindowAttribute>();
            if (attribute == null)
            {
                throw new InvalidOperationException(
                    $"{Tag} Controller {typeof(T).Name} is missing [Window] attribute.");
            }

            var prefab = await ProdAddressablesWrapper.LoadAsync<GameObject>(attribute.PrefabAddress, ct);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"{Tag} Prefab at address '{attribute.PrefabAddress}' not found.");
            }

            var view = prefab.GetComponent<WindowView>();
            if (view == null)
            {
                ProdAddressablesWrapper.Release(prefab);
                throw new InvalidOperationException(
                    $"{Tag} Prefab '{attribute.PrefabAddress}' has no WindowView component.");
            }

            // Spawn inactive so OnEnable does not fire before Configure/Inject finishes.
            var wasActive = view.gameObject.activeSelf;
            view.gameObject.SetActive(false);
            var instance = _resolver.Instantiate(view, parent);
            view.gameObject.SetActive(wasActive);

            var controller = new T();
            _resolver.Inject(controller);
            controller.Configure(instance, attribute);

            _addressByController[controller] = attribute.PrefabAddress;
            instance.gameObject.SetActive(false);

            return controller;
        }

        public void Destroy(IWindowController controller)
        {
            if (controller == null) return;

            if (controller.View is { GameObject: { } go })
            {
                Object.Destroy(go);
            }

            if (_addressByController.Remove(controller, out var address))
            {
                ProdAddressablesWrapper.Release(address);
            }

            controller.Dispose();
        }
    }
}
