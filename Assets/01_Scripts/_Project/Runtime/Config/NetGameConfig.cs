using UnityEngine;

namespace DungeonSteakhouse.Net
{
    [CreateAssetMenu(menuName = "DungeonSteakhouse/Net/Net Game Config", fileName = "NetGameConfig")]
    public sealed class NetGameConfig : ScriptableObject
    {
        [Header("Lobby")]
        [Min(1)][SerializeField] private int maxPlayers = 4;
        public int MaxPlayers => maxPlayers;

        [Tooltip("Used to reject incompatible builds when joining a lobby.")]
        [SerializeField] private string buildVersion = "0.1.0-dev";
        public string BuildVersion => buildVersion;

        [Header("Flow")]
        [Tooltip("If false, the project assumes no late-join (recommended for early development).")]
        [SerializeField] private bool allowLateJoin = false;
        public bool AllowLateJoin => allowLateJoin;

        [Tooltip("Keep the networking root alive across scene loads.")]
        [SerializeField] private bool dontDestroyOnLoad = true;
        public bool DontDestroyOnLoad => dontDestroyOnLoad;

        [Tooltip("If true, the server will set the active scene (Dungeon on run start) and synchronize it to clients.")]
        [SerializeField] private bool syncActiveScene = false;
        public bool SyncActiveScene => syncActiveScene;

        [Header("Scenes (must be in Build Settings)")]
        [Tooltip("Hub scene loaded at startup (additively).")]
        [SerializeField] private string tavernSceneName = "Tavern";
        public string TavernSceneName => tavernSceneName;

        [Tooltip("Run scene loaded when the host starts a run (additively).")]
        [SerializeField] private string dungeonSceneName = "Dungeon";
        public string DungeonSceneName => dungeonSceneName;

        [Tooltip("Persistent elevator scene loaded at startup via regular SceneManager (never unloaded).")]
        [SerializeField] private string elevatorSceneName = "Elevator";
        public string ElevatorSceneName => elevatorSceneName;
    }
}
