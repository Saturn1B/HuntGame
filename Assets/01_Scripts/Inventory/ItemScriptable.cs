using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "ItemScriptable", menuName = "Scriptable Objects/ItemScriptable")]
public class ItemScriptable : ScriptableObject
{
	[Header("Item Setup")]
	public bool stackable;
	public string itemName;
	[PreviewField]
	public Sprite itemSprite;
	[PreviewField]
	public GameObject itemPrefab;
}
