using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Bootstrap.Loading
{
    public sealed class LoadingGroup
    {
        public string Id { get; }
        public LoadingGroupExecutionMode ExecutionMode { get; }
        public IReadOnlyList<ILoadingOperation> Operations { get; }

        public LoadingGroup(string id, LoadingGroupExecutionMode executionMode, IEnumerable<ILoadingOperation> operations)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Group id is required.", nameof(id)) : id;
            ExecutionMode = executionMode;
            Operations = operations?.ToArray() ?? throw new ArgumentNullException(nameof(operations));
        }
    }
}
