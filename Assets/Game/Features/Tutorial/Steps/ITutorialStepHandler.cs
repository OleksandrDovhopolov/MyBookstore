using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;

namespace Game.Tutorial.Steps
{
    /// <summary>
    /// Executes one tutorial step type. One handler per <see cref="TutorialStepConfig.Type"/> discriminator;
    /// resolved through <see cref="TutorialStepHandlerRegistry"/>. Returns when the step's advance condition
    /// is met. Pass 1 handlers are stubs (log + return immediately); real gating/await lands next pass.
    /// </summary>
    public interface ITutorialStepHandler
    {
        /// <summary>Discriminator matched against <see cref="TutorialStepConfig.Type"/> (case-insensitive).</summary>
        string Type { get; }

        UniTask ExecuteAsync(TutorialStepConfig step, CancellationToken ct);
    }
}
