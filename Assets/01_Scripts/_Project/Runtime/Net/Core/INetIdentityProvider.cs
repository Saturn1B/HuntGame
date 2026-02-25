namespace DungeonSteakhouse.Net.Core
{
    public readonly struct NetLocalIdentity
    {
        public readonly ulong PlatformUserId;
        public readonly string DisplayName;

        public NetLocalIdentity(ulong platformUserId, string displayName)
        {
            PlatformUserId = platformUserId;
            DisplayName = displayName;
        }
    }

    public interface INetIdentityProvider
    {
        bool TryGetLocalIdentity(out NetLocalIdentity identity);
    }
}