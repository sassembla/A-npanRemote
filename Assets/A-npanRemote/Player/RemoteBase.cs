using System;
using UnityEngine;

public class RemoteBase
{
    [System.Diagnostics.Conditional("REMOTE")]
    public void SetRemoteSendingAct<T, U, V>(ref Action<T, U, V> act, Func<T, U, V, IRemotePayload> ret)
    {
        // actの上書きを行う。
        act = (T t, U u, V v) =>
        {
            var r = ret(t, u, v);
            SendToRemote(r);
        };
    }

    [System.Diagnostics.Conditional("REMOTE")]
    public void SendToRemote(IRemotePayload data)
    {
        _onData(data);
    }

    public Action<IRemotePayload> _onData = p => { };
}

public class RemoteMonoBehaviourBase : MonoBehaviour
{
    [System.Diagnostics.Conditional("REMOTE")]
    public void OnData(IRemotePayload data)
    {
        _onData(data);
    }

    public Action<IRemotePayload> _onData = p => { };
}

public interface IRemotePayload { }