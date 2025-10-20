using System;
using System.Collections.Generic;

namespace Maelstrom.Unity
{
    public interface IMaelstromUdpService : IDisposable
    {

        // Set local role: 1=corals, 2=ghostNet, 3=feed
        void SetLocalRole(ushort roleId);

        // Publish current local role maelstrom as 2-byte role + 4-byte float
        void PublishCurrenMaelstrom(float maelstrom);

        // Update local cache without broadcasting
        void SetLocalMaelstrom(string key, float value);

        // Returns external maelstrom value for the specified key
        float[] GetExternalMaelstroms();

        // Start/Stop background receive loop
        void Start();
        void Stop();
    }
}


