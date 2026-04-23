using UnityEngine;
using Sirenix.OdinInspector;

namespace HuntingGame.Item
{
	[CreateAssetMenu(fileName = "ItemScriptable", menuName = "Scriptable Objects/ItemScriptable")]
	public class ItemScriptable : ScriptableObject
	{
		[Header("Item Setup")]
		public bool stackable;
		public string itemName;
		public Pose offset;
		[PreviewField]
		public Sprite itemSprite;
		[PreviewField]
		public GameObject itemPrefab;
		[PreviewField]
		public GameObject itemVisualPrefab;
	}
}
