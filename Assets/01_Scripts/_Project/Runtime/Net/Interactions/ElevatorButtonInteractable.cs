using UnityEngine;
using DungeonSteakhouse.Net.Session;
using DungeonSteakhouse.Net.Flow;

namespace DungeonSteakhouse.Net.Interactions
{
    public enum ElevatorButtonAction
    {
        AutoBySessionPhase = 0,
        StartRun = 10,
        ReturnToLobby = 20
    }

    /// <summary>
    /// Elevator button that controls the session flow.
    /// Server-authoritative (runs on server via NetInteractable).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElevatorButtonInteractable : NetInteractable
    {
        [Header("Behavior")]
        [SerializeField] private ElevatorButtonAction action = ElevatorButtonAction.AutoBySessionPhase;

        [Header("References")]
        [SerializeField] private NetGameSession gameSession;
        [SerializeField] private NetSceneFlow sceneFlowFallback;

        [Header("Debug")]
        [SerializeField] private bool logServer = true;

        private void Awake()
        {
            if (gameSession == null)
                gameSession = NetGameSession.Instance;

            if (sceneFlowFallback == null)
                sceneFlowFallback = FindFirstObjectByType<NetSceneFlow>();
        }

        protected override bool ServerOnInteract(ulong interactorClientId)
        {
            if (gameSession == null)
                gameSession = NetGameSession.Instance;

            if (gameSession != null)
            {
                bool ok = TryExecuteWithSession(gameSession);
                if (logServer)
                    Debug.Log($"[ElevatorButtonInteractable] Session action={action} ok={ok}");

                return ok;
            }

            // Fallback if session manager is not present (not recommended long-term)
            bool fallbackOk = TryExecuteWithSceneFlow(sceneFlowFallback);
            if (logServer)
                Debug.Log($"[ElevatorButtonInteractable] Fallback SceneFlow action={action} ok={fallbackOk}");

            return fallbackOk;
        }

        private bool TryExecuteWithSession(NetGameSession session)
        {
            switch (action)
            {
                case ElevatorButtonAction.StartRun:
                    return session.ServerTryStartRun();

                case ElevatorButtonAction.ReturnToLobby:
                    return session.ServerTryReturnToLobby();

                default:
                    // Auto by phase
                    if (session.Phase == NetSessionPhase.Lobby)
                        return session.ServerTryStartRun();

                    if (session.Phase == NetSessionPhase.InRun)
                        return session.ServerTryReturnToLobby();

                    return false;
            }
        }

        private static bool TryExecuteWithSceneFlow(NetSceneFlow flow)
        {
            if (flow == null)
                return false;

            // Basic fallback (does not update NetGameSession phase)
            if (flow.Phase == NetRunPhase.Hub)
                return flow.TryStartRun();

            if (flow.Phase == NetRunPhase.InRun)
                return flow.TryReturnToHub();

            return false;
        }
    }
}