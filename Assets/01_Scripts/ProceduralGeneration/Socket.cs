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
		public Collider boundCollider;
		public Collider socket;

		public void CloseSocket()
		{
			isAvailable = false;
		}

		// EDITOR FUNCTION

		private void OnDrawGizmos()
		{
			Gizmos.color = Color.cyan;
			Gizmos.DrawRay(transform.position, transform.forward * 1f);
			Gizmos.color = isAvailable ? Color.green : Color.red;
			Gizmos.DrawWireCube(socket.bounds.center, socket.bounds.size);
			Gizmos.DrawWireCube(boundCollider.bounds.center, boundCollider.bounds.size);
		}
	}
}
