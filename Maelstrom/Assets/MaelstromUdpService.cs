using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Maelstrom.Unity
{
    public class MaelstromUdpService : IMaelstromUdpService
    {
        private const int DefaultPort = 5000;
        private const string DefaultMulticastAddressV4 = "192.168.1.255";
        private const string DefaultMulticastAddressV6 = "ff02::1"; // site-local, transient

        // Dual-stack: separate clients for IPv4 and IPv6
        private readonly UdpClient udpClientV4;
        private readonly UdpClient udpClientV6;
        private readonly IPEndPoint multicastEndpointV4;
        private readonly IPEndPoint multicastEndpointV6;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly Task receiveLoopTaskV4;
        private readonly Task receiveLoopTaskV6;
        private readonly ConcurrentDictionary<string, float> externalMaelstrom = new ConcurrentDictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private float localMaelstrom = 0f;
        private ushort localRoleId = 0; // 1=corals,2=ghostNet,3=feed

        public MaelstromUdpService(int port = DefaultPort, string multicastAddressV4 = DefaultMulticastAddressV4, string multicastAddressV6 = DefaultMulticastAddressV6)
        {
            // IPv4 client (broadcast)
            udpClientV4 = new UdpClient(AddressFamily.InterNetwork);
            udpClientV4.ExclusiveAddressUse = false;
            udpClientV4.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClientV4.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            udpClientV4.EnableBroadcast = true; // allow broadcast
            multicastEndpointV4 = new IPEndPoint(IPAddress.Parse(multicastAddressV4), port); // keep 192.168.1.255

            // IPv6 client - must bind to port before joining multicast group
            udpClientV6 = new UdpClient(AddressFamily.InterNetworkV6);
            udpClientV6.ExclusiveAddressUse = false;
            udpClientV6.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, port));

            var mAddrV6 = IPAddress.Parse(multicastAddressV6);
            // Join IPv6 multicast group with interface index 0 (default interface)
            udpClientV6.JoinMulticastGroup(0, mAddrV6);
            multicastEndpointV6 = new IPEndPoint(mAddrV6, port);


            receiveLoopTaskV4 = Task.Run(ReceiveLoopV4Async);
            receiveLoopTaskV6 = Task.Run(ReceiveLoopV6Async);
        }

        public void Start() { /* auto-starts in ctor */ }

        public void Stop()
        {
            cts.Cancel();
        }

        public IReadOnlyDictionary<string, float> getExternalMaestrom()
        {
            return new Dictionary<string, float>(externalMaelstrom);
        }

        public void PublishCurrenMaelstrom(float maelstrom)
        {
            localMaelstrom = Clamp01(maelstrom);
            var payload = EncodeBinary(localRoleId, maelstrom);
            if (payload == null) return;
            try { udpClientV4.Send(payload, payload.Length, multicastEndpointV4); } catch { }
            try { udpClientV6.Send(payload, payload.Length, multicastEndpointV6); } catch { }
        }

        public void SetLocalMaelstrom(string key, float value)
        {
            if (!IsAcceptedKey(key)) return;
            localMaelstrom = Clamp01(value);
        }

        public void SetLocalRole(ushort roleId)
        {
            localRoleId = roleId;
        }

        public float[] GetExternalMaelstroms()
        {
            var values = externalMaelstrom.Values;
            var result = new float[values.Count];
            int i = 0;
            foreach (var v in values)
            {
                result[i++] = Clamp01(v);
            }
            return result;
        }

        public IReadOnlyDictionary<string, float> GetAllMaelstroms()
        {
            var allMaelstroms = new Dictionary<string, float>(externalMaelstrom);

            if (localRoleId != 0)
            {
                string localKey = RoleToKey(localRoleId);
                if (localKey != null)
                {
                    allMaelstroms[localKey] = localMaelstrom;
                }
            }

            return allMaelstroms;
        }

        private async Task ReceiveLoopV4Async()
        {
            using (udpClientV4)
            {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        var result = await udpClientV4.ReceiveAsync();
                        DecodeAndIntegrateBinary(result.Buffer);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"UDP IPv4 receive error: {ex.Message}");
                    }
                }
            }
        }

        private async Task ReceiveLoopV6Async()
        {
            using (udpClientV6)
            {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        var result = await udpClientV6.ReceiveAsync();
                        DecodeAndIntegrateBinary(result.Buffer);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"UDP IPv6 receive error: {ex.Message}");
                    }
                }
            }
        }

        private void DecodeAndIntegrateBinary(byte[] buffer)
        {
            try
            {
                if (buffer == null || buffer.Length != 6) return;
                ushort role = (ushort)((buffer[0] << 8) | buffer[1]);
                float value = BitConverter.ToSingle(new byte[] { buffer[5], buffer[4], buffer[3], buffer[2] }, 0); // network big-endian to little-endian

                string key = RoleToKey(role);
                if (key == null || role == this.localRoleId) return;

                var extVal = Clamp01(value);
                externalMaelstrom[key] = extVal;

                // Debug.Log($"UDP : got {key} : {extVal}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"UDP decode error: {ex.Message}");
            }
        }

        private byte[] EncodeBinary(ushort roleId, float value)
        {
            if (roleId == 0) return null;
            string key = RoleToKey(roleId);
            if (key == null) return null;

            value = Clamp01(value);

            var bytes = new byte[6];
            bytes[0] = (byte)((roleId >> 8) & 0xFF); // big-endian role
            bytes[1] = (byte)(roleId & 0xFF);
            var floatLE = BitConverter.GetBytes(value); // little-endian on most platforms
            bytes[2] = floatLE[3];
            bytes[3] = floatLE[2];
            bytes[4] = floatLE[1];
            bytes[5] = floatLE[0];
            return bytes;
        }

        private static string RoleToKey(ushort role)
        {
            if (role == 1) return "corals";
            if (role == 2) return "ghostNet";
            if (role == 3) return "feed";
            return null;
        }

        private static bool IsAcceptedKey(string key)
        {
            return string.Equals(key, "corals", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "ghostNet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "feed", StringComparison.OrdinalIgnoreCase);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public void Dispose()
        {
            Stop();
            udpClientV4.Dispose();
            udpClientV6.Dispose();
            cts.Dispose();
        }
    }
}