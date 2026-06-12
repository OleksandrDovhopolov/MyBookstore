using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;

namespace Game.DayCycle.Tests.Editor.Fakes
{
    /// <summary>Records scene transition requests; does not actually load anything.</summary>
    public sealed class FakeSceneTransitionService : ISceneTransitionService
    {
        public int TransitionCount { get; private set; }
        public string LastSceneName { get; private set; }

        public UniTask TransitionToAsync(string sceneName, IProgress<float> progress, CancellationToken ct)
        {
            TransitionCount++;
            LastSceneName = sceneName;
            return UniTask.CompletedTask;
        }
    }
}
