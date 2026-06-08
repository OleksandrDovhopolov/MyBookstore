#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Save
{
    public class SaveFolderMenu
    {
    
        [MenuItem("Tools/Save/Open Persistent Folder")]
        private static void Open() => EditorUtility.RevealInFinder(Application.persistentDataPath);

        [MenuItem("Tools/Save/Delete Save Files")]
        private static void Wipe()
        {
            if (!EditorUtility.DisplayDialog("Delete save?", "This wipes bookstore_save.json + .bak", "Delete", "Cancel")) return;
            var dir = Application.persistentDataPath;
            foreach (var f in new[] { "bookstore_save.json", "bookstore_save.json.bak", "bookstore_save.json.tmp" })
            {
                var p = System.IO.Path.Combine(dir, f);
                if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
            }
            Debug.Log("[Save] wiped");
        }
    }
}
#endif