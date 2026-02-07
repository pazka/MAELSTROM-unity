using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Network communication abstraction that allows sending and receiving arbitrary typed data with tags
    /// </summary>
    public class NetworkManager
    {
        private static NetworkManager _instance;
        private static int[] _destinationPorts;

        private readonly object _callbackLock = new();
        private readonly ConcurrentDictionary<DataTag, List<Delegate>> _callbacks = new();
        private readonly ConcurrentQueue<Action> _pendingCallbacks = new();
        private byte[] _lastLogsPayload;
        private IUdpService _udpService;

        private NetworkManager()
        {
        }

        /// <summary>
        ///     Gets or creates the singleton instance of NetworkManager (uses Initialize config if set, else port 5000).
        /// </summary>
        public static NetworkManager Instance
        {
            get
            {
                if (_instance == null) _instance = new NetworkManager();
                return _instance;
            }
        }

        /// <summary>
        ///     Configures listen and destination ports before first use. Call once at startup (e.g. debug display: 5000, dest
        ///     5001,5002,5003; program A: 5001, dest 5000,5002,5003).
        /// </summary>
        /// <param name="listenPort">Port this app binds to for receiving.</param>
        /// <param name="destinationPorts">Ports to send to (other apps). If null, sends only to listenPort.</param>
        public void Initialize(int listenPort, int[] destinationPorts = null)
        {
            _destinationPorts = destinationPorts;

            _udpService = new UdpService(listenPort, _destinationPorts);
            _udpService.OnDataReceived += HandleUdpDataReceived;
            _udpService.Start();

            AppLogger.Log($"Config keys: {string.Join(", ", Config.GetAllKeys())}");
        }

        private void HandleUdpDataReceived(byte[] buffer)
        {
            if (buffer == null || buffer.Length < 4) return;

            HandleGenericDataReceived(buffer);
        }

        private static bool PayloadEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        /// <summary>
        ///     Sends data over the network with the specified tag
        /// </summary>
        /// <typeparam name="T">Type of data to send, must implement INetworkData</typeparam>
        /// <param name="tag">Tag identifying the type of data</param>
        /// <param name="data">Data object to send</param>
        public void SendNetwork<T>(DataTag tag, T data) where T : INetworkData
        {
            if (data == null)
            {
                AppLogger.LogWarning($"Cannot send null data for tag {tag}");
                return;
            }

            if (_udpService == null)
                return;

            try
            {
                var payload = data.ToNetwork();
                if (payload == null || payload.Length == 0)
                {
                    AppLogger.LogWarning($"Empty payload for tag {tag}");
                    return;
                }

                var message = EncodeMessage(tag, payload);
                _udpService.Send(message);
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Error sending network data for tag {tag}: {ex.Message}");
            }
        }

        /// <summary>
        ///     Registers a callback to receive data of the specified type and tag
        /// </summary>
        /// <typeparam name="T">Type of data to receive, must implement INetworkData</typeparam>
        /// <param name="tag">Tag identifying the type of data</param>
        /// <param name="callback">Callback to invoke when data is received</param>
        public void ListenNetwork<T>(DataTag tag, Action<T> callback) where T : INetworkData, new()
        {
            if (callback == null)
            {
                AppLogger.LogWarning($"Cannot register null callback for tag {tag}");
                return;
            }

            lock (_callbackLock)
            {
                if (!_callbacks.ContainsKey(tag)) _callbacks[tag] = new List<Delegate>();
                _callbacks[tag].Add(callback);
            }
        }

        /// <summary>
        ///     Unregisters a callback for the specified tag
        /// </summary>
        /// <typeparam name="T">Type of data</typeparam>
        /// <param name="tag">Tag identifying the type of data</param>
        /// <param name="callback">Callback to unregister</param>
        public void UnListenNetwork<T>(DataTag tag, Action<T> callback) where T : INetworkData
        {
            if (callback == null) return;

            lock (_callbackLock)
            {
                if (_callbacks.TryGetValue(tag, out var callbacks))
                {
                    callbacks.Remove(callback);
                    if (callbacks.Count == 0) _callbacks.TryRemove(tag, out _);
                }
            }
        }


        private void InvokeCallbacks(DataTag tag, byte[] payload)
        {
            if (!_callbacks.TryGetValue(tag, out var callbacks)) return;

            List<Delegate> callbacksToInvoke;
            lock (_callbackLock)
            {
                callbacksToInvoke = new List<Delegate>(callbacks);
            }

            foreach (var callback in callbacksToInvoke)
                try
                {
                    var deserialized = DeserializeData(callback, payload);
                    if (deserialized != null) _pendingCallbacks.Enqueue(() => callback.DynamicInvoke(deserialized));
                }
                catch (Exception ex)
                {
                    AppLogger.LogError($"Error preparing callback for tag {tag}: {ex.Message}");
                }
        }

        /// <summary>
        ///     Processes queued callbacks on the main thread. Should be called from Unity's Update or similar main thread method.
        /// </summary>
        public void ProcessCallbacks()
        {
            var processed = 0;
            const int maxPerFrame = 100;

            while (_pendingCallbacks.TryDequeue(out var callback) && processed < maxPerFrame)
            {
                try
                {
                    callback?.Invoke();
                }
                catch (Exception ex)
                {
                    AppLogger.LogError($"Error invoking queued callback: {ex.Message}");
                }

                processed++;
            }
        }

        private object DeserializeData(Delegate callback, byte[] payload)
        {
            var method = callback.Method;
            var parameters = method.GetParameters();

            if (parameters.Length != 1)
            {
                AppLogger.LogWarning("Callback for network data must have exactly one parameter");
                return null;
            }

            var parameterType = parameters[0].ParameterType;

            if (!typeof(INetworkData).IsAssignableFrom(parameterType))
            {
                AppLogger.LogWarning($"Callback parameter type {parameterType.Name} must implement INetworkData");
                return null;
            }

            try
            {
                var fromNetworkMethod = parameterType.GetMethod("FromNetwork",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(byte[]) },
                    null);

                if (fromNetworkMethod == null)
                {
                    AppLogger.LogWarning($"Type {parameterType.Name} must have a static FromNetwork(byte[]) method");
                    return null;
                }

                var deserialized = fromNetworkMethod.Invoke(null, new object[] { payload });

                if (deserialized != null && parameterType.IsAssignableFrom(deserialized.GetType())) return deserialized;

                AppLogger.LogWarning($"FromNetwork method for {parameterType.Name} returned invalid type");
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Error deserializing {parameterType.Name}: {ex.Message}");
                return null;
            }
        }

        private byte[] EncodeMessage(DataTag tag, byte[] payload)
        {
            var tagValue = (ushort)tag;
            var payloadLength = (ushort)payload.Length;

            var message = new byte[4 + payload.Length];
            BitConverter.GetBytes(tagValue).CopyTo(message, 0);
            BitConverter.GetBytes(payloadLength).CopyTo(message, 2);
            payload.CopyTo(message, 4);

            return message;
        }

        /// <summary>
        ///     Handles generic data received from UDP service (called internally)
        /// </summary>
        internal void HandleGenericDataReceived(byte[] buffer)
        {
            if (buffer == null || buffer.Length < 4) return;

            try
            {
                var tag = (DataTag)BitConverter.ToUInt16(buffer, 0);
                var payloadLength = BitConverter.ToUInt16(buffer, 2);

                if (buffer.Length < 4 + payloadLength)
                {
                    AppLogger.LogWarning(
                        $"Invalid message length for tag {tag}. Expected {4 + payloadLength}, got {buffer.Length}");
                    return;
                }

                var payload = new byte[payloadLength];
                Array.Copy(buffer, 4, payload, 0, payloadLength);

                if (tag == DataTag.Logs && PayloadEquals(_lastLogsPayload, payload))
                    return;

                if (tag == DataTag.Logs)
                    _lastLogsPayload = payload;

                InvokeCallbacks(tag, payload);
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Error handling generic data: {ex.Message}");
            }
        }

        /// <summary>
        ///     Cleans up resources and unregisters from UDP service events
        /// </summary>
        public void Dispose()
        {
            if (_udpService != null)
            {
                _udpService.OnDataReceived -= HandleUdpDataReceived;
                _udpService.Stop();
                _udpService.Dispose();
            }

            lock (_callbackLock)
            {
                _callbacks.Clear();
            }
        }
    }
}