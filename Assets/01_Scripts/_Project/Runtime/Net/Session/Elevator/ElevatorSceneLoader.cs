using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonSteakhouse.Net.Session
{
    [DisallowMultipleComponent]
    public sealed class ElevatorSceneLoader : MonoBehaviour
    {
        [SerializeField] private string elevatorSceneName = "Elevator";
        [SerializeField] private bool logDebug = true;

        private void Start()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name == elevatorSceneName)
                {
                    if (logDebug)
                        Debug.Log($"[ElevatorSceneLoader] '{elevatorSceneName}' already loaded, skipping.");
                    return;
                }
            }

            var op = SceneManager.LoadSceneAsync(elevatorSceneName, LoadSceneMode.Additive);
            if (op != null)
            {
                if (logDebug)
                    Debug.Log($"[ElevatorSceneLoader] Loading '{elevatorSceneName}' additively...");
            }
            else
            {
                Debug.LogError($"[ElevatorSceneLoader] Failed to start loading '{elevatorSceneName}'. Is it in Build Settings?");
            }
        }
    }
}