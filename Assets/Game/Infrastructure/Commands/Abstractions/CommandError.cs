using System;

namespace Game.Commands {
    public readonly struct CommandError {
        public string Code { get; }
        public string Message { get; }
        public Exception Exception { get; }

        public CommandError(string code, string message = null, Exception exception = null) {
            Code = code;
            Message = message;
            Exception = exception;
        }

        public CommandError(CommandError err, string message = null) : this(err.Code, message) {}
        public CommandError(CommandError err, Exception exception) : this(err.Code, null, exception) {}

        public static bool operator ==(CommandError c1, CommandError c2) {
            return c1.Equals(c2);
        }

        public static bool operator !=(CommandError c1, CommandError c2) {
            return !c1.Equals(c2);
        }

        public bool Equals(CommandError other) {
            return Code == other.Code;
        }

        public override bool Equals(object obj) => obj is CommandError other && Equals(other);

        public override int GetHashCode() => Code != null ? Code.GetHashCode() : 0;

        public override string ToString() {
            return $"{Code} {Message ?? Exception?.ToString()}";
        }
    }
}
