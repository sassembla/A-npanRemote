using UnityEngine;
using System.Net.WebSockets;
using System.Net;
using Fleck;
using System.Collections.Generic;
using System;
using UnityEditor;
using System.Text;
using System.Net.Sockets;
using System.Threading;

[InitializeOnLoad]
public class Server
{

    static Server()
    {
        var first = true;
        Action serverStop = null;
        EditorApplication.update += () =>
        {
            var a = Application.isPlaying;
            var b = EditorApplication.isPlaying;
            var c = EditorApplication.isPlayingOrWillChangePlaymode;
            if (a && b && c)
            {
                if (first)
                {
                    first = false;
                    serverStop = StartServer();
                }
            }

            if (a && b && !c)
            {
                serverStop?.Invoke();
            }
        };
    }

    private static Action StartServer()
    {
        IWebSocketConnection relaySocket = null;
        try
        {
            var server = new WebSocketServer("ws://0.0.0.0:1129");
            FleckLog.Level = LogLevel.Debug;
            server.Start(
                socket =>
                {
                    socket.OnOpen = () =>
                    {
                        if (socket.ConnectionInfo.Headers.ContainsKey("receiver"))
                        {
                            relaySocket = socket;
                        }
                    };
                    socket.OnClose = () => { };
                    socket.OnMessage = message =>
                    {
                        relaySocket?.Send(Encoding.UTF8.GetBytes(message));
                    };
                }
            );

            Action serverStop = () =>
            {
                server?.Dispose();
            };
            return serverStop;
        }
        catch (Exception e)
        {
            Debug.LogError("e:" + e);
            return null;
        }
    }
}