namespace Game.Commands {
    /// <summary>
    /// Запускает вложенную команду «fire-and-forget» и сразу завершает себя,
    /// не дожидаясь результата вложенной команды.
    /// </summary>
    public class NotWaitWrapperCommand : AbstractFakeProgressCommand {
        private readonly IProgressCommand _cmd;

        public NotWaitWrapperCommand(IProgressCommand cmd, ICommandLogger logger, ICommandErrorReporter errorReporter)
            : base(logger, errorReporter) {
            _cmd = cmd;
        }

        protected override void ExecInternal() {
            _cmd.Execute();
            NotifyComplete();
        }
    }
}
