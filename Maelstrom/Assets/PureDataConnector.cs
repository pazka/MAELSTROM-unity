using System;
using UnityEngine;
using Maelstrom.Unity;

namespace Maelstrom.Unity
{
    public class PureDataConnector : MonoBehaviour
    {
        private OSC osc;

        private void Start()
        {
            OpenConnection();
        }

        private void Update()
        {
            if (!IsOpen())
                return;

            osc.Update();
        }

        public void OnDestroy()
        {
            if (!IsOpen())
                return;

            osc.OnDestroy();
        }

        private void OpenConnection()
        {
            try
            {
                osc = new OSC(10301, "127.0.0.1", 10300);
                Debug.Log("Connected to Pure Data client !");

                var oscMess = new OscMessage();
                oscMess.address = "/Test";
                oscMess.values.Add("Hello");
                oscMess.values.Add(DateTime.Now.Millisecond);
                Send(oscMess);
            }
            catch (Exception e)
            {
                osc = null;
                Debug.Log(e.Message);
                throw;
            }
        }

        public bool IsOpen()
        {
            return osc != null && osc.IsOpen();
        }

        public void Send(OscMessage message)
        {
            if (!IsOpen())
                Debug.Log("Error when trying to send a message, the connection is not open")
                    ;
            osc.Send(message);
        }

        public void SendOscMessage(string address, int value)
        {
            Send(new OscMessage(address, value));
        }

        public void SendOscMessage(string address, float value)
        {
            Send(new OscMessage(address, value));
        }

        public void SendOscMessage(string address, string value)
        {
            Send(new OscMessage(address, value));
        }
    }
}