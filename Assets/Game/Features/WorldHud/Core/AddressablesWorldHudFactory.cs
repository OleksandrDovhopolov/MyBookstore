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

namespace Game.WorldHud
{
    public sealed class AddressablesWorldHudFactory : IWorldHudFactory
    {
        private const string Tag = "[WorldHudFactory]";

        private readonly IObjectResolver _resolver;
        private readonly Dictionary<WorldHud, string> _addressByHud = new();

        public AddressablesWorldHudFactory(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public async UniTask<T> CreateAsync<T>(CancellationToken ct) where T : WorldHud
        {
            var attribute = typeof(T).GetCustomAttribute<WorldHudAttribute>();
            if (attribute == null)
            {
                throw new InvalidOperationException(
                    $"{Tag} {typeof(T).Name} is missing [WorldHud] attribute.");
            }

            var prefab = await ProdAddressablesWrapper.LoadAsync<GameObject>(attribute.PrefabAddress, ct);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"{Tag} Prefab at address '{attribute.PrefabAddress}' not found.");
            }

            var prefabComponent = prefab.GetComponent<T>();
            if (prefabComponent == null)
            {
                ProdAddressablesWrapper.Release(prefab);
                throw new InvalidOperationException(
                    $"{Tag} Prefab '{attribute.PrefabAddress}' has no {typeof(T).Name} component.");
            }

            // Instantiate inactive so OnEnable on the spawned object doesn't fire before AttachInternal.
            var wasActive = prefabComponent.gameObject.activeSelf;
            prefabComponent.gameObject.SetActive(false);
            var instance = _resolver.Instantiate(prefabComponent);
            prefabComponent.gameObject.SetActive(wasActive);

            _addressByHud[instance] = attribute.PrefabAddress;
            return instance;
        }

        public void Destroy(WorldHud hud)
        {
            if (hud == null) return;

            if (_addressByHud.Remove(hud, out var address))
            {
                ProdAddressablesWrapper.Release(address);
            }

            if (hud.gameObject != null) Object.Destroy(hud.gameObject);
        }
    }
}
