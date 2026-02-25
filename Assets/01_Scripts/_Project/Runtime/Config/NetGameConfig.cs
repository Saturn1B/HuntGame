using UnityEngine;

namespace DungeonSteakhouse.Net
{
    [CreateAssetMenu(menuName = "DungeonSteakhouse/Net/Net Game Config", fileName = "NetGameConfig")]
    public sealed class NetGameConfig : ScriptableObject
    {
        [Header("Lobby")]
        [Min(1)] public int maxPlayers = 4;

        [Tooltip("Used to reject incompatible builds when joining a lobby.")]
        public string buildVersion = "0.1.0-dev";

        [Header("Flow")]
        [Tooltip("If false, the project assumes no late-join (recommended for early development).")]
        public bool allowLateJoin = false;

        [Tooltip("Keep the networking root alive across scene loads.")]
        public bool dontDestroyOnLoad = true;

        [Tooltip("If true, the server will set the active scene (Dungeon on run start) and synchronize it to clients.")]
        public bool syncActiveScene = false;

        [Header("Scenes (must be in Build Settings)")]
        [Tooltip("Hub scene loaded at startup (additively).")]
        public string tavernSceneName = "Tavern";

        [Tooltip("Run scene loaded when the host starts a run (additively).")]
        public string dungeonSceneName = "Dungeon";
    }
}