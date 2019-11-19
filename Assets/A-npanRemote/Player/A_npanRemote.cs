using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using WebuSocketCore;
using AutoyaFramework.Persistence.Files;
using Chanquo.v2;

public class A_npanRemote : IDisposable
{
    private bool isRemoteConnected;
    private WebuSocket ws = null;
    private static A_npanRemote _this;


    [System.Diagnostics.Conditional("REMOTE")]
    public static void LatestConnectionRecord(ref string record)
    {
        var fp = new FilePersistence(Application.persistentDataPath);
        if (fp.IsExist("connectionRecord", "latestIp"))
        {
            record = fp.Load("connectionRecord", "latestIp");
            return;
        }
        record = "";
    }



    [System.Diagnostics.Conditional("REMOTE")]
    public static void Setup<P0, PayloadType>(string ip, ref Action<P0> onSend, Action<P0> onReceived) where PayloadType : IRemotePayload1
    {
        _this = new A_npanRemote();
#if UNITY_EDITOR
        // エディタの場合、セットアップと受け取り時の処理のセットアップを行う。
        _this.SetupEditorConnection1<P0, PayloadType>(onReceived);
#else
        // エディタ以外であれば、特定のIPへと接続を行う。
        _this.SetupRemoteConnection1<P0, PayloadType>(ip, ref onSend, onReceived);
#endif
    }

    [System.Diagnostics.Conditional("REMOTE")]
    public static void Setup<P0, P1, PayloadType>(string ip, ref Action<P0, P1> onSend, Action<P0, P1> onReceived) where PayloadType : IRemotePayload2
    {
        _this = new A_npanRemote();
#if UNITY_EDITOR
        // エディタの場合、セットアップと受け取り時の処理のセットアップを行う。
        _this.SetupEditorConnection2<P0, P1, PayloadType>(onReceived);
#else
        // エディタ以外であれば、特定のIPへと接続を行う。
        _this.SetupRemoteConnection2<P0, P1, PayloadType>(ip, ref onSend, onReceived);
#endif
    }

    [System.Diagnostics.Conditional("REMOTE")]
    public static void Setup<P0, P1, P2, PayloadType>(string ip, ref Action<P0, P1, P2> onSend) where PayloadType : IRemotePayload3
    {
        _this = new A_npanRemote();
#if UNITY_EDITOR
        // エディタの場合、セットアップと受け取り時の処理のセットアップを行う。
        _this.SetupEditorConnection3<P0, P1, P2, PayloadType>(onSend);
#else
        // エディタ以外であれば、特定のIPへと接続を行う。
        _this.SetupRemoteConnection3<P0, P1, P2, PayloadType>(ip, ref onSend);
#endif
    }






    public static void Teardown()
    {
        _this?.Dispose();
    }



    private void SetupRemoteConnection1<T, W>(string ip, ref Action<T> onData, Action<T> onReceived) where W : IRemotePayload1
    {
        // onDataの書き換えを行う
        onData = (t) =>
        {
            // ここで送り出しを行う。
            if (isRemoteConnected)
            {
                try
                {
                    var w = Activator.CreateInstance(typeof(W), t);
                    var json = JsonUtility.ToJson(w);
                    _this.ws.Send(Encoding.UTF8.GetBytes(json));
                }
                catch (Exception e)
                {
                    Debug.LogError("e:" + e);
                }
            }
            onReceived(t);
        };

        StartConnect(ip);
    }

    private void SetupRemoteConnection2<T, U, W>(string ip, ref Action<T, U> onData, Action<T, U> onReceived) where W : IRemotePayload2
    {
        // onDataの書き換えを行う
        onData = (t, u) =>
        {
            // ここで送り出しを行う。
            if (isRemoteConnected)
            {
                try
                {
                    var w = Activator.CreateInstance(typeof(W), t, u);
                    var json = JsonUtility.ToJson(w);
                    _this.ws.Send(Encoding.UTF8.GetBytes(json));
                }
                catch (Exception e)
                {
                    Debug.LogError("e:" + e);
                }
            }
            onReceived(t, u);
        };

        StartConnect(ip);
    }


    private void SetupRemoteConnection3<T, U, V, W>(string ip, ref Action<T, U, V> onData) where W : IRemotePayload3
    {
        // onDataの書き換えを行う
        onData = (t, u, v) =>
        {
            // ここで送り出しを行う。
            if (isRemoteConnected)
            {
                try
                {
                    var w = Activator.CreateInstance(typeof(W), t, u, v);
                    var json = JsonUtility.ToJson(w);
                    _this.ws.Send(Encoding.UTF8.GetBytes(json));
                }
                catch (Exception e)
                {
                    Debug.LogError("e:" + e);
                }
            }
        };

        StartConnect(ip);
    }

    private void StartConnect(string ip)
    {
        var url = "ws://" + ip + ":1129";
        var fp = new FilePersistence(Application.persistentDataPath);
        ws = new WebuSocket(
            url,
            10240,
            () =>
            {
                // 接続完了
                isRemoteConnected = true;

                // 接続できたipを保存
                fp.Update("connectionRecord", "latestIp", ip);
            },
            segments =>
            {
                // データを受け取ったのでなんかする。
            },
            () => { },
            closedEnum =>
            {
                isRemoteConnected = false;
                Debug.Log("closedEnum:" + closedEnum);
            },
            (error, reason) =>
            {
                isRemoteConnected = false;
                Debug.Log("e:" + error + " reason:" + reason);
            }
        );
    }

    private struct OnUpdatePayload
    {
        public string payload;
    }

    private void SetupEditorConnection1<T, W>(Action<T> onReceived) where W : IRemotePayload1
    {
        StartReceiving(
            data =>
            {
                // jsonからWを生成する
                var wData = (W)(typeof(A_npanRemote).GetMethod("JsonUtilityTypeResolver").MakeGenericMethod(typeof(W)).Invoke(this, new object[] { data }));

                // deserializeしたデータを受信用のハンドラに投入する
                try
                {
                    onReceived((T)wData.Param0());
                }
                catch
                {
                    // エラーは無視する。
                }
            }
        );
    }

    private void SetupEditorConnection2<T, U, W>(Action<T, U> onReceived) where W : IRemotePayload2
    {
        StartReceiving(
            data =>
            {
                // jsonからWを生成する
                var wData = (W)(typeof(A_npanRemote).GetMethod("JsonUtilityTypeResolver").MakeGenericMethod(typeof(W)).Invoke(this, new object[] { data }));

                // deserializeしたデータを受信用のハンドラに投入する
                try
                {
                    onReceived((T)wData.Param0(), (U)wData.Param1());
                }
                catch
                {
                    // エラーは無視する。
                }
            }
        );
    }

    private void SetupEditorConnection3<T, U, V, W>(Action<T, U, V> onReceived) where W : IRemotePayload3
    {
        StartReceiving(
            data =>
            {
                // jsonからWを生成する
                var wData = (W)(typeof(A_npanRemote).GetMethod("JsonUtilityTypeResolver").MakeGenericMethod(typeof(W)).Invoke(this, new object[] { data }));

                // deserializeしたデータを受信用のハンドラに投入する
                try
                {
                    onReceived((T)wData.Param0(), (U)wData.Param1(), (V)wData.Param2());
                }
                catch
                {
                    // エラーは無視する。
                }
            }
        );
    }

    private void StartReceiving(Action<string> onReceived)
    {
        // WS -> updateへのデータの転送を行うチャンネル。
        var chan = Chan<OnUpdatePayload>.Make();

        // updateでデータを受け取るブロック。
        chan.Receive(
            (data, ok) =>
            {
                if (!ok)
                {
                    return;
                }

                onReceived(data.payload);
            }
        );

        var fp = new FilePersistence(Application.persistentDataPath);
        ws = new WebuSocket(
            "ws://" + "127.0.0.1" + ":1129",
            10240,
            () =>
            {
                // 接続できたipを保存
                fp.Update("connectionRecord", "latestIp", "127.0.0.1");

                // var data = new byte[1300];
                // // for (var i = 0; i < data.Length; i++)
                // // {
                // //     data[i] = (byte)UnityEngine.Random.Range(0, 100);
                // // }

                // try
                // {
                //     ws.Send(data);
                // }
                // catch (Exception e)
                // {
                //     Debug.Log("ここだ" + e);
                // }

                // do nothng.
            },
            segments =>
            {
                // データを受け取った。受け取りは非メインスレッドなので、メインスレッドに転送する必要がある。
                while (0 < segments.Count)
                {
                    var data = segments.Dequeue();
                    var bytes = new byte[data.Count];
                    Buffer.BlockCopy(data.Array, data.Offset, bytes, 0, data.Count);

                    // updateブロックへと転送する
                    var jsonPayload = new OnUpdatePayload();
                    jsonPayload.payload = Encoding.UTF8.GetString(bytes);
                    chan.Send(jsonPayload);
                }
            },
            () => { },
            closedEnum =>
            {
                Debug.Log("closedEnum:" + closedEnum);
            },
            (error, reason) =>
            {
                Debug.Log("error:" + error + " reason:" + reason);
            },
            new Dictionary<string, string> { { "local", "" } }
        );
    }

    public T JsonUtilityTypeResolver<T>(string json)
    {
        return JsonUtility.FromJson<T>(json);
    }





    #region IDisposable Support
    private bool disposedValue = false; // To detect redundant calls

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // 切断を行う
                ws?.Disconnect();
                Channels.Close<OnUpdatePayload>();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            disposedValue = true;
        }
    }

    // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
    // ~Remote()
    // {
    //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
    //   Dispose(false);
    // }

    // This code added to correctly implement the disposable pattern.
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);
        // TODO: uncomment the following line if the finalizer is overridden above.
        // GC.SuppressFinalize(this);
    }
    #endregion
}