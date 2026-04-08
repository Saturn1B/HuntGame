using UnityEngine;

namespace HuntingGame.Gameplay
{
	public class DamageZone : MonoBehaviour
	{
		[SerializeField] private int damageAmount;

		private void OnTriggerEnter(Collider other)
		{
			if (other.TryGetComponent(out Health health))
			{
				health.ChangeHealth(-damageAmount);
			}
		}
	}
}

