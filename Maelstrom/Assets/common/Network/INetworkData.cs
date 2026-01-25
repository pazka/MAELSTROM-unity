namespace Maelstrom.Unity
{
    /// <summary>
    /// Interface for data classes that can be serialized and sent over the network.
    /// Classes implementing this interface must also provide a static FromNetwork method:
    /// public static T FromNetwork(byte[] data) where T : INetworkData
    /// </summary>
    public interface INetworkData
    {
        /// <summary>
        /// Serializes the object to a byte array for network transmission
        /// </summary>
        /// <returns>Byte array representation of the object</returns>
        byte[] ToNetwork();
    }
}
