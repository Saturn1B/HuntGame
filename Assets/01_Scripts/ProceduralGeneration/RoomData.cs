using UnityEngine;
using System.Collections.Generic;

namespace HuntingGame.ProceduralGeneration
{
	[CreateAssetMenu(fileName = "RoomData", menuName = "Scriptable Objects/RoomData")]
	public class RoomData : ScriptableObject
	{
		public string roomName;
		public GameObject roomPrefab;
		public int roomWeight;
		public List<SocketType> socketTypes = new List<SocketType>();
	}
}
