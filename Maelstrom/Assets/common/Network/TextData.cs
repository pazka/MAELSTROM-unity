using System;
using System.Text;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Network data class for maelstrom values
    /// </summary>
    public class TextData : INetworkData
    {
        public TextData()
        {
        }

        public TextData(CommonMaelstrom.RoleId roleId, string text)
        {
            RoleId = roleId;
            Text = text;
        }

        public CommonMaelstrom.RoleId RoleId { get; set; }
        public string Text { get; set; }

        /// <summary>
        ///     Serializes the object to a byte array for network transmission, roleId and value
        /// </summary>
        /// <returns>Byte array representation of the object</returns>
        public byte[] ToNetwork()
        {
            //encode roles, then length of text then text
            var bytes = new byte[1 + Text.Length + 1];
            bytes[0] = (byte)RoleId;
            bytes[1] = (byte)Text.Length;
            Encoding.UTF8.GetBytes(Text).CopyTo(bytes, 2);
            return bytes;
        }

        public static TextData FromNetwork(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentException("TextData requires at least 1 byte");
            var roleId = (CommonMaelstrom.RoleId)data[0];
            var length = data[1];
            var text = Encoding.UTF8.GetString(data, 2, length);
            return new TextData(roleId, text);
        }
    }
}