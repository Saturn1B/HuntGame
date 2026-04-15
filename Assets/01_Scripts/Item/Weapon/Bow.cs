using UnityEngine;
using HuntingGame.Inventory;
using DG.Tweening;

namespace HuntingGame.Item
{
    public class Bow : MonoBehaviour, ISimpleUseable
    {
        [SerializeField] private ItemScriptable ammoType;
		[SerializeField] private Transform arrowSpawnPoint;
		[SerializeField] private float maxBowForce;
		[SerializeField] private float timeToMaxTension;
		private PlayerInventory playerInventory;

		private Rigidbody currentAmmo;

		private float currentTension = 0f;
		private bool isPulling = false;

		private void Awake()
		{
			playerInventory = GetComponentInParent<PlayerInventory>();
		}

		private void Update()
		{
			if (!isPulling) return;

			currentTension = Mathf.MoveTowards(currentTension, 1f, Time.deltaTime / timeToMaxTension);
		}

		private void SpawnArrow()
		{
			currentAmmo = Instantiate(ammoType.itemPrefab, arrowSpawnPoint).GetComponent<Rigidbody>();

			isPulling = true;
		}

		private void Shoot()
		{
			isPulling = false;

			float recoilMult = Mathf.Lerp(0.1f, 1f, currentTension);
			transform.DOPunchPosition(new Vector3(0, .05f, -.1f) * recoilMult, .3f, 5, 1);
			transform.DOPunchRotation(new Vector3(-5, 0, 0) * recoilMult, .3f, 5, 1);

			if (currentAmmo != null && currentAmmo.TryGetComponent(out Arrow arrow))
				arrow.Shoot(maxBowForce * currentTension);

			playerInventory.RemoveItemOfType(ammoType);
			currentAmmo = null;
			currentTension = 0f;
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
