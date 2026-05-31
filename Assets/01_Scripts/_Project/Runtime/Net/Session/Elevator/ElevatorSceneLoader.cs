using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace DungeonSteakhouse.Net.Session
{
    [DisallowMultipleComponent]
    public sealed class ElevatorSceneLoader : MonoBehaviour
    {
        [SerializeField] private string elevatorSceneName = "Elevator";
        [SerializeField] private bool logDebug = true;

        private void Start()
        {
            if (!NetworkManager.Singleton.IsServer) return;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name == elevatorSceneName)
                {
                    if (logDebug) Debug.Log($"[ElevatorSceneLoader] '{elevatorSceneName}' already loaded, skipping.");
                    return;
                }
            }

            var status = NetworkManager.Singleton.SceneManager.LoadScene(elevatorSceneName, LoadSceneMode.Additive);
            if (logDebug) Debug.Log($"[ElevatorSceneLoader] LoadScene '{elevatorSceneName}': {status}");
        }
    }
}
