using Unity.Netcode;
using UnityEngine;

namespace DungeonSteakhouse.Net.InteractionsV2
{
    public enum UseAction : byte
    {
        Primary = 0,
        Secondary = 1
    }

    /// <summary>
    /// Context passed through predicted + server-confirmed use flow.
    /// </summary>
    public struct UseContext : INetworkSerializable
    {
        public ulong InteractorClientId;
        public Vector3 HitPoint;
        public uint PredictionId;
        public UseAction Action;

        public UseContext(ulong interactorClientId, Vector3 hitPoint, uint predictionId, UseAction action)
        {
            InteractorClientId = interactorClientId;
            HitPoint = hitPoint;
            PredictionId = predictionId;
            Action = action;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref InteractorClientId);
            serializer.SerializeValue(ref HitPoint);
            serializer.SerializeValue(ref PredictionId);
            serializer.SerializeValue(ref Action);
        }
    }
}