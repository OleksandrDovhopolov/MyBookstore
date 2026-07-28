using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations;

namespace Infrastructure
{
    [SharedBetweenAnimators]
    public class AnimatorStateObserver : StateMachineBehaviour
    {
        private readonly Dictionary<int, Dictionary<int, UniTaskCompletionSource>> _tasks = new();

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (stateInfo.normalizedTime >= 1)
            {
                TryCompleteTask(animator, stateInfo);
            }
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex,
            AnimatorControllerPlayable controller)
        {
            TryCompleteTask(animator, stateInfo);
        }

        private void TryCompleteTask(Animator animator, AnimatorStateInfo stateInfo)
        {
            var instance = animator.GetInstanceID();
            if (!_tasks.TryGetValue(instance, out var animatorTasks)) return;

            if (!animatorTasks.TryGetValue(stateInfo.tagHash, out var result)) return;

            result.TrySetResult();
            animatorTasks.Remove(stateInfo.tagHash);

            if (animatorTasks.Count == 0) _tasks.Remove(instance);
        }

        public UniTask WaitForStateComplete(string state, Animator animator)
        {
            var instance = animator.GetInstanceID();

            if (!_tasks.TryGetValue(instance, out var animatorTasks))
            {
                animatorTasks = new Dictionary<int, UniTaskCompletionSource>();
                _tasks[instance] = animatorTasks;
            }

            var hash = Animator.StringToHash(state);

            if (!animatorTasks.TryGetValue(hash, out var result))
            {
                result = new UniTaskCompletionSource();
                animatorTasks[hash] = result;
            }

            return result.Task;
        }

        public void ClearObserver(Animator animator)
        {
            var instance = animator.GetInstanceID();

            if (!_tasks.TryGetValue(instance, out var animatorTasks)) return;

            foreach (var task in animatorTasks.Values)
            {
                task.TrySetResult();
            }

            _tasks.Remove(instance);
        }
    }
}