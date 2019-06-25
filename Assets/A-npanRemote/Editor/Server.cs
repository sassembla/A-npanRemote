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
using System.IO;

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

    private static IWebSocketConnection remoteSocket;

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
                if (true)
                {
                    return;
                }

                if (true)
                {
                    // 作ってみたけどクッソ重い。うーん。CaptureScreenshotAsTextureがバグっているのが悪い。
                    if (waitingStored)
                    {
                        waitingStored = false;
                        using (var sr = new StreamReader(fileName))
                        {
                            var buffer = new byte[sr.BaseStream.Length];
                            sr.BaseStream.Read(buffer, 0, buffer.Length);
                            // Debug.Log("buffer:" + buffer.Length);// この時点で1.995mBもあるので、jpgに変換する。

                            jpgTex.LoadImage(buffer);
                            var jpgBytes = jpgTex.EncodeToJPG(10);// 90KB
                            remoteSocket?.Send(jpgBytes);
                        }
                        File.Delete(fileName);
                    }
                    if (shot)
                    {
                        shot = false;
                        ScreenCapture.CaptureScreenshot(fileName);
                        waitingStored = true;
                    }
                }
                else
                {
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    var jpgBytes = tex.EncodeToJPG(10);// 90KB
                    using (var sw = new StreamWriter(fileName))
                    {
                        sw.BaseStream.Write(jpgBytes, 0, jpgBytes.Length);
                    }
                    remoteSocket?.Send(jpgBytes);
                }
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
        try
        {
            var count = 0;
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