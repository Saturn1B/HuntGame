using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace ProceduralGeneration
{
	[CreateAssetMenu(fileName = "LoopData", menuName = "Scriptable Objects/LoopData")]
	public class LoopData : ScriptableObject
	{
		public List<RoomData> roomLoop = new List<RoomData>();
		public List<Pose> relativePoseLoop = new List<Pose>();
	}
}
