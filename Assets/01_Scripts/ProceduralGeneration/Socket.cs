using UnityEngine;

namespace ProceduralGeneration
{
	public enum SocketType
	{
		SMALL,
		MEDIUM,
		LARGE
		// We can expand list if more socket type needed
	}

	[RequireComponent(typeof(Collider))]
	public class Socket : MonoBehaviour
	{
		public SocketType socketType;
		public bool isAvailable = true;
		public Room room;
		[SerializeField] private Collider boundCollider;

		// EDITOR FUNCTION

		private void OnDrawGizmos()
		{
			Gizmos.color = isAvailable ? Color.green : Color.red;
			Gizmos.DrawRay(transform.position, transform.forward * 1f);
			Gizmos.DrawWireSphere(transform.position, .1f);
			Gizmos.DrawWireCube(boundCollider.bounds.center, boundCollider.bounds.size);
		}
	}
}
