using System;
using System.Collections.Generic;

namespace Game.Commands {
    public interface IQueueCommand : IProgressCommand {
        ICommand CompletedCommand { get; }

        ICommand RunningCommand { get; }

        bool IsContains(ICommand cmd);
        void Add(ICommand c);
        void Add(IEnumerable<ICommand> list);
        void Add(params ICommand[] list);
        void AddCommandCompleteHandler(Action<IQueueCommand> completeHandler);

        void RetryCompletedCommand();
        void ContinueExecute();

        void SetExecuteMode(QueueExecuteMode mode);
    }
}
