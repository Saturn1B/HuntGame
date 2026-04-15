using UnityEngine;
using System.Collections.Generic;

namespace HuntingGame.ProceduralGeneration
{
	public class Room : MonoBehaviour
	{
		public string roomName;
		public Socket[] sockets;
		[HideInInspector] public Vector2[] footprint;
		[HideInInspector] public RoomData originalData;
	}
}
