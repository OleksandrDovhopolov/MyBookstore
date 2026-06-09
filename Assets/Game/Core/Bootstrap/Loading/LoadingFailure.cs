using System;

namespace Game.Bootstrap.Loading
{
    public sealed class LoadingFailure
    {
        public string PhaseId { get; }
        public string GroupId { get; }
        public string OperationId { get; }
        public int Attempt { get; }
        public bool TimedOut { get; }
        public bool IsCritical { get; }
        public Exception Exception { get; }

        public LoadingFailure(
            string phaseId,
            string groupId,
            string operationId,
            int attempt,
            bool timedOut,
            bool isCritical,
            Exception exception)
        {
            PhaseId = phaseId;
            GroupId = groupId;
            OperationId = operationId;
            Attempt = attempt;
            TimedOut = timedOut;
            IsCritical = isCritical;
            Exception = exception;
        }
    }
}
