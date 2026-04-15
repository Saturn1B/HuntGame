using UnityEngine;
using Sirenix.OdinInspector;
using HuntingGame.Inventory;
using System.Collections;
using DG.Tweening;

namespace HuntingGame.Item
{
    public class ShootingWeapon : MonoBehaviour, ISimpleUseable
    {
		[Header("Weapon Settings")]
		[SerializeField] protected float weaponPower;
		[SerializeField, Range(0, .1f)] protected float spread;
		[SerializeField] protected ItemScriptable itemData;

		[Space]

		[Header("Weapon Ammo Settings")]
        [SerializeField] protected ItemScriptable ammoType;
        [SerializeField] protected Transform arrowSpawnPoint;
        [SerializeField] protected bool needAmmo;
        [SerializeField, Range(0, 10), HideIf("needAmmo")] private float reloadTime;

		protected bool isReloading;

        protected PlayerInventory playerInventory;
        protected Munition currentAmmo;

		protected virtual void Awake()
		{
			playerInventory = GetComponentInParent<PlayerInventory>();
		}

		protected virtual void Shoot()
		{
			ShootRecoil();

			currentAmmo = Instantiate(ammoType.itemPrefab, arrowSpawnPoint).GetComponent<Munition>();

			Vector3 rndOffset = Random.insideUnitSphere * spread;

			if (currentAmmo != null)
				currentAmmo.Shoot(weaponPower, rndOffset);

			currentAmmo = null;

			if (!needAmmo)
			{
				isReloading = true;
				StartCoroutine(Reload());
			}
			else
				playerInventory.RemoveItemOfType(ammoType);
		}

		protected virtual IEnumerator Reload()
		{
			yield return new WaitForSeconds(reloadTime);
			isReloading = false;
		}

		protected virtual bool HasAmmo()
		{
			if (playerInventory.GetNumberItemOfType(ammoType) > 0)
				return true;

			return false;
		}

		protected virtual void ShootRecoil()
		{
			transform.DOKill();

			transform.localPosition = itemData.offset.position;

			transform.DOPunchPosition(new Vector3(0, .05f, -.1f), .3f, 5, 1);
			transform.DOPunchRotation(new Vector3(-5, 0, 0), .3f, 5, 1);
		}

		public virtual void StartUse()
		{
			Shoot();
		}

		public virtual void EndUse()
		{
		}

		public virtual bool CanUse()
		{
			if (!needAmmo)
			{
				if (isReloading) return false;
				return true;
			}

			return HasAmmo();
		}
	}
}
