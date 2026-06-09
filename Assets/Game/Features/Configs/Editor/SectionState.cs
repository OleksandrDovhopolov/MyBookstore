using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Game.Configs.Editor
{
    /// <summary>UI-состояние окна (§11 спеки).</summary>
    internal enum EditorWindowState
    {
        Disconnected,
        Idle,
        Loading,
        Loaded,
        Dirty,
        Publishing,
        Conflict,
        Error,
        Empty
    }

    /// <summary>
    /// Данные одной открытой секции в окне (§5 спеки). Минимизация: храним сериализованный
    /// pulled snapshot для сравнения с working, и parsed working для редактирования / item list.
    /// </summary>
    internal sealed class SectionState
    {
        public string Section = "books";
        public string Environment = "dev";

        public long CurrentVersion;
        public string CurrentEtag;          // canonical (no quotes)
        public string PulledSnapshotJson;   // serialized array, как отдал сервер
        public JArray WorkingArray = new(); // parsed working
        public string SelectedItemId;
        public string PublishComment = string.Empty;

        public EditorWindowState State = EditorWindowState.Idle;
        public string LastError;
        public string LastOperationResult;

        /// <summary>Dirty = working != pulled (по нормализованной сериализации).</summary>
        public bool IsDirty
        {
            get
            {
                if (PulledSnapshotJson == null)
                    return WorkingArray != null && WorkingArray.Count > 0;
                return Serialize(WorkingArray) != Normalize(PulledSnapshotJson);
            }
        }

        public bool IsEmpty => CurrentEtag == null && CurrentVersion == 0;

        public IEnumerable<JObject> Items
        {
            get
            {
                foreach (var t in WorkingArray)
                    if (t is JObject o)
                        yield return o;
            }
        }

        public void ApplyPulled(AdminConfigDto dto, string etag)
        {
            CurrentVersion = dto?.Version ?? 0;
            CurrentEtag = etag;
            var arr = dto?.Json ?? new JArray();
            PulledSnapshotJson = arr.ToString(Newtonsoft.Json.Formatting.None);
            WorkingArray = (JArray)arr.DeepClone();
            SelectedItemId = null;
        }

        public void MarkEmpty()
        {
            CurrentVersion = 0;
            CurrentEtag = null;
            PulledSnapshotJson = null;
            WorkingArray = new JArray();
            SelectedItemId = null;
        }

        public string SerializeWorking(Newtonsoft.Json.Formatting f = Newtonsoft.Json.Formatting.None)
            => WorkingArray.ToString(f);

        private static string Serialize(JArray a)
            => a == null ? "[]" : a.ToString(Newtonsoft.Json.Formatting.None);

        private static string Normalize(string serialized)
        {
            try { return JArray.Parse(serialized).ToString(Newtonsoft.Json.Formatting.None); }
            catch { return serialized; }
        }
    }
}
