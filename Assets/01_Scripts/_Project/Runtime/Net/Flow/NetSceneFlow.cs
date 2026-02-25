using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace DungeonSteakhouse.Net.Flow
{
    public enum NetRunPhase
    {
        Hub = 0,
        InRun = 10
    }

    public sealed class NetSceneFlow : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private NetGameConfig config;

        [Header("References")]
        [SerializeField] private NetworkManager networkManager;

        private Scene _tavernScene;
        private Scene _dungeonScene;

        private bool _sceneManagerConfigured;
        private NetRunPhase _phase = NetRunPhase.Hub;

        public NetRunPhase Phase => _phase;

        private void Awake()
        {
            ValidateReferences();

            if (networkManager != null)
                networkManager.OnServerStarted += OnServerStarted;
        }

        private void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager.OnServerStarted -= OnServerStarted;

                if (networkManager.SceneManager != null)
                    networkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
            }
        }

        public bool TryStartRun()
        {
            if (!IsHostReadyForSceneOps(out var sceneManager))
                return false;

            if (_phase == NetRunPhase.InRun)
            {
                Debug.LogWarning("[NetSceneFlow] Run already started.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(config.dungeonSceneName))
            {
                Debug.LogError("[NetSceneFlow] Dungeon scene name is empty in NetGameConfig.");
                return false;
            }

            var status = sceneManager.LoadScene(config.dungeonSceneName, LoadSceneMode.Additive);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogWarning($"[NetSceneFlow] Failed to load dungeon scene '{config.dungeonSceneName}' with status: {status}");
                return false;
            }

            _phase = NetRunPhase.InRun;
            return true;
        }

        public bool TryReturnToHub()
        {
            if (!IsHostReadyForSceneOps(out var sceneManager))
                return false;

            if (_phase != NetRunPhase.InRun)
            {
                Debug.LogWarning("[NetSceneFlow] Not in run. Nothing to unload.");
                return false;
            }

            if (!_dungeonScene.IsValid() || !_dungeonScene.isLoaded)
            {
                Debug.LogWarning("[NetSceneFlow] Dungeon scene invalid/not loaded. Forcing phase back to Hub.");
                _phase = NetRunPhase.Hub;
                return false;
            }

            var status = sceneManager.UnloadScene(_dungeonScene);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogWarning($"[NetSceneFlow] Failed to unload dungeon scene with status: {status}");
                return false;
            }

            _phase = NetRunPhase.Hub;
            return true;
        }

        private void OnServerStarted()
        {
            ConfigureSceneManagerOnce();
        }

        private void ConfigureSceneManagerOnce()
        {
            if (_sceneManagerConfigured)
                return;

            if (networkManager == null || networkManager.SceneManager == null)
                return;

            var sceneManager = networkManager.SceneManager;

            // Bootstrap + additive approach: server loads/unloads additive scenes.
            sceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);

            // Active scene sync is OPTIONAL (you asked for it disabled).
            sceneManager.ActiveSceneSynchronizationEnabled = (config != null && config.syncActiveScene);

            // Strict validation: only allow additive loads for known scenes.
            sceneManager.VerifySceneBeforeLoading = VerifySceneBeforeLoading;

            sceneManager.OnSceneEvent += OnSceneEvent;

            _sceneManagerConfigured = true;
            Debug.Log($"[NetSceneFlow] NetworkSceneManager configured. ActiveSceneSync={sceneManager.ActiveSceneSynchronizationEnabled}");
        }

        private bool VerifySceneBeforeLoading(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode)
        {
            if (loadSceneMode == LoadSceneMode.Single)
                return false;

            if (config == null)
                return false;

            return sceneName == config.tavernSceneName || sceneName == config.dungeonSceneName;
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            var isServerLocalEvent = sceneEvent.ClientId == NetworkManager.ServerClientId;

            switch (sceneEvent.SceneEventType)
            {
                case SceneEventType.LoadComplete:
                    {
                        if (!isServerLocalEvent)
                            return;

                        if (sceneEvent.SceneName == config.tavernSceneName)
                            _tavernScene = sceneEvent.Scene;

                        if (sceneEvent.SceneName == config.dungeonSceneName)
                            _dungeonScene = sceneEvent.Scene;

                        break;
                    }

                case SceneEventType.UnloadComplete:
                    {
                        if (!isServerLocalEvent)
                            return;

                        if (sceneEvent.SceneName == config.dungeonSceneName)
                            _dungeonScene = default;

                        break;
                    }

                case SceneEventType.LoadEventCompleted:
                    {
                        // Server receives this when all clients finished loading.
                        if (!isServerLocalEvent)
                            return;

                        // You requested: do NOT force active scene changes by default.
                        // If later you decide you want it, toggle config.syncActiveScene.
                        if (config != null && config.syncActiveScene)
                        {
                            if (sceneEvent.SceneName == config.dungeonSceneName && _dungeonScene.IsValid() && _dungeonScene.isLoaded)
                                SceneManager.SetActiveScene(_dungeonScene);

                            if (sceneEvent.SceneName == config.tavernSceneName && _tavernScene.IsValid() && _tavernScene.isLoaded)
                                SceneManager.SetActiveScene(_tavernScene);
                        }

                        break;
                    }
            }
        }

        private bool IsHostReadyForSceneOps(out NetworkSceneManager sceneManager)
        {
            sceneManager = null;

            if (config == null)
            {
                Debug.LogError("[NetSceneFlow] NetGameConfig is missing.");
                return false;
            }

            if (networkManager == null)
            {
                Debug.LogError("[NetSceneFlow] NetworkManager reference is missing.");
                return false;
            }

            if (!networkManager.IsHost && !networkManager.IsServer)
            {
                Debug.LogWarning("[NetSceneFlow] Only the host/server can load/unload network scenes.");
                return false;
            }

            sceneManager = networkManager.SceneManager;
            if (sceneManager == null)
            {
                Debug.LogWarning("[NetSceneFlow] NetworkSceneManager is not available yet (network not started?).");
                return false;
            }

            if (!_sceneManagerConfigured)
                ConfigureSceneManagerOnce();

            return true;
        }

        private void ValidateReferences()
        {
            if (config == null)
                Debug.LogWarning("[NetSceneFlow] NetGameConfig is not assigned.");

            if (networkManager == null)
                networkManager = NetworkManager.Singleton;

            if (networkManager == null)
                Debug.LogError("[NetSceneFlow] NetworkManager is missing.");
        }
    }
}