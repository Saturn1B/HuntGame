using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace HuntingGame.ProceduralGeneration
{
#if UNITY_EDITOR
	[CustomEditor(typeof(Socket))]
	public class DoorEditor : OdinEditor
	{
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            Socket socket = (Socket)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Socket Setup Tools", EditorStyles.boldLabel);

			// Fetch Bounds button
			if (GUILayout.Button("Fetch Socket Bounds", GUILayout.Height(30)))
			{
				FetchBound(socket);
			}

			EditorGUILayout.Space(5);

            // Place Socket button
            if (GUILayout.Button("Place Socket", GUILayout.Height(30)))
            {
                PlaceSocket(socket);
            }

            EditorGUILayout.Space(5);

            // Full registration flow
            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
            if (GUILayout.Button("Register Socket Prefab", GUILayout.Height(40)))
            {
                RunFullRegistrationFlow(socket);
            }
            GUI.backgroundColor = Color.white;
        }

		// ---------------------------------------------------------------

		private void FetchBound(Socket socket)
		{
			Undo.RecordObject(socket, "Fetch Bound");

			BoxCollider collider = socket.GetComponent<BoxCollider>();

			MeshRenderer[] renderers = socket.GetComponentsInChildren<MeshRenderer>();

			if (renderers.Length == 0)
			{
				collider.center = Vector3.zero;
				collider.size = Vector3.one;
				return;
			}

			Bounds bounds = renderers[0].bounds;

			for (int i = 1; i < renderers.Length; i++)
			{
				bounds.Encapsulate(renderers[i].bounds);
			}

			Vector3 localCenter = socket.transform.InverseTransformPoint(bounds.center);
			Vector3 localSize = socket.transform.InverseTransformVector(bounds.size);

			collider.center = localCenter;
			collider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

			EditorUtility.SetDirty(socket);
			Debug.Log($"[SocketEditor] Fetched bounds on '{socket.name}'.");
		}

        // ---------------------------------------------------------------

        private void PlaceSocket(Socket socket)
        {
            Undo.RecordObject(socket, "Place Socket");

            if (socket.socket == null) return;

            BoxCollider collider = socket.GetComponent<BoxCollider>();

            if (collider == null) return;

            socket.socket.transform.position = new Vector3(collider.center.x, collider.center.y, collider.center.z + collider.size.z / 2);

            EditorUtility.SetDirty(socket);
            Debug.Log($"[SocketEditor] Placed socket.");
        }

        // ---------------------------------------------------------------

        private void RunFullRegistrationFlow(Socket socket)
        {
            // 2. Ask for a name and weight via a popup dialog
            string socketName = SocketNamePopup.Show();
            if (string.IsNullOrWhiteSpace(socketName))
            {
                Debug.LogWarning("[SocketEditor] Registration cancelled — no name entered.");
                return;
            }

            // 3. Save as prefab — let user pick folder
            string prefabPath = PickPrefabSavePath(socketName);
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogWarning("[RoomEditor] Registration cancelled — no prefab save path chosen.");
                return;
            }

            GameObject prefabAsset = SaveAsPrefab(socket.gameObject, prefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError("[RoomEditor] Failed to save prefab.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SocketEditor] Socket '{socketName}' fully registered!");
        }

        // ---------------------------------------------------------------

        private string PickPrefabSavePath(string roomName)
        {
            string folder = EditorUtility.OpenFolderPanel("Choose Prefab Save Folder", "Assets", "");
            if (string.IsNullOrEmpty(folder)) return null;

            // Convert absolute path -> relative
            folder = MakeRelative(folder);
            return $"{folder}/{roomName}.prefab";
        }

        // ---------------------------------------------------------------

        private GameObject SaveAsPrefab(GameObject go, string path)
        {
            EnsureAssetDirectoryExists(Path.GetDirectoryName(path));

            bool success;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out success);

            if (success)
                Debug.Log($"[SocketEditor] Prefab saved at '{path}'.");
            else
                Debug.LogError($"[SocketEditor] Failed to save prefab at '{path}'.");

            return success ? prefab : null;
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

    public class SocketNamePopup : EditorWindow
    {
        private string enteredName = "";
        private bool confirmed = false;
        private bool focusSet = false;
        private static SocketNamePopup instance;

        public static string Show()
        {
            instance = CreateInstance<SocketNamePopup>();
            instance.titleContent = new GUIContent("Socket Setup");
            instance.minSize = new Vector2(320, 130);
            instance.maxSize = new Vector2(320, 130);
            instance.ShowModalUtility();

            return instance.confirmed
                ? instance.enteredName
                : null;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Socket Name", EditorStyles.boldLabel);

            GUI.SetNextControlName("NameField");
            enteredName = EditorGUILayout.TextField(enteredName);
            if (!focusSet)
            {
                EditorGUI.FocusTextInControl("NameField");
                focusSet = true;
            }

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
