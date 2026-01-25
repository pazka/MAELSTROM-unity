using System;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Network data class for maelstrom values
    /// </summary>
    public class FloatData : INetworkData
    {
        public FloatData()
        {
        }

        public FloatData(CommonMaelstrom.RoleId roleId, float value)
        {
            RoleId = roleId;
            Value = value;
        }

        public CommonMaelstrom.RoleId RoleId { get; set; }
        public float Value { get; set; }

        /// <summary>
        ///     Serializes the object to a byte array for network transmission, roleId and value
        /// </summary>
        /// <returns>Byte array representation of the object</returns>
        public byte[] ToNetwork()
        {
            var bytes = new byte[6];
            var roleIdValue = (ushort)RoleId;
            bytes[0] = (byte)((roleIdValue >> 8) & 0xFF);
            bytes[1] = (byte)(roleIdValue & 0xFF);
            var floatLE = BitConverter.GetBytes(Value);
            bytes[2] = floatLE[3];
            bytes[3] = floatLE[2];
            bytes[4] = floatLE[1];
            bytes[5] = floatLE[0];
            return bytes;
        }

        public static FloatData FromNetwork(byte[] data)
        {
            if (data == null || data.Length != 6) throw new ArgumentException("FloatData requires exactly 6 bytes");
            var roleId = (ushort)((data[0] << 8) | data[1]);
            var value = BitConverter.ToSingle(new[] { data[5], data[4], data[3], data[2] }, 0);
            return new FloatData((CommonMaelstrom.RoleId)roleId, value);
        }
    }
}