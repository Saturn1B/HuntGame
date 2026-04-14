using UnityEngine;
using HuntingGame.Inventory;

namespace HuntingGame.Item
{
    public class Bow : MonoBehaviour, ISimpleUseable
    {
        [SerializeField] private ItemScriptable ammoType;
		[SerializeField] private Transform arrowSpawnPoint;
		[SerializeField] private float bowForce;
		private PlayerInventory playerInventory;

		private Rigidbody currentAmmo;

		private void Awake()
		{
			playerInventory = GetComponentInParent<PlayerInventory>();
		}

		private void SpawnArrow()
		{
			currentAmmo = Instantiate(ammoType.itemPrefab, arrowSpawnPoint).GetComponent<Rigidbody>();
		}

		private void Shoot()
		{
			playerInventory.RemoveItemOfType(ammoType);

			if (currentAmmo.TryGetComponent(out Arrow arrow))
				arrow.Shoot(bowForce);

			currentAmmo = null;
		}

		private bool HasAmmo()
		{
			if (playerInventory.GetNumberItemOfType(ammoType) > 0)
				return true;

			return false;
		}

		public void StartUse()
		{
			SpawnArrow();
		}

		public void EndUse()
		{
			Shoot();
		}

		public bool CanUse()
		{
			return HasAmmo();
		}
	}
}
