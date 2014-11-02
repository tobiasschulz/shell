﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shell.Common;
using Shell.Common.IO;
using Shell.Common.Tasks;
using Shell.Common.Util;

namespace Shell.HolePunching
{
    public class HolePunchingUtil : Library
    {
        public static int KEEP_ALIVE_TIMEOUT_MS = 10000;
        private readonly string SECTION = "Peer";

        public static readonly Random Random = new Random ();

        public HolePunchingUtil ()
        {
            ConfigName = "HolePunching";
        }

        public static async Task RedirectEverything (UdpConnection udp, TcpClient tcp)
        {
            Log.Debug ("RedirectEverything: start...");
            bool running = true;
            List<Task> tasks = new List<Task> ();
            CancellationTokenSource source = new CancellationTokenSource ();
            Action cancel = () => source.Cancel ();
            udp.OnDisconnect += cancel;

            tasks.Add (Task.Run (async () => {
                while (running) {
                    Packet packet = await udp.ReceiveAsync ();

                    await tcp.GetStream ().WriteAsync (buffer: packet.Buffer, offset: 0, count: packet.Buffer.Length);
                    Log.Debug ("Forward (udp -> tcp): ", packet.Buffer.Length, " bytes");
                }
            }, source.Token));

            tasks.Add (Task.Run (async () => {
                byte[] buffer = new byte[1024];

                while (running) {
                    int bytesRead = await tcp.GetStream ().ReadAsync (buffer, 0, (int)buffer.Length);
                    if (bytesRead > 0) {
                        udp.Send (buffer, bytesRead);
                        Log.Debug ("Forward (tcp -> udp): ", bytesRead, " bytes");
                    }
                }
            }, source.Token));

            tasks.Add (Task.Run (async () => {
                while (running) {
                    if (!tcp.IsStillConnected ()) {
                        Log.Error ("TCP has disconnected: ", tcp);
                        running = false;
                        udp.Disconnect ();
                        source.CancelAfter (1000);
                    }
                    await Task.Delay (100);
                }
            }, source.Token));

            udp.SendKeepAlivePackets (() => running, source.Token);

            Log.Debug ("RedirectEverything: running...");

            await Task.WhenAll (tasks);

            Log.Debug ("RedirectEverything: stopped.");
            udp.OnDisconnect -= cancel;
        }

        public void ReadConfig (out string peer, out int myoffset, out int peeroffset)
        {
            ConfigFile config = fs.Config.OpenConfigFile ("peer.ini");

            peer = "";
            myoffset = 0;
            peeroffset = 0;

            int i = 0;
            while (true) {
                peer = config [SECTION, "peer_hostname", ""];
                myoffset = config.GetOptionInt (SECTION, "local_portoffset", 0);
                peeroffset = config.GetOptionInt (SECTION, "peer_portoffset", 0);

                if (peer == "" || myoffset == 0 || peeroffset == 0 || myoffset == peeroffset) {
                    if (Commons.CurrentPlatform == Platforms.Linux) {
                        if (i == 0) {
                            Log.Debug ("Linux...");
                            Process.Start (@"gedit", config.Filename);
                        }
                    } else {
                        if (i == 0) {
                            Log.Debug ("Windows...");
                            Process.Start (@"notepad.exe", config.Filename);
                        }
                    }

                    Thread.Sleep (1000);
                    config.Reload ();
                    ++i;
                } else {
                    break;
                }
            }
        }
    }
}

