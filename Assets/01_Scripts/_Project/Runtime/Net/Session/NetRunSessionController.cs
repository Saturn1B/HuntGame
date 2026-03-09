using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using DungeonSteakhouse.Net;
using DungeonSteakhouse.Net.Flow;

namespace DungeonSteakhouse.Net.Session
{
    public enum NetSessionAutoCommand
    {
        Auto = 0,
        StartRun = 1,
        ReturnToHub = 2
    }

    /// <summary>
    /// Single, server-authoritative orchestrator for:
    /// - Start run (load Dungeon additively + elevator down)
    /// - Return to hub (unload Dungeon + elevator up)
    ///
    /// Notes:
    /// - Hub scene stays loaded forever.
    /// - Dungeon scene is loaded/unloaded via NGO NetworkSceneManager.
    /// - This class is intentionally a MonoBehaviour (no NetworkObject required).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetRunSessionController : MonoBehaviour
    {
        public static NetRunSessionController Instance { get; private set; }

        [Header("References")]
        [SerializeField] private NetGameRoot netGameRoot;
        [SerializeField] private NetReadyPlatformGate startGate;
        [SerializeField] private NetReadyPlatformGate returnGate;
        [SerializeField] private NetElevatorDescentController elevator;

        [Header("Elevator Travel")]
        [Tooltip("Positive distance along elevator local axis (controller defines the axis, default is down).")]
        [SerializeField] private float downDistance = 12f;
        [SerializeField] private float downDuration = 6f;

        [Tooltip("If 0, 'up' uses -downDistance.")]
        [SerializeField] private float upDistance = 0f;
        [SerializeField] private float upDuration = 6f;

        [Header("Debug")]
        [SerializeField] private bool logDebug = true;

        private bool _busy;
        private bool _loadEventCompleted;
        private bool _unloadEventCompleted;

        private string _dungeonSceneName;
        private Coroutine _sequenceRoutine;

        public bool IsBusy => _busy;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (netGameRoot == null)
                netGameRoot = NetGameRoot.Instance;

            if (netGameRoot != null && netGameRoot.Config != null)
                _dungeonSceneName = netGameRoot.Config.dungeonSceneName;

            if (string.IsNullOrWhiteSpace(_dungeonSceneName))
                _dungeonSceneName = "Dungeon";

            // Best-effort auto-wiring (optional). You can still assign references explicitly.
            if (startGate == null || returnGate == null)
            {
                var gates = FindObjectsOfType<NetReadyPlatformGate>(true);
                if (startGate == null)
                    startGate = FindGate(gates, NetReadyGateMode.HubOnly);

                if (returnGate == null)
                    returnGate = FindGate(gates, NetReadyGateMode.InRunOnly) ?? FindGate(gates, NetReadyGateMode.HubAndRun);
            }

            if (elevator == null)
                elevator = FindObjectOfType<NetElevatorDescentController>(true);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            UnsubscribeSceneCallbacks();
        }

        public bool CanExecuteServerCommand(NetSessionAutoCommand command, out string reason)
        {
            reason = string.Empty;

            if (command == NetSessionAutoCommand.Auto)
                command = GetAutoCommandFromPhase();

            return command switch
            {
                NetSessionAutoCommand.StartRun => CanStartRunServer(out reason),
                NetSessionAutoCommand.ReturnToHub => CanReturnToHubServer(out reason),
                _ => false
            };
        }

        public void ServerExecuteCommand(ulong senderClientId, NetSessionAutoCommand command)
        {
            if (command == NetSessionAutoCommand.Auto)
                command = GetAutoCommandFromPhase();

            switch (command)
            {
                case NetSessionAutoCommand.StartRun:
                    ServerRequestStartRun(senderClientId);
                    break;

                case NetSessionAutoCommand.ReturnToHub:
                    ServerRequestReturnToHub(senderClientId);
                    break;
            }
        }

        public bool CanStartRunServer(out string reason)
        {
            reason = string.Empty;

            if (!IsServer())
            {
                reason = "Not running as server.";
                return false;
            }

            if (_busy)
            {
                reason = "Session controller is busy.";
                return false;
            }

            if (!IsInHubPhase())
            {
                reason = "Not in Hub phase.";
                return false;
            }

            if (startGate != null && !startGate.AllReadyConfirmedServer)
            {
                reason = "Start gate not confirmed ready.";
                return false;
            }

            if (IsDungeonLoaded())
            {
                reason = "Dungeon already loaded.";
                return false;
            }

            return true;
        }

        public bool CanReturnToHubServer(out string reason)
        {
            reason = string.Empty;

            if (!IsServer())
            {
                reason = "Not running as server.";
                return false;
            }

            if (_busy)
            {
                reason = "Session controller is busy.";
                return false;
            }

            if (!IsInRunPhase())
            {
                reason = "Not in Run phase.";
                return false;
            }

            if (!IsDungeonLoaded())
            {
                reason = "Dungeon not loaded.";
                return false;
            }

            if (returnGate != null && !returnGate.AllReadyConfirmedServer)
            {
                reason = "Return gate not confirmed ready.";
                return false;
            }

            return true;
        }

        public void ServerRequestStartRun(ulong senderClientId)
        {
            if (!CanStartRunServer(out string reason))
            {
                if (logDebug)
                    Debug.LogWarning($"[NetRunSessionController] StartRun denied (client {senderClientId}): {reason}");
                return;
            }

            if (logDebug)
                Debug.Log($"[NetRunSessionController] StartRun accepted (client {senderClientId}). Loading '{_dungeonSceneName}' + elevator down.");

            _busy = true;
            _loadEventCompleted = false;

            startGate?.ServerClearConfirmedReady();

            if (elevator != null)
                elevator.ServerMoveBy(downDistance, Mathf.Max(0.01f, downDuration));

            SubscribeSceneCallbacks();

            var status = NetworkManager.Singleton.SceneManager.LoadScene(_dungeonSceneName, LoadSceneMode.Additive);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"[NetRunSessionController] LoadScene failed: '{_dungeonSceneName}', status={status}");
                AbortSequence();
                return;
            }

            RestartRoutine(StartFinalizeRoutine());
        }

        public void ServerRequestReturnToHub(ulong senderClientId)
        {
            if (!CanReturnToHubServer(out string reason))
            {
                if (logDebug)
                    Debug.LogWarning($"[NetRunSessionController] ReturnToHub denied (client {senderClientId}): {reason}");
                return;
            }

            if (logDebug)
                Debug.Log($"[NetRunSessionController] ReturnToHub accepted (client {senderClientId}). Unloading '{_dungeonSceneName}' + elevator up.");

            _busy = true;
            _unloadEventCompleted = false;

            returnGate?.ServerClearConfirmedReady();

            float actualUpDistance = Mathf.Abs(upDistance) > 0.001f ? upDistance : -downDistance;

            if (elevator != null)
                elevator.ServerMoveBy(actualUpDistance, Mathf.Max(0.01f, upDuration));

            SubscribeSceneCallbacks();

            Scene dungeon = SceneManager.GetSceneByName(_dungeonSceneName);
            if (!dungeon.IsValid() || !dungeon.isLoaded)
            {
                Debug.LogError($"[NetRunSessionController] Dungeon scene not valid/loaded on server: '{_dungeonSceneName}'.");
                AbortSequence();
                return;
            }

            var status = NetworkManager.Singleton.SceneManager.UnloadScene(dungeon);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"[NetRunSessionController] UnloadScene failed: '{_dungeonSceneName}', status={status}");
                AbortSequence();
                return;
            }

            RestartRoutine(ReturnFinalizeRoutine());
        }

        private IEnumerator StartFinalizeRoutine()
        {
            while (_busy && !_loadEventCompleted)
                yield return null;

            while (_busy && elevator != null && elevator.IsMoving)
                yield return null;

            if (logDebug)
                Debug.Log("[NetRunSessionController] StartRun sequence completed (dungeon loaded + elevator arrived).");

            FinishSequence();
        }

        private IEnumerator ReturnFinalizeRoutine()
        {
            while (_busy && !_unloadEventCompleted)
                yield return null;

            while (_busy && elevator != null && elevator.IsMoving)
                yield return null;

            if (logDebug)
                Debug.Log("[NetRunSessionController] ReturnToHub sequence completed (dungeon unloaded + elevator arrived).");

            FinishSequence();
        }

        private void SubscribeSceneCallbacks()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SceneManager == null)
                return;

            nm.SceneManager.OnSceneEvent -= OnSceneEvent;
            nm.SceneManager.OnSceneEvent += OnSceneEvent;
        }

        private void UnsubscribeSceneCallbacks()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SceneManager == null)
                return;

            nm.SceneManager.OnSceneEvent -= OnSceneEvent;
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            if (!IsServer())
                return;

            if (!_busy)
                return;

            // Only care about the dungeon scene.
            if (sceneEvent.SceneName != _dungeonSceneName)
                return;

            // On host, we only process "local server" events here.
            if (sceneEvent.ClientId != NetworkManager.ServerClientId)
                return;

            switch (sceneEvent.SceneEventType)
            {
                case SceneEventType.LoadEventCompleted:
                    _loadEventCompleted = true;
                    if (logDebug)
                        Debug.Log($"[NetRunSessionController] LoadEventCompleted: '{sceneEvent.SceneName}'.");
                    break;

                case SceneEventType.UnloadEventCompleted:
                    _unloadEventCompleted = true;
                    if (logDebug)
                        Debug.Log($"[NetRunSessionController] UnloadEventCompleted: '{sceneEvent.SceneName}'.");
                    break;
            }
        }

        private NetSessionAutoCommand GetAutoCommandFromPhase()
        {
            if (IsInHubPhase())
                return NetSessionAutoCommand.StartRun;

            if (IsInRunPhase())
                return NetSessionAutoCommand.ReturnToHub;

            return NetSessionAutoCommand.StartRun;
        }

        private bool IsInHubPhase()
        {
            var flow = netGameRoot != null ? netGameRoot.SceneFlow : null;
            return flow == null || flow.Phase == NetRunPhase.Hub;
        }

        private bool IsInRunPhase()
        {
            var flow = netGameRoot != null ? netGameRoot.SceneFlow : null;
            return flow != null && flow.Phase == NetRunPhase.InRun;
        }

        private bool IsDungeonLoaded()
        {
            Scene dungeon = SceneManager.GetSceneByName(_dungeonSceneName);
            return dungeon.IsValid() && dungeon.isLoaded;
        }

        private static bool IsServer()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsServer;
        }

        private static NetReadyPlatformGate FindGate(NetReadyPlatformGate[] gates, NetReadyGateMode expectedMode)
        {
            if (gates == null)
                return null;

            for (int i = 0; i < gates.Length; i++)
            {
                if (gates[i] != null && gates[i].Mode == expectedMode)
                    return gates[i];
            }

            return null;
        }

        private void RestartRoutine(IEnumerator routine)
        {
            if (_sequenceRoutine != null)
                StopCoroutine(_sequenceRoutine);

            _sequenceRoutine = StartCoroutine(routine);
        }

        private void FinishSequence()
        {
            _busy = false;
            _sequenceRoutine = null;

            UnsubscribeSceneCallbacks();
        }

        private void AbortSequence()
        {
            if (logDebug)
                Debug.LogWarning("[NetRunSessionController] Sequence aborted.");

            _busy = false;
            _sequenceRoutine = null;

            UnsubscribeSceneCallbacks();
        }
    }
}
