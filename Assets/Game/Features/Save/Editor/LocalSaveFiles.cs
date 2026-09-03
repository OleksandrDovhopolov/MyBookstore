using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Save.Editor
{
    /// <summary>
    /// The on-disk save cache written by LocalDiskStorage, plus its .bak/.tmp siblings.
    /// Shared by Tools/Save/Delete Save Files and by the server reset window, so the two can never
    /// drift on which files count as "the local save".
    /// </summary>
    public static class LocalSaveFiles
    {
        public static readonly string[] FileNames =
        {
            "bookstore_save.json",
            "bookstore_save.json.bak",
            "bookstore_save.json.tmp"
        };

        /// <summary>Deletes every local save file that exists. Returns the ones actually removed.</summary>
        public static IReadOnlyList<string> DeleteAll()
        {
            var deleted = new List<string>();
            var dir = Application.persistentDataPath;

            foreach (var fileName in FileNames)
            {
                var path = Path.Combine(dir, fileName);
                if (!File.Exists(path))
                {
                    continue;
                }

                File.Delete(path);
                deleted.Add(fileName);
            }

            return deleted;
        }
    }
}
