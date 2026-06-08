using Game.Commands;

namespace Game.Http {
    public static class ConnectionCommandsErrors {
        public static readonly CommandError NullEndpointError = new CommandError($"{nameof(NullEndpointError)}");

        public static readonly CommandError HttpError = new CommandError($"{nameof(HttpError)}");
        public static readonly CommandError NotFoundError = new CommandError($"{nameof(NotFoundError)}");
        public static readonly CommandError RequestTimeoutError = new CommandError($"{nameof(RequestTimeoutError)}");
        public static readonly CommandError ConnectionTimeoutError = new CommandError($"{nameof(ConnectionTimeoutError)}");
        public static readonly CommandError RequestError = new CommandError($"{nameof(RequestError)}");
        public static readonly CommandError ServerError = new CommandError($"{nameof(ServerError)}");

        public static readonly CommandError Forbidden = new CommandError($"{nameof(Forbidden)}");
        public static readonly CommandError ServiceUnavailable = new CommandError($"{nameof(ServiceUnavailable)}");

        public static readonly CommandError RequestAbortedError = new CommandError($"{nameof(RequestAbortedError)}");

        public static readonly CommandError WrongResponseError = new CommandError($"{nameof(WrongResponseError)}");
        public static readonly CommandError ParseError = new CommandError($"{nameof(ParseError)}");

        public static bool IsPossibleNetworkError(CommandError error) {
            return error == HttpError
                || IsTimeoutError(error)
                || IsProxyAccessError(error)
                || error == BaseCommandsErrors.NoInternetError;
        }

        public static bool IsTimeoutError(CommandError error) {
            return error == RequestTimeoutError
                || error == ConnectionTimeoutError;
        }

        public static bool IsProxyAccessError(CommandError error) {
            return error == ServiceUnavailable
                || error == Forbidden;
        }
    }
}
