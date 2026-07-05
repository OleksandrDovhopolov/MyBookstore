using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using UnityEngine;

namespace Game.Tutorial.Steps
{
    /// <summary>
    /// Pass 1 stub: logs the step and completes immediately (no gating/await yet). Real handlers replace
    /// <see cref="ExecuteAsync"/> next pass; the type wiring and registry stay the same.
    /// </summary>
    public abstract class LoggingStepHandlerBase : ITutorialStepHandler
    {
        protected const string LogPrefix = "[Tutorial]";

        public abstract string Type { get; }

        public virtual UniTask ExecuteAsync(TutorialStepConfig step, CancellationToken ct)
        {
            Debug.Log($"{LogPrefix} step '{step?.Id}' ({Type}) — stub auto-advance. text=\"{step?.Text}\"");
            return UniTask.CompletedTask;
        }
    }
}
