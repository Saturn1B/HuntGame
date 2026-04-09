using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace HuntingGame.Inventory
{
    public class InventorySlot : MonoBehaviour
    {
		[Header("Slot Setup")]
		[SerializeField] private Image itemImage;
		[SerializeField] private TMP_Text itemNumberText;
		private int itemNumber;
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
    }
}
