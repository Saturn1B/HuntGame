using UnityEngine;
using System.Collections.Generic;

namespace ProceduralGeneration
{
	public class Room : MonoBehaviour
	{
		public string roomName;
		public Socket[] sockets;
		[HideInInspector] public Collider boundCollider;
		[HideInInspector] public Vector2[] footprint;
	}
}
