using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Generic UDP service for network communication. Binds to a listen port and sends to one or more destination ports
    ///     (Option 2: multiple ports).
    /// </summary>
    public class UdpService : IUdpService
    {
        private const int DefaultPort = 5000;
        private const string DefaultMulticastAddressV4 = "192.168.1.255";
        private const string DefaultMulticastAddressV6 = "ff02::1";
        private readonly CancellationTokenSource cts = new();
        private readonly Task receiveLoopTaskV4;
        private readonly Task receiveLoopTaskV6;
        private readonly IReadOnlyList<IPEndPoint> sendEndpointsV4;
        private readonly IReadOnlyList<IPEndPoint> sendEndpointsV6;

        private readonly UdpClient udpClientV4;
        private readonly UdpClient udpClientV6;

        /// <param name="listenPort">Port this process binds to for receiving.</param>
        /// <param name="destinationPorts">Ports to send to (broadcast/multicast). If null or empty, sends only to listenPort.</param>
        /// <param name="multicastAddressV4">IPv4 broadcast/multicast address. If null, read from Config (broadastIPv4Adress).</param>
        /// <param name="multicastAddressV6">IPv6 multicast address. If null, read from Config (multicastIPv6Adress).</param>
        public UdpService(int listenPort = DefaultPort, int[] destinationPorts = null,
            string multicastAddressV4 = null,
            string multicastAddressV6 = null)
        {
            string addrV4Str = multicastAddressV4 ?? Config.Get("broadastIPv4Adress", DefaultMulticastAddressV4);
            string addrV6Str = multicastAddressV6 ?? Config.Get("multicastIPv6Adress", DefaultMulticastAddressV6);

            AppLogger.Log($"Starting server : {listenPort}");
            var ports = destinationPorts != null && destinationPorts.Length > 0
                ? destinationPorts.Distinct().ToArray()
                : new[] { listenPort };

            var addrV4 = IPAddress.Parse(addrV4Str);
            var addrV6 = IPAddress.Parse(addrV6Str);
            sendEndpointsV4 = ports.Select(p => new IPEndPoint(addrV4, p)).ToList();
            sendEndpointsV6 = ports.Select(p => new IPEndPoint(addrV6, p)).ToList();

            udpClientV4 = new UdpClient(AddressFamily.InterNetwork);
            udpClientV4.ExclusiveAddressUse = false;
            udpClientV4.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClientV4.Client.Bind(new IPEndPoint(IPAddress.Any, listenPort));
            udpClientV4.EnableBroadcast = true;

            udpClientV6 = new UdpClient(AddressFamily.InterNetworkV6);
            udpClientV6.ExclusiveAddressUse = false;
            udpClientV6.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, listenPort));
            udpClientV6.JoinMulticastGroup(0, addrV6);

            receiveLoopTaskV4 = Task.Run(ReceiveLoopV4Async);
            receiveLoopTaskV6 = Task.Run(ReceiveLoopV6Async);
        }

        public event Action<byte[]> OnDataReceived;

        public void Start()
        {
        }

        public void Stop()
        {
            cts.Cancel();
        }

        public void Send(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            foreach (var ep in sendEndpointsV4)
                try
                {
                    udpClientV4.Send(data, data.Length, ep);
                }
                catch
                {
                }

            foreach (var ep in sendEndpointsV6)
                try
                {
                    udpClientV6.Send(data, data.Length, ep);
                }
                catch
                {
                }
        }

        public void Dispose()
        {
            Stop();
            udpClientV4?.Dispose();
            udpClientV6?.Dispose();
            cts?.Dispose();
        }

        private async Task ReceiveLoopV4Async()
        {
            using (udpClientV4)
            {
                while (!cts.IsCancellationRequested)
                    try
                    {
                        var result = await udpClientV4.ReceiveAsync();
                        if (result.Buffer != null && result.Buffer.Length > 0) OnDataReceived?.Invoke(result.Buffer);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogWarning($"UDP IPv4 receive error: {ex.Message}");
                    }
            }
        }

        private async Task ReceiveLoopV6Async()
        {
            using (udpClientV6)
            {
                while (!cts.IsCancellationRequested)
                    try
                    {
                        var result = await udpClientV6.ReceiveAsync();
                        if (result.Buffer != null && result.Buffer.Length > 0) OnDataReceived?.Invoke(result.Buffer);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogWarning($"UDP IPv6 receive error: {ex.Message}");
                    }
            }
        }
    }
}