namespace Game.Commands {
    public static class BaseCommandsErrors {
        public static readonly CommandError NoError = new CommandError($"{nameof(NoError)}");
        public static readonly CommandError InternalCmdExceptionError = new CommandError($"{nameof(InternalCmdExceptionError)}");
        public static readonly CommandError CompleteEventCmdExceptionError = new CommandError($"{nameof(CompleteEventCmdExceptionError)}");
        public static readonly CommandError NoInternetError = new CommandError($"{nameof(NoInternetError)}");
        public static readonly CommandError UnknownError = new CommandError($"{nameof(UnknownError)}");
        public static readonly CommandError CommandInQueueFailedError = new CommandError($"{nameof(CommandInQueueFailedError)}");
    }
}
