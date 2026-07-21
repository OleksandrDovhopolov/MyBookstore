using System.Collections.Generic;

namespace Game.Tutorial.API
{
    public interface ITutorialSettings
    {
        IEnumerable<string> ConfiguredSequenceIds { get; }

        bool IsEnabled(string sequenceId);
    }
}
