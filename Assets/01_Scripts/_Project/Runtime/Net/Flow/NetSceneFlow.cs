using System;
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
        public event Action<NetRunPhase> PhaseChanged;

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

            // Host updates immediately; clients will update on LoadComplete event.
            SetPhase(NetRunPhase.InRun);
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
                SetPhase(NetRunPhase.Hub);
                return false;
            }

            var status = sceneManager.UnloadScene(_dungeonScene);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogWarning($"[NetSceneFlow] Failed to unload dungeon scene with status: {status}");
                return false;
            }

            // Host updates immediately; clients will update on UnloadComplete event.
            SetPhase(NetRunPhase.Hub);
            return true;
        }

        private void SetPhase(NetRunPhase phase)
        {
            if (_phase == phase)
                return;

            _phase = phase;
            PhaseChanged?.Invoke(_phase);
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

            sceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);

            // You requested: don't force active scene changes by default.
            sceneManager.ActiveSceneSynchronizationEnabled = (config != null && config.syncActiveScene);

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
            if (networkManager == null || config == null)
                return;

            // Determine if this event is "local" to this process (important on host: it receives events for all clients).
            bool isLocalEvent;
            if (networkManager.IsServer)
                isLocalEvent = sceneEvent.ClientId == NetworkManager.ServerClientId;
            else
                isLocalEvent = sceneEvent.ClientId == networkManager.LocalClientId;

            // Update local phase when THIS client finishes loading/unloading the dungeon scene.
            if (isLocalEvent)
            {
                if (sceneEvent.SceneEventType == SceneEventType.LoadComplete && sceneEvent.SceneName == config.dungeonSceneName)
                    SetPhase(NetRunPhase.InRun);

                if (sceneEvent.SceneEventType == SceneEventType.UnloadComplete && sceneEvent.SceneName == config.dungeonSceneName)
                    SetPhase(NetRunPhase.Hub);

                if (sceneEvent.SceneEventType == SceneEventType.LoadComplete && sceneEvent.SceneName == config.tavernSceneName)
                    SetPhase(NetRunPhase.Hub);
            }

            // Keep server-side scene references (only server local event)
            if (!networkManager.IsServer || sceneEvent.ClientId != NetworkManager.ServerClientId)
                return;

            switch (sceneEvent.SceneEventType)
            {
                case SceneEventType.LoadComplete:
                    {
                        if (sceneEvent.SceneName == config.tavernSceneName)
                            _tavernScene = sceneEvent.Scene;

                        if (sceneEvent.SceneName == config.dungeonSceneName)
                            _dungeonScene = sceneEvent.Scene;

                        break;
                    }

                case SceneEventType.UnloadComplete:
                    {
                        if (sceneEvent.SceneName == config.dungeonSceneName)
                            _dungeonScene = default;

                        break;
                    }

                case SceneEventType.LoadEventCompleted:
                    {
                        if (config.syncActiveScene)
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