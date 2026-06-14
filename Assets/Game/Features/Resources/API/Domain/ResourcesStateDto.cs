using System.Collections.Generic;

namespace Game.Resources.API
{
    /// <summary>
    /// Transport DTO between <see cref="IResourcesService"/> and <see cref="IResourcesRepository"/>.
    /// Persisted shape is the same — a simple id→amount dictionary. Lives in the API assembly so a
    /// future server-backed repository can target it without referencing the implementation.
    /// </summary>
    public sealed class ResourcesStateDto
    {
        public Dictionary<string, int> Amounts { get; set; } = new();
    }
}
