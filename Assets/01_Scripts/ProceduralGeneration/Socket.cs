using UnityEngine;

namespace HuntingGame.ProceduralGeneration
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
		[HideInInspector] public bool isAvailable = true;
		[HideInInspector] public Room room;
		[HideInInspector] public Room connectedRoom;
		[HideInInspector] public BoxCollider boundCollider;
		public Collider socket;
		[SerializeField] private GameObject barricade;

		private void Awake()
		{
			if (boundCollider == null)
				boundCollider = GetComponent<BoxCollider>();
		}

		public void CloseSocket()
		{
			barricade.SetActive(true);
			isAvailable = false;
		}

		// EDITOR FUNCTION

		private void OnDrawGizmos()
		{
			Gizmos.color = Color.cyan;
			if (socket != null)
				Gizmos.DrawRay(socket.transform.position, transform.forward * 1f);

			Gizmos.color = isAvailable ? Color.green : Color.red;
			if (socket != null)
				Gizmos.DrawWireCube(socket.bounds.center, socket.bounds.size);
			if (boundCollider != null)
				Gizmos.DrawWireCube(boundCollider.bounds.center, boundCollider.bounds.size);
		}
	}
}
