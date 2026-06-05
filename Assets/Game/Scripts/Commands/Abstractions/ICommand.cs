using System;
using Cysharp.Threading.Tasks;

namespace Game.Commands {
    /// <summary>
    /// Базовая единица работы: асинхронная операция с состоянием, результатом, ошибкой и ретраями.
    /// Используется как единый контракт во всех системах/фичах проекта.
    /// </summary>
    public interface ICommand {
        CommandState State { get; }
        bool IsExecuting { get; }
        bool IsSucceed { get; }
        bool HasResult { get; }
        bool HasError { get; }

        CommandError Error { get; }

        float ExecuteTime { get; }

        ICommand AddCompleteHandler(Action<ICommand> completeHandler);
        void RemoveCompleteHandler(Action<ICommand> completeHandler);

        ICommand AddSucceedHandler(Action<ICommand> succeedHandler);
        void RemoveSucceedHandler(Action<ICommand> succeedHandler);

        void Execute();
        UniTask<ICommand> ExecuteAsync();
        void Terminate();
        void Reset();
        void Retry();

        ICommand SetTimeout(int milliseconds);
        string ToString();
    }
}
