using System;
using System.Collections;
using UnityEngine;
using Steamworks;

namespace DungeonSteakhouse.Net.Steam
{
    [DefaultExecutionOrder(-1000)]
    public sealed class SteamBootstrap : MonoBehaviour
    {
        public static bool Ready { get; private set; }
        public static event Action<bool> ReadyChanged;

        [Header("Steam Ownership")]
        [Tooltip("Leave this OFF when using FacepunchTransport, because FacepunchTransport already calls SteamClient.Init().")]
        [SerializeField] private bool initializeSteamClient = false;

        [Tooltip("Only used if Initialize Steam Client is ON.")]
        [SerializeField] private uint steamAppId = 480;

        [Tooltip("Leave this OFF when using FacepunchTransport, because FacepunchTransport already calls SteamClient.RunCallbacks().")]
        [SerializeField] private bool runCallbacks = false;

        [Tooltip("Only shutdown Steam if this component initialized it.")]
        [SerializeField] private bool shutdownOnDestroy = false;

        private bool _ownsSteamClient;

        private void Awake()
        {
            // If FacepunchTransport is present, it already initializes Steam in its Awake().
            // We keep this as an optional mode for tests or future refactors.
            if (initializeSteamClient && !SteamClient.IsValid)
            {
                try
                {
                    SteamClient.Init(steamAppId, true);
                    _ownsSteamClient = SteamClient.IsValid;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    _ownsSteamClient = false;
                }
            }

            StartCoroutine(WaitForSteamValid());
        }

        private void Update()
        {
            if (!runCallbacks)
                return;

            if (SteamClient.IsValid)
                SteamClient.RunCallbacks();
        }

        private void OnDestroy()
        {
            if (_ownsSteamClient && shutdownOnDestroy)
            {
                try
                {
                    SteamClient.Shutdown();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            // If we don't own Steam, we should not force Ready=false,
            // but we can still mark this instance as no longer providing a guarantee.
            SetReady(false);
        }

        private IEnumerator WaitForSteamValid()
        {
            yield return new WaitUntil(() => SteamClient.IsValid);
            SetReady(true);
        }

        private static void SetReady(bool ready)
        {
            if (Ready == ready)
                return;

            Ready = ready;
            ReadyChanged?.Invoke(Ready);
        }
    }
}