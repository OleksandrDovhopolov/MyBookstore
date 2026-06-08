namespace Game.Commands {
    public interface IProgressQueueCommand : IQueueCommand {
        void AddProgress(IProgressCommand cmd, IProgressSettings settings = null);
        void CleanUp();
    }
}
