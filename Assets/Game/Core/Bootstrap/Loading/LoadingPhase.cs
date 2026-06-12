using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Bootstrap.Loading
{
    public sealed class LoadingPhase
    {
        public string Id { get; }
        public IReadOnlyList<LoadingGroup> Groups { get; }

        public LoadingPhase(string id, IEnumerable<LoadingGroup> groups)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Phase id is required.", nameof(id)) : id;
            Groups = groups?.ToArray() ?? throw new ArgumentNullException(nameof(groups));
        }
    }
}
