using System;
using System.Text;
using UnityEngine;

namespace DungeonSteakhouse.Net.Connection
{
    [Serializable]
    public struct NetConnectionPayload
    {
        public string buildVersion;
        public ulong platformUserId;
        public string displayName;
    }

    public static class NetConnectionPayloadCodec
    {
        public static byte[] Encode(NetConnectionPayload payload)
        {
            var json = JsonUtility.ToJson(payload);
            return Encoding.UTF8.GetBytes(json);
        }

        public static bool TryDecode(byte[] data, out NetConnectionPayload payload)
        {
            payload = default;

            if (data == null || data.Length == 0)
                return false;

            try
            {
                var json = Encoding.UTF8.GetString(data);
                payload = JsonUtility.FromJson<NetConnectionPayload>(json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}