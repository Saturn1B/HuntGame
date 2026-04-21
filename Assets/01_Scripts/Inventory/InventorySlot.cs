using UnityEngine;
using TMPro;
using UnityEngine.UI;
using HuntingGame.Item;

namespace HuntingGame.Inventory
{
    public class InventorySlot : MonoBehaviour
    {
		[Header("Slot Setup")]
		[SerializeField] private Image itemImage;
		[SerializeField] private TMP_Text itemNumberText;
		[SerializeField] private int itemNumber;
		public ItemScriptable currentItem;

		private Camera playerCamera;
		private PlayerInventory playerInventory;

		private void Start()
		{
			RefreshItem();
		}

		public void Setup(PlayerInventory playerInventory)
		{
			this.playerInventory = playerInventory;
			itemImage.gameObject.SetActive(false);
			itemNumberText.gameObject.SetActive(false);
		}

		private void RefreshItem()
		{
			if (itemNumber == 0)
				currentItem = null;

			if(currentItem == null)
			{
				itemImage.gameObject.SetActive(false);
				itemNumberText.gameObject.SetActive(false);
				return;
			}

			if (currentItem.itemSprite != null)
			{
				itemImage.sprite = currentItem.itemSprite;
				itemImage.gameObject.SetActive(true);
			}

			itemNumberText.text = itemNumber.ToString();
			itemNumberText.gameObject.SetActive(itemNumber > 1);

		}

		public int GetItemNumber()
		{
			return itemNumber;
		}

		public void ChangeNumber(int value)
		{
			if (itemNumber + value > 1 && !currentItem.stackable) return;

			itemNumber += value;

			RefreshItem();
		}

		public void AddItem(ItemScriptable itemScriptable)
		{
			currentItem = itemScriptable;
			ChangeNumber(1);
			RefreshItem();
		}

		public void RemoveItem()
		{
			itemNumber = 0;

			RefreshItem();
		}
    }
}
