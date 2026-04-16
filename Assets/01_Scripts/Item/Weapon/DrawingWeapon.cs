using UnityEngine;
using DG.Tweening;

namespace HuntingGame.Item
{
    public class DrawingWeapon : ShootingWeapon
    {

		[Space]

		[Header("Tension Settings")]
		[SerializeField] private float timeToMaxTension;

		private float currentTension = .2f;
		private bool isPulling = false;

		private void Update()
		{
			if (!isPulling) return;

			currentTension = Mathf.MoveTowards(currentTension, 1f, Time.deltaTime / timeToMaxTension);
		}

		protected virtual void SpawnAmmo()
		{
			currentAmmo = Instantiate(ammoType.itemPrefab, arrowSpawnPoint).GetComponent<Munition>();

			isPulling = true;
		}

		protected override void Shoot()
		{
			isPulling = false;

			ShootRecoil();

			Vector3 rndOffset = Random.insideUnitSphere * spread;

			if (currentAmmo != null)
				currentAmmo.Shoot(weaponPower * currentTension, rndOffset);

			currentAmmo = null;
			currentTension = .2f;

			if (!needAmmo)
			{
				isReloading = true;
				StartCoroutine(Reload());
			}
			else
				playerInventory.RemoveItemOfType(ammoType);
		}

		protected override void ShootRecoil()
		{
			transform.DOKill();

			transform.localPosition = itemBehaviour.itemData.offset.position;

			float recoilMult = Mathf.Lerp(0.1f, 1f, currentTension);
			transform.DOPunchPosition(new Vector3(0, .05f, -.1f) * recoilMult, .3f, 5, 1);
			transform.DOPunchRotation(new Vector3(-5, 0, 0) * recoilMult, .3f, 5, 1);
		}

		public override void StartUse()
		{
			SpawnAmmo();
		}

		public override void EndUse()
		{
			Shoot();
		}
	}
}
