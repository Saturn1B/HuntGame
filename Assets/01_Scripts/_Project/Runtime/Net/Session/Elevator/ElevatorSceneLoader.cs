using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace DungeonSteakhouse.Net.Session
{
    [DisallowMultipleComponent]
    public sealed class ElevatorSceneLoader : NetworkBehaviour
    {
        [SerializeField] private string elevatorSceneName = "Elevator";
        [SerializeField] private bool logDebug = true;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer) return;

            // Already loaded locally (e.g. placed in build as boot scene)
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name == elevatorSceneName)
                {
                    if (logDebug)
                        Debug.Log($"[ElevatorSceneLoader] '{elevatorSceneName}' already loaded, skipping.");
                    return;
                }
            }

            if (NetworkManager.SceneManager == null)
            {
                Debug.LogError("[ElevatorSceneLoader] NetworkSceneManager not available.");
                return;
            }

            var status = NetworkManager.SceneManager.LoadScene(
                elevatorSceneName, LoadSceneMode.Additive);

            if (logDebug)
                Debug.Log($"[ElevatorSceneLoader] Loading '{elevatorSceneName}' via NGO. Status={status}");
        }
    }
}