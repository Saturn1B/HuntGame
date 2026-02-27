using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace ProceduralGeneration
{
#if UNITY_EDITOR
    [CustomEditor(typeof(Room))]
    public class RoomEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            Room room = (Room)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Room Setup Tools", EditorStyles.boldLabel);

            // Fetch Sockets button
            if (GUILayout.Button("Fetch All Socket Children", GUILayout.Height(30)))
            {
                FetchSockets(room);
            }

            EditorGUILayout.Space(5);

            // The big one — full registration flow
            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
            if (GUILayout.Button("Register Room (Prefab + ScriptableObject)", GUILayout.Height(40)))
            {
                RunFullRegistrationFlow(room);
            }
            GUI.backgroundColor = Color.white;
        }

        // ---------------------------------------------------------------

        private void FetchSockets(Room room)
        {
            Undo.RecordObject(room, "Fetch Sockets");

            // Gather every Socket in children (excluding self if Room somehow also has Socket)
            Socket[] found = room.GetComponentsInChildren<Socket>(includeInactive: true);

            room.sockets.Clear();

            foreach (Socket s in found)
            {
                Undo.RecordObject(s, "Assign Room Reference");
                s.room = room;
                room.sockets.Add(s);
                EditorUtility.SetDirty(s);
            }

            EditorUtility.SetDirty(room);
            Debug.Log($"[RoomEditor] Fetched {found.Length} socket(s) on '{room.name}'.");
        }

        // ---------------------------------------------------------------

        private void RunFullRegistrationFlow(Room room)
        {
            // 1. Always re-fetch sockets first so everything is up to date
            FetchSockets(room);

            // 2. Ask for a name and weight via a popup dialog
            var (roomName, roomWeight) = RoomNamePopup.Show();
            if (string.IsNullOrWhiteSpace(roomName))
            {
                Debug.LogWarning("[RoomEditor] Registration cancelled — no name entered.");
                return;
            }

            room.roomName = roomName;
            EditorUtility.SetDirty(room);

            // 3. Save as prefab — let user pick folder
            string prefabPath = PickPrefabSavePath(roomName);
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogWarning("[RoomEditor] Registration cancelled — no prefab save path chosen.");
                return;
            }

            GameObject prefabAsset = SaveAsPrefab(room.gameObject, prefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError("[RoomEditor] Failed to save prefab.");
                return;
            }

            // 4. Create ScriptableObject — let user pick folder
            string soPath = PickSOSavePath(roomName);
            if (string.IsNullOrEmpty(soPath))
            {
                Debug.LogWarning("[RoomEditor] Registration cancelled — no ScriptableObject save path chosen.");
                return;
            }

            CreateRoomData(room, prefabAsset, roomName, roomWeight, soPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[RoomEditor] Room '{roomName}' fully registered!");
        }

        // ---------------------------------------------------------------
        // Name dialog — a simple EditorUtility input dialog isn't built
        // into Unity so we use a lightweight custom EditorWindow popup.

        private string PickPrefabSavePath(string roomName)
        {
            string folder = EditorUtility.OpenFolderPanel("Choose Prefab Save Folder", "Assets", "");
            if (string.IsNullOrEmpty(folder)) return null;

            // Convert absolute path → relative
            folder = MakeRelative(folder);
            return $"{folder}/{roomName}.prefab";
        }

        private string PickSOSavePath(string roomName)
        {
            string folder = EditorUtility.OpenFolderPanel("Choose RoomData SO Save Folder", "Assets", "");
            if (string.IsNullOrEmpty(folder)) return null;

            folder = MakeRelative(folder);
            return $"{folder}/{roomName}_Data.asset";
        }

        // ---------------------------------------------------------------

        private GameObject SaveAsPrefab(GameObject go, string path)
        {
            EnsureAssetDirectoryExists(Path.GetDirectoryName(path));

            bool success;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out success);

            if (success)
                Debug.Log($"[RoomEditor] Prefab saved at '{path}'.");
            else
                Debug.LogError($"[RoomEditor] Failed to save prefab at '{path}'.");

            return success ? prefab : null;
        }

        private void CreateRoomData(Room room, GameObject prefabAsset, string roomName, int roomWeight, string soPath)
        {
            EnsureAssetDirectoryExists(Path.GetDirectoryName(soPath));

            RoomData data = ScriptableObject.CreateInstance<RoomData>();
            data.roomName = roomName;
            data.roomPrefab = prefabAsset;
            data.roomWeight = roomWeight;

            data.socketTypes = room.sockets
                .Select(s => s.socketType)
                .Distinct()
                .ToList();

            AssetDatabase.CreateAsset(data, soPath);
            Debug.Log($"[RoomEditor] RoomData SO saved at '{soPath}' with {data.socketTypes.Count} socket type(s).");
        }

        // ---------------------------------------------------------------

        private string MakeRelative(string absolutePath)
        {
            // Normalize both paths to forward slashes to avoid Windows separator issues
            string fullProject = Path.GetFullPath(Application.dataPath + "/..").Replace("\\", "/");
            string normalized = absolutePath.Replace("\\", "/");

            if (normalized.StartsWith(fullProject))
                normalized = normalized.Substring(fullProject.Length).TrimStart('/');

            return normalized;
        }

        /// <summary>
        /// Ensures every segment of a Unity-relative folder path exists,
        /// creating missing folders via AssetDatabase so they're properly tracked.
        /// </summary>
        private void EnsureAssetDirectoryExists(string relativeFolderPath)
        {
            relativeFolderPath = relativeFolderPath.Replace("\\", "/").TrimEnd('/');

            if (AssetDatabase.IsValidFolder(relativeFolderPath)) return;

            string[] parts = relativeFolderPath.Split('/');
            string current = parts[0]; // starts with "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                    AssetDatabase.Refresh();
                }
                current = next;
            }
        }
    }

    // ==================================================================
    // Lightweight popup window to capture a room name
    // ==================================================================

    public class RoomNamePopup : EditorWindow
    {
        private string enteredName = "";
        private int enteredWeight = 1;
        private bool confirmed = false;
        private bool focusSet = false;
        private static RoomNamePopup instance;

        public static (string name, int weight) Show()
        {
            instance = CreateInstance<RoomNamePopup>();
            instance.titleContent = new GUIContent("Room Setup");
            instance.minSize = new Vector2(320, 130);
            instance.maxSize = new Vector2(320, 130);
            instance.ShowModalUtility();

            return instance.confirmed
                ? (instance.enteredName, instance.enteredWeight)
                : (null, 0);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Room Name", EditorStyles.boldLabel);

            GUI.SetNextControlName("NameField");
            enteredName = EditorGUILayout.TextField(enteredName);
            if (!focusSet)
            {
                EditorGUI.FocusTextInControl("NameField");
                focusSet = true;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Room Weight", EditorStyles.boldLabel);
            enteredWeight = Mathf.Max(1, EditorGUILayout.IntField(enteredWeight));

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Confirm") || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
            {
                if (!string.IsNullOrWhiteSpace(enteredName))
                {
                    confirmed = true;
                    Close();
                }
            }

            if (GUILayout.Button("Cancel"))
            {
                confirmed = false;
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
#endif
}
