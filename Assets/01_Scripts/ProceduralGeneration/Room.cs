using UnityEngine;
using System.Collections.Generic;

namespace ProceduralGeneration
{
	[RequireComponent(typeof(Collider))]
	public class Room : MonoBehaviour
	{
		public string roomName;
		public List<Socket> sockets = new List<Socket>();
		public Collider boundCollider;

		// EDITOR FUNCTION

		private void OnDrawGizmos()
		{
			Gizmos.color = Color.purple;
			Gizmos.DrawWireCube(boundCollider.bounds.center, boundCollider.bounds.size);
		}
	}
}
