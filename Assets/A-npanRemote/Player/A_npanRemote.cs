using System;
using System.Collections.Generic;
using System.Text;
using ChanquoCore;
using UnityEngine;
using WebuSocketCore;
using AutoyaFramework.Persistence.Files;

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
    public static void Setup<T, U, V, W>(string ip, ref Action<T, U, V> onSend, Action<T, U, V> onReceived) where W : IRemotePayload3
    {
        _this = new A_npanRemote();
#if !UNITY_EDITOR
        // エディタの場合、セットアップと受け取り時の処理のセットアップを行う。
        _this.SetupEditorConnection3<T, U, V, W>(onReceived);
#else
        // エディタ以外であれば、特定のIPへと接続を行う。
        _this.SetupRemoteConnection3<T, U, V, W>(ip, ref onSend, onReceived);
#endif
    }


    [System.Diagnostics.Conditional("REMOTE")]

    public static void SendToEditor<T>(T faceTrackingPayload)
    {
        // エディタの場合は何もしない
#if UNITY_EDITOR
        return;
#endif


    }





    public static void Teardown()
    {
        _this?.Dispose();
    }



    private void SetupRemoteConnection3<T, U, V, W>(string ip, ref Action<T, U, V> onData, Action<T, U, V> onReceived) where W : IRemotePayload3
    {
        var url = "ws://" + ip + ":1129";

        var fp = new FilePersistence(Application.persistentDataPath);

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
            onReceived(t, u, v);
        };

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
                Debug.Log("closedEnum:" + closedEnum);
            },
            (error, reason) =>
            {
                Debug.Log("e:" + error + " reason:" + reason);
            }
        );
    }

    private class OnUpdatePayload : IChanquoBase
    {
        public string payload;
    }

    private void SetupEditorConnection3<T, U, V, W>(Action<T, U, V> onReceived) where W : IRemotePayload3
    {
        // WS -> updateへのデータの転送を行うチャンネル。
        var chan = Chanquo.MakeChannel<OnUpdatePayload>();

        // updateでデータを受け取るブロック。
        Chanquo.Select<OnUpdatePayload>(
            (data, ok) =>
            {
                if (!ok)
                {
                    return;
                }

                // jsonからWを生成する
                var wData = (W)(typeof(A_npanRemote).GetMethod("JsonUtilityTypeResolver").MakeGenericMethod(typeof(W)).Invoke(this, new object[] { data.payload }));

                // deserializeしたデータを受信用のハンドラに投入する
                try
                {
                    onReceived((T)wData.T(), (U)wData.U(), (V)wData.V());
                }
                catch
                {
                    // エラーは無視する。
                }
            }
        );


        var jsonPayload = new OnUpdatePayload();

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