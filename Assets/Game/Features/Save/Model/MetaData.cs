namespace Save.Model
{
    public sealed class MetaData
    {
        public int SchemaVersion { get; set; } = 1;
        public long Revision { get; set; }
        public string Hash { get; set; } = "";
        public long TimestampUtcMs { get; set; }
    }
}
