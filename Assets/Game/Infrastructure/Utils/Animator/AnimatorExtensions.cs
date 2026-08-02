using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Infrastructure
{
    public static class AnimatorExtensions
    {
        public static async UniTask SetTriggerAndWait(this Animator target, string trigger)
        {
            target.SetTrigger(trigger);
            await target.WaitForStateComplete(trigger);
        }
        
        public static async UniTask WaitForStateComplete(this Animator target, string tag)
        {
            var observer = target.GetBehaviour<AnimatorStateObserver>();
            if (observer == null)
                return;

            await UniTask.Yield();
            await observer.WaitForStateComplete(tag, target);
        }

        public static void ForceFinishState(this Animator target, int layer)
        {
            target.Play(target.GetCurrentAnimatorStateInfo(layer).fullPathHash, layer, 1f); 
        }

        public static UniTask PlayAsync(this Animation target, string animationName)
        {
            target.Play(animationName);
            return UniTask.WaitWhile(() => target.isPlaying);
        }

        public static void Sample(this Animation target, string animationName, GameObject gameObject, float normalizedTime)
        {
            var clip = target.GetClip(animationName);
            clip.SampleAnimation(gameObject, clip.length * normalizedTime);
        }

        public static void ClearObserve(this Animator target)
        {
            var observer = target.GetBehaviour<AnimatorStateObserver>();
            if (observer == null)
                return;
        
            observer.ClearObserver(target);
        }
    }
}
