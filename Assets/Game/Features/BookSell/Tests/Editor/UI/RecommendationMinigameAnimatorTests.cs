using System.Reflection;
using Book.Sell.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Book.Sell.Tests.Editor.UI
{
    public sealed class RecommendationMinigameAnimatorTests
    {
        [Test]
        public void MissingOptionalRefs_DoNotThrow()
        {
            var root = new GameObject("animator");
            try
            {
                var animator = root.AddComponent<RecommendationMinigameAnimator>();

                Assert.DoesNotThrow(() => animator.PrepareForRequest());
                Assert.DoesNotThrow(() => animator.PlayRequestIntro());
                Assert.DoesNotThrow(() => animator.ShowBookDetail());
                Assert.DoesNotThrow(() => animator.HideBookDetail());
                Assert.DoesNotThrow(() => animator.PlayResult("ok"));
                Assert.DoesNotThrow(() => animator.KillAll());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ShowThenHideBookDetail_KillsPreviousTweenWithoutThrowing()
        {
            var root = new GameObject("animator");
            var detail = new GameObject("detail", typeof(RectTransform));
            try
            {
                var animator = root.AddComponent<RecommendationMinigameAnimator>();
                SetPrivate(animator, "_bookDetailRoot", detail.GetComponent<RectTransform>());

                Assert.DoesNotThrow(() => animator.ShowBookDetail());
                Assert.DoesNotThrow(() => animator.HideBookDetail());
                Assert.DoesNotThrow(() => animator.ShowBookDetail());
                Assert.DoesNotThrow(() => animator.HideBookDetailInstant());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(detail);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlayResult_DisablesFinishButtonImmediately()
        {
            var root = new GameObject("animator");
            var result = new GameObject("result", typeof(RectTransform));
            var button = new GameObject("finish", typeof(RectTransform), typeof(Button));
            try
            {
                var animator = root.AddComponent<RecommendationMinigameAnimator>();
                SetPrivate(animator, "_resultRoot", result.GetComponent<RectTransform>());
                SetPrivate(animator, "_finishButton", button.GetComponent<Button>());

                animator.PlayResult("done");

                Assert.IsFalse(button.GetComponent<Button>().interactable);
                Assert.IsFalse(button.GetComponent<CanvasGroup>().blocksRaycasts);
            }
            finally
            {
                root.GetComponent<RecommendationMinigameAnimator>()?.KillAll();
                UnityEngine.Object.DestroyImmediate(button);
                UnityEngine.Object.DestroyImmediate(result);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void SetPrivate<T>(RecommendationMinigameAnimator animator, string fieldName, T value)
        {
            typeof(RecommendationMinigameAnimator)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(animator, value);
        }
    }
}
