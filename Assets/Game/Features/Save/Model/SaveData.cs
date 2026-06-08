using System.Collections.Generic;

namespace Save.Model
{
    public sealed class SaveData
    {
        public MetaData Meta { get; set; } = new();
        public Dictionary<string, ModulePayload> Modules { get; set; } = new();
    }
}
