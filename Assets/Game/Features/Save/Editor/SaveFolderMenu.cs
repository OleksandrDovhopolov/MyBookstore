using UnityEditor;
using UnityEngine;

namespace Save.Editor
{
    public static class SaveFolderMenu
    {
        [MenuItem("Tools/Save/Open Persistent Folder")]
        private static void Open() => EditorUtility.RevealInFinder(Application.persistentDataPath);

        [MenuItem("Tools/Save/Delete Save Files")]
        private static void Wipe()
        {
            var files = string.Join(", ", LocalSaveFiles.FileNames);
            if (!EditorUtility.DisplayDialog("Delete save?", $"This wipes {files}", "Delete", "Cancel"))
            {
                return;
            }

            var deleted = LocalSaveFiles.DeleteAll();
            Debug.Log(deleted.Count == 0
                ? "[Save] no local save files to delete"
                : $"[Save] wiped {deleted.Count} local save file(s): {string.Join(", ", deleted)}");
        }
    }
}
