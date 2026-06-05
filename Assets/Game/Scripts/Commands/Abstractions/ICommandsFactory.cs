namespace Game.Commands {
    public interface ICommandsFactory {
        IQueueCommand GetQueueCommand(string name, CommandFailBehaviour behaviour = CommandFailBehaviour.Continue);
        IQueueCommand GetQueueCommand(params ICommand[] commands);
        IProgressQueueCommand GetProgressQueueCommand(string name, CommandFailBehaviour behaviour = CommandFailBehaviour.Continue);
        IProgressQueueCommand GetProgressQueueCommand(string name, params ICommand[] commands);
    }
}
