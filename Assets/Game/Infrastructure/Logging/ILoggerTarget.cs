namespace Game.Logging
{
    public interface ILoggerTarget
    {
        string Name { get; }
        void Write(in LogEntry entry);
    }
}
