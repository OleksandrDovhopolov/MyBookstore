using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using UnityEngine.SceneManagement;

namespace Game.DayCycle.Tests.Editor.Fakes
{
    /// <summary>Records scene transition requests; does not actually load anything.</summary>
    public sealed class FakeSceneTransitionService : ISceneTransitionService
    {
        public int TransitionCount { get; private set; }
        public int AdditiveLoadCount { get; private set; }
        public int UnloadCount { get; private set; }
        public string LastSceneName { get; private set; }

        public UniTask TransitionToAsync(string sceneName, IProgress<float> progress, CancellationToken ct)
        {
            TransitionCount++;
            LastSceneName = sceneName;
            return UniTask.CompletedTask;
        }

        public UniTask<Scene> LoadAdditiveAsync(string sceneName, bool makeActive, IProgress<float> progress, CancellationToken ct)
        {
            AdditiveLoadCount++;
            LastSceneName = sceneName;
            return UniTask.FromResult(default(Scene));
        }

        public UniTask UnloadAsync(string sceneName, CancellationToken ct)
        {
            UnloadCount++;
            return UniTask.CompletedTask;
        }

        public void SetActiveScene(string sceneName)
        {
        }
    }
}
