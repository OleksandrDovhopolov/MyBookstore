using System;
using System.Collections.Generic;
using Game.Tutorial.API;
using UnityEngine;

namespace Game.Tutorial.Presentation
{
    [CreateAssetMenu(fileName = "TutorialSettings", menuName = "Game/Tutorial/Tutorial Settings")]
    public sealed class TutorialSettings : ScriptableObject, ITutorialSettings
    {
        [SerializeField] private List<Entry> _sequences = new();

        public IEnumerable<string> ConfiguredSequenceIds
        {
            get
            {
                foreach (var entry in _sequences)
                    yield return entry?.Id;
            }
        }

        public bool IsEnabled(string sequenceId)
        {
            if (string.IsNullOrEmpty(sequenceId))
                return true;

            foreach (var entry in _sequences)
            {
                if (entry == null)
                    continue;

                if (string.Equals(entry.Id, sequenceId, StringComparison.Ordinal))
                    return entry.Enabled;
            }

            return true;
        }

        public void ValidateAgainst(IEnumerable<string> knownSequenceIds)
        {
            var known = new HashSet<string>(knownSequenceIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in _sequences)
            {
                var id = entry?.Id;
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"{TutorialLog.Prefix} tutorial settings contain an empty sequence id.");
                    continue;
                }

                if (!known.Contains(id))
                    Debug.LogWarning($"{TutorialLog.Prefix} tutorial settings contain unknown sequence id '{id}'.");

                if (!seen.Add(id))
                    Debug.LogWarning($"{TutorialLog.Prefix} tutorial settings contain duplicate sequence id '{id}'; first entry wins.");
            }
        }

        public static TutorialSettings CreateDefault()
            => CreateInstance<TutorialSettings>();

        public static TutorialSettings CreateWithEntries(params Entry[] entries)
        {
            var settings = CreateDefault();
            if (entries != null)
                settings._sequences.AddRange(entries);
            return settings;
        }

        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string _id;
            [SerializeField] private bool _enabled = true;

            public Entry()
            {
            }

            public Entry(string id, bool enabled = true)
            {
                _id = id;
                _enabled = enabled;
            }

            public string Id => _id;
            public bool Enabled => _enabled;
        }
    }
}
