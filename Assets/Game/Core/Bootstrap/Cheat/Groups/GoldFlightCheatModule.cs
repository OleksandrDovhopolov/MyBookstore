using System;
using System.Threading;
using cheatModule;
using Cysharp.Threading.Tasks;
using Game.Resources.API;
using Infrastructure.ResourceAnimations;
using UnityEngine;

namespace Game.Cheat
{
    /// <summary>
    /// Cheat buttons that grant gold and play the coin-flight animation starting from the pressed
    /// button toward the HUD gold counter. Mirrors <see cref="ResourcesCheatModule"/> but with a
    /// flight visual; the animation is best-effort (skipped when the service or a gold target is
    /// unavailable).
    /// </summary>
    public class GoldFlightCheatModule : ICheatsModule
    {
        private const string CardsGroup = "Gold (fly)";
        private const string LogTag = "[GoldFlightCheat]";
        private const string CheatReason = "cheat";

        private static readonly int[] Amounts = { 1, 25, 100 };

        private readonly IResourcesService _resources;
        private readonly IResourceAnimationService _animations;
        private readonly CancellationToken _ct;

        public GoldFlightCheatModule(
            IResourcesService resources,
            IResourceAnimationService animations,
            CancellationToken ct)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _animations = animations; // optional: the flight is a nice-to-have, gold still grants.
            _ct = ct;
        }

        public void Initialize(ICheatsContainer cheatsContainer)
        {
            foreach (var amount in Amounts)
                AddGoldButton(cheatsContainer, amount);
        }

        private void AddGoldButton(ICheatsContainer container, int amount)
        {
            container.AddItem<CheatButtonItem>(item =>
                item.OnClick($"+{amount} Gold", () => OnGoldButtonClicked(item, amount))
                    .WithGroup(CardsGroup));
        }

        // The panel closes on click (default cheat behavior), which tears the button down, so the
        // flight source is captured as a screen point synchronously here — before CloseAll — rather
        // than passing the live RectTransform into the async play call.
        private void OnGoldButtonClicked(CheatButtonItem item, int amount)
        {
            var sourceScreenPoint = TryGetButtonScreenPoint(item);
            GrantGoldAsync(amount, sourceScreenPoint).Forget();
        }

        private async UniTaskVoid GrantGoldAsync(int amount, Vector2? sourceScreenPoint)
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
                            ResourceAnimationTargetIds.Resource(ResourceIds.Gold))),
                    _ct);
            }
            catch (OperationCanceledException)
            {
            }
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
