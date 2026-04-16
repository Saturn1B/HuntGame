using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace HuntingGame.Item
{
#if UNITY_EDITOR
    [CustomEditor(typeof(ItemBehaviour))]
    public class ItemBehaviourEditor : OdinEditor
    {
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			ItemBehaviour item = (ItemBehaviour)target;

            if (item.itemData != null) return;

			EditorGUILayout.Space(10);
			EditorGUILayout.LabelField("Item Setup Tools", EditorStyles.boldLabel);

			EditorGUILayout.Space(5);

			GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
			if (GUILayout.Button("Register Item (Prefab + ScriptableObject)", GUILayout.Height(40)))
			{
				RunFullRegistrationFlow(item);
			}
			GUI.backgroundColor = Color.white;
		}

		// ---------------------------------------------------------------

        private void RunFullRegistrationFlow(ItemBehaviour item)
		{
            ItemPopup.Show((name, stackable, sprite) =>
            {
                ProcessRegistration(item, name, stackable, sprite);
            });
		}


        private void ProcessRegistration(ItemBehaviour item, string itemName, bool itemStackable, Sprite itemSprite)
        {
            Pose itemOffset = new Pose(item.transform.localPosition, item.transform.localRotation);

            // 1. Ask for all options via a popup dialog
            if (string.IsNullOrWhiteSpace(itemName))
            {
                Debug.LogWarning("[ItemEditor] Registration cancelled — no name entered.");
                return;
            }

            if (itemSprite == null)
            {
                Debug.LogWarning("[ItemEditor] Registration cancelled — no sprite entered.");
                return;
            }

            EditorUtility.SetDirty(item);

			// 3. Save as prefab — let user pick folder
			string prefabPath = PickPrefabSavePath(itemName);
			if (string.IsNullOrEmpty(prefabPath))
			{
				Debug.LogWarning("[ItemEditor] Registration cancelled — no prefab save path chosen.");
				return;
			}

			GameObject prefabAsset = SaveAsPrefab(item.gameObject, prefabPath);
			if (prefabAsset == null)
			{
				Debug.LogError("[ItemEditor] Failed to save prefab.");
				return;
			}

            prefabAsset.transform.position = Vector3.zero;
            prefabAsset.transform.rotation = Quaternion.identity;

            EditorUtility.SetDirty(prefabAsset);

            // 4. Create ScriptableObject — let user pick folder
            string soPath = PickSOSavePath(itemName);
			if (string.IsNullOrEmpty(soPath))
			{
				Debug.LogWarning("[ItemEditor] Registration cancelled — no ScriptableObject save path chosen.");
				return;
			}

			ItemScriptable createdData = CreateItemData(item, prefabAsset, itemName, itemOffset, itemStackable, itemSprite, soPath);
            if(createdData != null)
			{
                ItemBehaviour prefabBehaviour = prefabAsset.GetComponent<ItemBehaviour>();
                if(prefabBehaviour != null)
				{
                    prefabBehaviour.itemData = createdData;
                    EditorUtility.SetDirty(prefabBehaviour);
				}
			}

			AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ItemEditor] Item '{itemName}' fully registered!");
        }

        // ---------------------------------------------------------------
        // Name dialog — a simple EditorUtility input dialog isn't built
        // into Unity so we use a lightweight custom EditorWindow popup.

        private string PickPrefabSavePath(string roomName)
        {
            string folder = EditorUtility.OpenFolderPanel("Choose Prefab Save Folder", "Assets", "");
            if (string.IsNullOrEmpty(folder)) return null;

            // Convert absolute path -> relative
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
                Debug.Log($"[ItemEditor] Prefab saved at '{path}'.");
            else
                Debug.LogError($"[ItemEditor] Failed to save prefab at '{path}'.");

            return success ? prefab : null;
        }

        private ItemScriptable CreateItemData(ItemBehaviour item, GameObject prefabAsset, string itemName, Pose itemOffset, bool itemStackable, Sprite itemSprite, string soPath)
        {
            EnsureAssetDirectoryExists(Path.GetDirectoryName(soPath));

            ItemScriptable data = ScriptableObject.CreateInstance<ItemScriptable>();
            data.itemName = itemName;
            data.itemPrefab = prefabAsset;
            data.offset = itemOffset;
            data.stackable = itemStackable;
            data.itemSprite = itemSprite;

            AssetDatabase.CreateAsset(data, soPath);
            Debug.Log($"[ItemEditor] ItemScriptable SO saved at '{soPath}'");

            return data;
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
    // Lightweight popup window to capture a item data
    // ==================================================================

    public class ItemPopup : EditorWindow
    {
        private string enteredName = "";
        private bool enteredStackable = true;
        private Sprite enteredSprite = null;

        private System.Action<string, bool, Sprite> onConfirm;

        public static void Show (System.Action<string, bool, Sprite> callback)
        {
            ItemPopup window = CreateInstance<ItemPopup>();
            window.titleContent = new GUIContent("Item Setup");
            window.minSize = new Vector2(300, 260);
            window.onConfirm = callback;
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Item Name", EditorStyles.boldLabel);

            GUI.SetNextControlName("NameField");
            enteredName = EditorGUILayout.TextField(enteredName);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Item Stackable", EditorStyles.boldLabel);
            enteredStackable = EditorGUILayout.Toggle(enteredStackable);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Item Sprite", EditorStyles.boldLabel);
            enteredSprite = (Sprite)EditorGUILayout.ObjectField(enteredSprite, typeof(Sprite), false, GUILayout.Width(64), GUILayout.Height(64));

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Confirm") || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
            {
                if (!string.IsNullOrWhiteSpace(enteredName) && enteredSprite != null)
                {
                    onConfirm?.Invoke(enteredName, enteredStackable, enteredSprite);
                    Close();
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
#endif
}
