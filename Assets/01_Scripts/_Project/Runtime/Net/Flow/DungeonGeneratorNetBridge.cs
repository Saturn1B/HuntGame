using DungeonSteakhouse.Net.Session;
using HuntingGame.ProceduralGeneration;
using Unity.Netcode;
using UnityEngine;

namespace HuntingGame.ProceduralGeneration
{
    [RequireComponent(typeof(DungeonGenerator))]
    public class DungeonGeneratorNetBridge : MonoBehaviour
    {
        private DungeonGenerator _generator;

        private void Awake()
        {
            _generator = GetComponent<DungeonGenerator>();
        }

        private void OnEnable()
        {
            if (NetSessionManager.Instance == null) return;

            NetSessionManager.Instance.StateChanged += OnSessionStateChanged;

            // Event may have already fired before this scene was loaded
            if (NetworkManager.Singleton.IsServer
                && NetSessionManager.Instance.State == NetSessionState.StartingRun)
            {
                _generator.Generate();
            }
        }

        private void OnDisable()
        {
            if (NetSessionManager.Instance != null)
                NetSessionManager.Instance.StateChanged -= OnSessionStateChanged;
        }

        private void OnSessionStateChanged(NetSessionState previous, NetSessionState next)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            if (next != NetSessionState.StartingRun) return;

            _generator.Generate();
        }
    }
}