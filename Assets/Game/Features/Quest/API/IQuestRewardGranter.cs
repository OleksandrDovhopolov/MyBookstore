using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Rewards.API;

namespace Game.Quest.API
{
    public readonly struct QuestRewardGrantResult
    {
        public bool Success { get; }
        public bool AlreadyGranted { get; }
        public RewardSpec Granted { get; }
        public string FailureReason { get; }

        private QuestRewardGrantResult(bool success, bool alreadyGranted, RewardSpec granted, string failureReason)
        {
            Success = success;
            AlreadyGranted = alreadyGranted;
            Granted = granted;
            FailureReason = failureReason;
        }

        public static QuestRewardGrantResult Ok(RewardSpec granted) =>
            new(true, false, granted, null);

        public static QuestRewardGrantResult Duplicate() =>
            new(true, true, null, null);

        public static QuestRewardGrantResult Fail(string reason) =>
            new(false, false, null, reason);
    }

    public interface IQuestRewardGranter
    {
        bool IsGranted(string questId);
        UniTask<QuestRewardGrantResult> TryGrantAsync(string questId, CancellationToken ct);
    }
}
