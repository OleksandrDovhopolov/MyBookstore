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
        public int ContentRevision { get; private set; }
        public int SelectedItemIndex = -1;  // -1 = nothing selected
        public string PublishComment = string.Empty;

        public JObject SelectedItem
            => SelectedItemIndex >= 0 && SelectedItemIndex < WorkingArray.Count
                ? WorkingArray[SelectedItemIndex] as JObject
                : null;

        public EditorWindowState State = EditorWindowState.Idle;
        public string LastError;
        public string LastOperationResult;

        /// <summary>Dirty tracks intentional UI mutations; avoid serializing large sections every repaint.</summary>
        public bool IsDirty { get; private set; }

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
            SelectedItemIndex = -1;
            IsDirty = false;
            ContentRevision++;
        }

        public void MarkEmpty()
        {
            CurrentVersion = 0;
            CurrentEtag = null;
            PulledSnapshotJson = null;
            WorkingArray = new JArray();
            SelectedItemIndex = -1;
            IsDirty = false;
            ContentRevision++;
        }

        public void ReplaceWorking(JArray array, bool dirty)
        {
            WorkingArray = array ?? new JArray();
            SelectedItemIndex = -1;
            IsDirty = dirty;
            ContentRevision++;
        }

        public void MarkDirty()
        {
            IsDirty = true;
            ContentRevision++;
        }

        public string SerializeWorking(Newtonsoft.Json.Formatting f = Newtonsoft.Json.Formatting.None)
            => WorkingArray.ToString(f);

    }
}
