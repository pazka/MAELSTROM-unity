using System;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Generic UDP service interface for network communication
    /// </summary>
    public interface IUdpService : IDisposable
    {
        /// <summary>
        /// Event fired when generic data is received
        /// </summary>
        event Action<byte[]> OnDataReceived;

        /// <summary>
        /// Starts the UDP service
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the UDP service
        /// </summary>
        void Stop();

        /// <summary>
        /// Sends arbitrary byte array data over UDP
        /// </summary>
        /// <param name="data">Byte array to send</param>
        void Send(byte[] data);
    }
}
