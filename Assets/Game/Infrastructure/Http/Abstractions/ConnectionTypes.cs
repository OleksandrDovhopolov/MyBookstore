namespace Game.Http {
    public enum ConnectionCheckBehaviour {
        SilentWithComplete,
        ErrorLogsWithComplete,
        SilentWithRetry,
        NoInternetSignalWithRetry,
        NoInternetSignalOnce
    }

    public enum RequestStates {
        Processing,
        Finished,
        Error,
        Aborted,
        ConnectionTimedOut,
        TimedOut
    }

    public enum HTTPMethods {
        Get,
        Post
    }
}
