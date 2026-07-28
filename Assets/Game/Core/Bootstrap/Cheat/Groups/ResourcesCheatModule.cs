using System;
using System.Threading;
using cheatModule;
using Cysharp.Threading.Tasks;
using Game.Resources.API;
using Infrastructure.ResourceAnimations;
using MessagePipe;
using UIShared;
using UnityEngine;

namespace Game.Cheat
{
    public class ResourcesCheatModule : ICheatsModule
    {
        private const string CardsGroup = "Resources";
        private const string LogTag = "[ResourcesCheat]";
        private const string CheatReason = "cheat";

        private readonly IResourcesService _resources;
        private readonly IResourceAnimationService _animations;
        private readonly IPublisher<ResourceCounterCountUpRequested> _countUpPublisher;
        private readonly CancellationToken _ct;

        public ResourcesCheatModule(
            IResourcesService resources,
            IResourceAnimationService animations,
            IPublisher<ResourceCounterCountUpRequested> countUpPublisher,
            CancellationToken ct)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _animations = animations;
            _countUpPublisher = countUpPublisher;
            _ct = ct;
        }

        public void Initialize(ICheatsContainer cheatsContainer)
        {
            AddGoldButton(cheatsContainer, 100);
            AddGoldButton(cheatsContainer, 1000);
            AddGoldButton(cheatsContainer, 10000);
        }

        private void AddGoldButton(ICheatsContainer container, int amount)
        {
            container.AddItem<CheatButtonItem>(item =>
                item.OnClick($"+{amount} Gold", () => OnGoldButtonClicked(item, amount))
                    .WithGroup(CardsGroup));
        }

        private void OnGoldButtonClicked(CheatButtonItem item, int amount)
        {
            var sourceScreenPoint = TryGetButtonScreenPoint(item);
            AddGoldAsync(amount, sourceScreenPoint).Forget();
        }

        private async UniTaskVoid AddGoldAsync(int amount, Vector2? sourceScreenPoint)
        {
            try
            {
                await _resources.AddAsync(ResourceIds.Gold, amount, CheatReason, _ct);
                Debug.Log($"{LogTag} +{amount} gold (total now {_resources.GetAmount(ResourceIds.Gold)}).");

                if (_animations == null || sourceScreenPoint == null) return;

                await _animations.PlayAsync(
                    new ResourceAnimationRequest(
                        ResourceIds.Gold,
                        amount,
                        ResourceAnimationEndpoint.ScreenPoint(sourceScreenPoint.Value),
                        ResourceAnimationEndpoint.RegisteredTarget(
                            ResourceAnimationTargetIds.Resource(ResourceIds.Gold)),
                        onParticleArrived: OnGoldParticleArrived),
                    _ct);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void OnGoldParticleArrived()
        {
            _countUpPublisher?.Publish(new ResourceCounterCountUpRequested(ResourceIds.Gold));
        }

        private static Vector2? TryGetButtonScreenPoint(CheatButtonItem item)
        {
            if (item == null || item.transform is not RectTransform rect) return null;

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var worldCenter = (corners[0] + corners[2]) * 0.5f;

            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            return RectTransformUtility.WorldToScreenPoint(camera, worldCenter);
        }
    }
}
