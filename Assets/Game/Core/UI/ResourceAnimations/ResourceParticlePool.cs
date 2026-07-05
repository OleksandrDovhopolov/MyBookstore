using System.Collections.Generic;
using Infrastructure.ResourceAnimations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.UI.ResourceAnimations
{
    internal sealed class ResourceParticlePool
    {
        private readonly ResourceParticleView _prefab;
        private readonly Transform _parent;
        private readonly Queue<ResourceParticleView> _pool = new();

        public ResourceParticlePool(ResourceParticleView prefab, Transform parent)
        {
            _prefab = prefab;
            _parent = parent;
        }

        public bool CanSpawn => _prefab != null && _parent != null;

        public ResourceParticleView Get()
        {
            ResourceParticleView particle = null;
            while (_pool.Count > 0 && particle == null)
                particle = _pool.Dequeue();

            if (particle == null)
                particle = Object.Instantiate(_prefab, _parent, false);

            particle.transform.SetParent(_parent, false);
            particle.gameObject.SetActive(true);
            particle.Cleanup();
            return particle;
        }

        public void Release(ResourceParticleView particle)
        {
            if (particle == null) return;

            particle.Cleanup();
            particle.gameObject.SetActive(false);
            particle.transform.SetParent(_parent, false);
            _pool.Enqueue(particle);
        }
    }
}
