using System;

namespace Game.Commands {
    public interface IProgressCommand : ICommand {
        int CurrentPercent { get; }
        IProgressCommand AddProgressHandler(Action<IProgressCommand, int> progressHandler);
        IProgressCommand RemoveProgressHandler(Action<IProgressCommand, int> progressHandler);
    }

    public interface IProgressCommand<out T> : IProgressCommand {
        T Result { get; }
    }

    public interface IDownloadProgressCommand : IProgressCommand {
        long DownloadedBytes { get; }
        long TotalBytes { get; }
    }
}
