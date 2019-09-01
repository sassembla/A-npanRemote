using UnityEngine;
using System;
using UnityEditor;
using System.IO;
using WebuSocketCore.Server;

[InitializeOnLoad]
public class Server
{
    private enum ServerState
    {
        None,
        Running
    };
    private static ServerState serverState = ServerState.None;


    private static bool shot = false;
    private static bool waitingStored = false;

    static Server()
    {
        var first = true;
        Action serverStop = null;
        var fileName = "pa.png";
        var jpgTex = new Texture2D(1, 1);

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
                    serverState = ServerState.Running;
                    serverStop = StartServer();
                }
            }

            if (a && b && !c)
            {
                serverState = ServerState.None;
                serverStop?.Invoke();
            }

            if (serverState == ServerState.Running)
            {
                return;
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                var jpgBytes = tex.EncodeToJPG(10);// 90KB
                using (var sw = new StreamWriter(fileName))
                {
                    sw.BaseStream.Write(jpgBytes, 0, jpgBytes.Length);
                }
                // remoteSocket?.Send(jpgBytes);
            }

        };
    }

    [MenuItem("Window/TakeScreenshotWithCaptureScreenshotAsTexture")]
    public static void Menu()
    {
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        var jpgBytes = tex.EncodeToJPG(10);// 90KB
        using (var sw = new StreamWriter("a.jpg"))
        {
            sw.BaseStream.Write(jpgBytes, 0, jpgBytes.Length);
        }
    }

    private static Action StartServer()
    {
        ClientConnection localSocket = null;
        ClientConnection remoteSocket = null;
        var server = new WebuSocketServer(
            1129,
            newConnection =>
            {
                if (newConnection.RequestHeaderDict.ContainsKey("local"))
                {
                    localSocket = newConnection;
                    newConnection.OnMessage = segments =>
                    {
                        Debug.Log("local");
                        while (0 < segments.Count)
                        {
                            var data = segments.Dequeue();
                            var bytes = new byte[data.Count];
                            Buffer.BlockCopy(data.Array, data.Offset, bytes, 0, data.Count);

                            remoteSocket?.Send(bytes);

                            // count++;

                            // // データがきたタイミングでフレームがいい感じだったらスクショを送り出す
                            // if (count % 10 == 0)
                            // {
                            //     shot = true;
                            //     count = 0;
                            // }
                        }
                    };
                }
                else
                {
                    Debug.Log("remote");
                    remoteSocket = newConnection;
                    newConnection.OnMessage = segments =>
                    {
                        while (0 < segments.Count)
                        {
                            var data = segments.Dequeue();
                            var bytes = new byte[data.Count];
                            Buffer.BlockCopy(data.Array, data.Offset, bytes, 0, data.Count);

                            localSocket?.Send(bytes);
                        }
                    };
                }
            }
        );

        /*
            IWebSocketConnection localSocket = null;
            var server = new WebSocketServer("ws://0.0.0.0:1129");
            FleckLog.Level = LogLevel.Info;
            server.Start(
                socket =>
                {
                    socket.OnOpen = () =>
                    {
                        if (socket.ConnectionInfo.Headers.ContainsKey("receiver"))
                        {
                            localSocket = socket;
                        }
                        else
                        {
                            remoteSocket = socket;
                        }
                    };
                    socket.OnBinary = message =>
                    {
                        if (socket == localSocket)
                        {
                            remoteSocket?.Send(message);
                            return;
                        }

                        localSocket?.Send(message);

                        count++;

                        // データがきたタイミングでフレームがいい感じだったらスクショを送り出す
                        if (count % 10 == 0)
                        {
                            shot = true;
                            count = 0;
                        }
                    };
                }
            );
         */

        Action serverStop = () =>
        {
            server?.Dispose();
        };
        return serverStop;
    }
}