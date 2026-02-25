using UnityEngine;
using Steamworks;

public class SteamBootstrap : MonoBehaviour
{
    [SerializeField] private uint appId = 480; // Steam App ID (480 = Spacewar test app)
    public static bool Ready { get; private set; } // True when Steam is initialized and valid

    void Awake()
    {
        // Prevent duplicates when you have multiple scenes, domain reloads, etc.
        var existing = FindObjectsOfType<SteamBootstrap>(true);
        if (existing.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        // Keep this object alive across scene loads
        DontDestroyOnLoad(gameObject);

        // If Steam is already initialized (by transport or something else), don't re-init
        if (SteamClient.IsValid)
        {
            Ready = true;
            Debug.Log($"Steam already ready. Name={SteamClient.Name} Id={SteamClient.SteamId}");
            return;
        }

        try
        {
            // Initialize Steamworks (Facepunch)
            SteamClient.Init(appId);

            // SteamClient.IsValid becomes true if initialization succeeded
            Ready = SteamClient.IsValid;
            Debug.Log($"Steam Ready={Ready}. Name={SteamClient.Name} Id={SteamClient.SteamId}");
        }
        catch (System.Exception e)
        {
            // If Steam was already initialized somewhere else, accept it and continue
            if (e.Message != null && e.Message.ToLower().Contains("already initialized"))
            {
                Ready = SteamClient.IsValid;
                Debug.Log($"Steam already initialized elsewhere. Ready={Ready}");
            }
            else
            {
                // Any other exception means initialization failed
                Ready = false;
                Debug.LogError($"Steam init failed: {e}");
            }
        }
    }

    void Update()
    {
        // Facepunch Steamworks requires callbacks to be pumped regularly
        if (SteamClient.IsValid)
        {
            Ready = true;
            SteamClient.RunCallbacks();
        }
    }

    void OnApplicationQuit()
    {
        // Optional: in the Editor, this can help avoid "double init" issues on next Play
        if (SteamClient.IsValid)
            SteamClient.Shutdown();
    }
}