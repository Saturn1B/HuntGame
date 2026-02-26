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
		public bool isAvailable;
		public Room room;
		[SerializeField] private Collider boundCollider;

		// EDITOR FUNCTION

		private void OnDrawGizmos()
		{
			Gizmos.color = isAvailable ? Color.green : Color.red;
			Gizmos.DrawLine(transform.position, transform.forward * 2);
			Gizmos.DrawSphere(transform.position, .5f);
			Gizmos.DrawCube(boundCollider.bounds.center, boundCollider.bounds.size);
		}
	}
}
