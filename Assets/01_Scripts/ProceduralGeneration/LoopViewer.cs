using UnityEngine;


namespace ProceduralGeneration
{
	public class LoopViewer : MonoBehaviour
	{
		[SerializeField] private LoopData[] loopLibrary;
		[SerializeField] private string currentlyViewing;
		private LoopData currentData;
		[SerializeField] private int currentIndex;

#if UNITY_EDITOR
		[Sirenix.OdinInspector.Button, Sirenix.OdinInspector.EnableIf("@loopLibrary.Length > 0"), Sirenix.OdinInspector.GUIColor(0, 1, 0)]
		public void Preview()
		{
			currentIndex = 0;
			ViewLoop();
		}
		[Sirenix.OdinInspector.Button, Sirenix.OdinInspector.EnableIf("@loopLibrary.Length > 1"), Sirenix.OdinInspector.GUIColor(.1f, .5f, .8f)]
		public void Next()
		{
			currentIndex = (currentIndex + 1) % loopLibrary.Length;
			ViewLoop();
		}
		[Sirenix.OdinInspector.Button, Sirenix.OdinInspector.EnableIf("@currentData != null"), Sirenix.OdinInspector.GUIColor(1, 0, 0)]
		public void ClearPreview()
		{
			ClearLoop();
			currentData = null;
			currentlyViewing = "";
		}
#endif

		public void ViewLoop()
		{
			ClearLoop();

			currentData = loopLibrary[currentIndex];
			currentlyViewing = currentData.name;

			for (int i = 0; i < currentData.roomLoop.Count; i++)
			{
				Room room = Instantiate(currentData.roomLoop[i].roomPrefab, currentData.relativePoseLoop[i].position, currentData.relativePoseLoop[i].rotation).GetComponent<Room>();
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

