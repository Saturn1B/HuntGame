using UnityEngine;


namespace ProceduralGeneration
{
	public class LoopViewer : MonoBehaviour
	{
		[SerializeField] private LoopData loopData;

#if UNITY_EDITOR
		[Sirenix.OdinInspector.Button, Sirenix.OdinInspector.EnableIf("@loopData != null"), Sirenix.OdinInspector.GUIColor(0, 1, 0)]
		public void Preview()
		{
			ViewLoop();
		}
		[Sirenix.OdinInspector.Button, Sirenix.OdinInspector.EnableIf("@loopData != null"), Sirenix.OdinInspector.GUIColor(1, 0, 0)]
		public void ClearPreview()
		{
			ClearLoop();
			loopData = null;
		}
#endif

		public void ViewLoop()
		{
			ClearLoop();

			for (int i = 0; i < loopData.roomLoop.Count; i++)
			{
				Room room = Instantiate(loopData.roomLoop[i].roomPrefab, loopData.relativePositionLoop[i], loopData.relativeRotationLoop[i]).GetComponent<Room>();
				room.transform.SetParent(transform);
			}
		}

		private void ClearLoop()
		{
			//Destroy all room in dungeon
			for (int i = transform.childCount - 1; i >= 0; i--)
			{
				GameObject child = transform.GetChild(i).gameObject;
				DestroyImmediate(child);
			}
		}
	}
}

