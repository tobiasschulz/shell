﻿using System;
using Shell.Common.Tasks;
using System.Net;
using System.Net.Sockets;
using Shell.Common;
using Shell.Common.IO;
using Shell.Common.Util;
using System.Diagnostics;
using System.Threading;
using System.Text;

namespace Shell.HolePunching
{
    public class PortForwardServerTask : Task, MainTask
    {
        public PortForwardServerTask ()
        {
            Name = "HolePunching";
            Description = "Forward a port (server)";
            Options = new string[] { "hole-punching-port-forward-server", "hp-pf-server" };
            ConfigName = "HolePunching";
            ParameterSyntax = "";
        }

        protected override void InternalRun (string[] args)
        {
            string peer;
            int myoffset;
            int peeroffset;
            new HolePunchingLibrary ().ReadConfig (peer: out peer, myoffset: out myoffset, peeroffset: out peeroffset);

            ushort myport = NetworkUtils.CurrentPort (myoffset);
            ushort peerport = NetworkUtils.CurrentPort (peeroffset);

            NatTraverse nattra = new NatTraverse (localPort: myport, remoteHost: peer, remotePort: peerport);
            UdpClient sock;
            nattra.Punch (out sock);
            IPEndPoint remote = nattra.RemoteEndPoint;

            ushort targetPort;
            if (GetTarget (sock: sock, targetPort: out targetPort)) {
                TcpClient tcpSock;
                if (ConnectTcp (port: targetPort, sock: out tcpSock)) {
                    ForwardPort (udp: sock, udpRemote: remote, tcp: tcpSock);
                } else {
                    Log.Error ("Unable to connect to tcp target.");
                }
            } else {
                Log.Error ("Unable to get target port.");
            }
        }

        bool GetTarget (UdpClient sock, out ushort targetPort)
        {
            bool running = true;
            bool success = false;
            int _targetPort = 0;

            System.Threading.Tasks.Task.Run (async () => {
                while (running) {
                    UdpReceiveResult receivedResults = await sock.ReceiveAsync ();
                    string receivedString = Encoding.ASCII.GetString (receivedResults.Buffer).Trim ();
                    if (receivedString.StartsWith ("TARGET:")) {
                        string target = receivedString.Substring (7);
                        Log.Message ("Received target: ", target);
                        if (int.TryParse (target, out _targetPort)) {
                            running = false;
                            success = true;
                        } else {
                            Log.Error ("Invalid target port: ", target);
                            running = false;
                            success = false;
                        }
                    } else {
                        Log.Debug ("Received shit while waiting for target: ", receivedString);
                    }
                }
            });

            while (running) {
                Thread.Sleep (100);
            }
            targetPort = (ushort)_targetPort;

            return success;
        }

        bool ConnectTcp (ushort port, out TcpClient tcpSock)
        {
            try {
                tcpSock = new TcpClient ();
                tcpSock.Connect ("127.0.0.1", port);
                return true;
            } catch (Exception ex) {
                Log.Error (ex);
                return false;
            }
        }

        void ForwardPort (UdpClient udp, IPEndPoint udpRemote, TcpClient tcp)
        {
            bool running = true;

            System.Threading.Tasks.Task.Run (async () => {
                while (running) {
                    UdpReceiveResult receivedResults = await udp.ReceiveAsync ();
                    tcp.GetStream ().WriteAsync (buffer: receivedResults.Buffer, offset: 0, count: receivedResults.Buffer.Length);
                    Log.Debug ("Forward (udp -> tcp): ", receivedResults.Buffer.Length, " bytes");
                }
            });

            System.Threading.Tasks.Task.Run (async () => {
                byte[] buffer = new byte[8 * 1024];

                while (running) {
                    int bytesRead = await tcp.GetStream ().ReadAsync (buffer, 0, (int)buffer.Length);
                    await udp.SendAsync(buffer, bytesRead, udpRemote);
                }
            });


            while (running) {
                Thread.Sleep (1000);
            }
        }
    }
}

