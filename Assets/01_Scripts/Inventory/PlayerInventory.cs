using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using HuntingGame.Item;

namespace HuntingGame.Inventory
{
	[ExecuteInEditMode]
    public class PlayerInventory : MonoBehaviour
    {
		[Header("Inventory Setup")]
		[SerializeField, Range(1, 10)] private int inventorySize = 1;
		[SerializeField] private GameObject slotPrefab;
		[SerializeField] private Transform inventoryPanel;
		[SerializeField] private Color selected, unselected;
		[SerializeField] private TMP_Text itemText;
		[SerializeField] private Transform rightHand;

		[SerializeField, HideInInspector] private InventorySlot[] inventorySlots;
		private InventorySlot selectedSlot;
		private int slotIndex;

		private GameObject objectInHand;

		private SimplePlayerInteractor playerInteractor;

		private void OnValidate()
		{

#if UNITY_EDITOR
			if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;

			if (inventorySlots != null && inventorySlots.Length == inventorySize && inventoryPanel.childCount == inventorySize) return;

			UnityEditor.EditorApplication.delayCall += () =>
			{
				if (this == null && UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;

				for (int i = inventoryPanel.childCount - 1; i >= 0; i--)
				{
					if (inventoryPanel.GetChild(i) != null)
						DestroyImmediate(inventoryPanel.GetChild(i).gameObject);
				}

				InitializeInventory();

				UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(this.gameObject.scene);
			};
#endif
		}

		private void Awake()
		{
			playerInteractor = GetComponent<SimplePlayerInteractor>();
		}

		private void Start()
		{
			if (inventorySlots != null && inventorySlots.Length == inventorySize && inventoryPanel.childCount > 0)
			{
#if UNITY_EDITOR
				if(UnityEditor.EditorApplication.isPlaying)
					SetSelectedSlot(slotIndex);
#else
				SetSelectedSlot(slotIndex);
#endif
				return;
			}
			InitializeInventory();
		}

		void InitializeInventory()
		{
			inventorySlots = new InventorySlot[inventorySize];

			for (int i = 0; i < inventorySize; i++)
			{
				InventorySlot slot = Instantiate(slotPrefab, inventoryPanel).GetComponent<InventorySlot>();
				inventorySlots[i] = slot;
				slot.Setup(this);
			}

			slotIndex = 0;
			SetSelectedSlot(slotIndex);
		}

		public void ScrollSlot(int value)
		{
			if (value == 0) return;

			if (value < 0)
				value = value + inventorySlots.Length;
			slotIndex = (slotIndex + value) % inventorySlots.Length;
			SetSelectedSlot(slotIndex);
		}

		private void SetSelectedSlot(int slotIndex)
		{
			RemoveObjectInHand();

			for (int i = 0; i < inventorySlots.Length; i++)
			{
				if (i == slotIndex)
				{
					selectedSlot = inventorySlots[i];
					selectedSlot.transform.GetComponent<Image>().color = selected;
				}
				else
					inventorySlots[i].transform.GetComponent<Image>().color = unselected;
			}
			if (selectedSlot.currentItem != null)
			{
				StopAllCoroutines();
				StartCoroutine(ShowItemName(selectedSlot.currentItem.itemName));
				if (selectedSlot.currentItem.itemPrefab)
				{
					//Add Item in hand
					GameObject obj = Instantiate(selectedSlot.currentItem.itemPrefab, rightHand);
					obj.transform.localPosition = selectedSlot.currentItem.offset.position;
					obj.transform.localRotation = selectedSlot.currentItem.offset.rotation;

					objectInHand = obj;
					if (objectInHand.TryGetComponent(out ISimpleUseable useable))
						playerInteractor.SetUseable(useable);
				}
				else
				{
					//Remove Item from hand
					RemoveObjectInHand();
				}
			}
			else
			{
				StopAllCoroutines();
				itemText.gameObject.SetActive(false);
				itemText.color = Color.white - new Color(0, 0, 0, 1f);

				//Remove Item from hand
				RemoveObjectInHand();
			}
		}

		private IEnumerator ShowItemName(string itemName)
		{
			itemText.text = itemName;
			itemText.gameObject.SetActive(true);
			itemText.color = Color.white;

			yield return new WaitForSeconds(1f);

			while (itemText.color.a > 0)
			{
				itemText.color -= new Color(0, 0, 0, .01f);
				yield return null;
			}

			itemText.gameObject.SetActive(false);
			itemText.color = Color.white - new Color(0, 0, 0, 1f);
		}

		private void RemoveObjectInHand()
		{
			Destroy(objectInHand);
			objectInHand = null;
			playerInteractor.SetUseable(null);
		}

		public int GetNumberItemOfType(ItemScriptable itemType)
		{
			foreach (InventorySlot slot in inventorySlots)
			{
				if (slot.currentItem == itemType)
					return slot.GetItemNumber();
			}

			return 0;
		}

		public void RemoveItemOfType(ItemScriptable itemType)
		{
			foreach (InventorySlot slot in inventorySlots)
			{
				if (slot.currentItem == itemType)
					slot.ChangeNumber(-1);
			}
		}
	}
}
