namespace DungeonSteakhouse.Net.Core
{
    public interface INetBootstrapper
    {
        bool IsReady { get; }
        void Initialize();
        void Shutdown();
    }
}
