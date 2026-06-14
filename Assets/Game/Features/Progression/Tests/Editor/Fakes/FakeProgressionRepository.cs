using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Progression.API;

namespace Game.Progression.Tests.Editor.Fakes
{
    public sealed class FakeProgressionRepository : IProgressionRepository
    {
        public ProgressionStateDto Stored { get; set; } = new();
        public int SaveCallCount { get; private set; }

        public UniTask<ProgressionStateDto> LoadAsync(CancellationToken ct)
            => UniTask.FromResult(new ProgressionStateDto { Reputation = Stored.Reputation });

        public UniTask SaveAsync(ProgressionStateDto state, CancellationToken ct)
        {
            Stored = new ProgressionStateDto { Reputation = state?.Reputation ?? 0 };
            SaveCallCount++;
            return UniTask.CompletedTask;
        }
    }
}
