namespace Game.Bootstrap.Loading
{
    public sealed class LoadingRunResult
    {
        public static LoadingRunResult Success() => new(true, null, null);
        public static LoadingRunResult Failed(LoadingFailure failure, int failedPhaseIndex) => new(false, failure, failedPhaseIndex);

        public bool IsSuccess { get; }
        public LoadingFailure Failure { get; }
        public int? FailedPhaseIndex { get; }

        private LoadingRunResult(bool isSuccess, LoadingFailure failure, int? failedPhaseIndex)
        {
            IsSuccess = isSuccess;
            Failure = failure;
            FailedPhaseIndex = failedPhaseIndex;
        }
    }
}
