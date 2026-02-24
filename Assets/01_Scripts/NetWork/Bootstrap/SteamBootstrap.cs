using UnityEngine;
using Steamworks;

public class SteamBootstrap : MonoBehaviour
{
    [SerializeField] private uint appId = 480;
    public static bool Ready { get; private set; }

    void Awake()
    {
        // Empêche les doublons si tu as plusieurs scènes / reload domain etc.
        var existing = FindObjectsOfType<SteamBootstrap>(true);
        if (existing.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

        // ✅ Si Steam est déjà initialisé (par le transport ou autre), on ne ré-init pas
        if (SteamClient.IsValid)
        {
            Ready = true;
            Debug.Log($"Steam déjà prêt. Name={SteamClient.Name} Id={SteamClient.SteamId}");
            return;
        }

        try
        {
            SteamClient.Init(appId);
            Ready = SteamClient.IsValid;
            Debug.Log($"Steam Ready={Ready}. Name={SteamClient.Name} Id={SteamClient.SteamId}");
        }
        catch (System.Exception e)
        {
            // Si c'est déjà init, on accepte et on continue
            if (e.Message != null && e.Message.ToLower().Contains("already initialized"))
            {
                Ready = SteamClient.IsValid;
                Debug.Log($"Steam déjà initialisé ailleurs. Ready={Ready}");
            }
            else
            {
                Ready = false;
                Debug.LogError($"Steam init failed: {e}");
            }
        }
    }

    void Update()
    {
        if (SteamClient.IsValid)
        {
            Ready = true;
            SteamClient.RunCallbacks();
        }
    }

    void OnApplicationQuit()
    {
        // Optionnel : en Editor, ça évite parfois des soucis de “double init” au prochain Play
        if (SteamClient.IsValid)
            SteamClient.Shutdown();
    }
}