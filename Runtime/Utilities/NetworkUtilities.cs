using System;
using System.Collections;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace BCI2000
{
    public class TcpHostPing
    {
        public enum Status {Idle, Busy, Success, Failure}

        public bool Completed => status is Status.Success or Status.Failure;
        public bool Succeeded => status is Status.Success;
        public Status status {get; private set;}

        private Thread pingThread;


        public void PingAsync(string address, int port)
        {
            if (status is Status.Busy)
            {
                Debug.Log("There is already a live ping thread, ignoring...");
                return;
            }
            status = Status.Busy;
            pingThread = new (() => PingHost(address, port));
            pingThread.Start();
        }

        private void PingHost(string address, int port)
        {
            try {
                TcpClient client = new(address, port);
                status = Status.Success;
            }
            catch {
                status = Status.Failure;
            }
        }
    }


    public static class NetworkExtensions
    {
        public static void ExecuteWhenHostAvailable
        (
            this MonoBehaviour behavior,
            string address, int port, Action callback
        )
        => behavior.StartCoroutine(
            RunExecuteWhenHostAvailable(callback, address, port)
        );

        private static IEnumerator RunExecuteWhenHostAvailable
        (
            Action callback, string address, int port
        )
        {
            TcpHostPing ping = new();
            
            while (!ping.Succeeded)
            {
                ping.PingAsync(address, port);
                while (!ping.Completed)
                    yield return new WaitForEndOfFrame();
            }

            callback();
        }


        public static bool IsConnected(this TcpClient client)
        {
            return client.GetState() is not (
                TcpState.Unknown or TcpState.Closed or
                TcpState.Closing or TcpState.CloseWait
            );
        }

        public static TcpState GetState(this TcpClient client)
        {
            TcpConnectionInformation matchingConnection
            = IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpConnections()
                .SingleOrDefault(x => x.LocalEndPoint.Equals(
                    client.Client.LocalEndPoint
                    )
                );
            return matchingConnection?.State ?? TcpState.Unknown;
        }
    }
}