using System;

public class RemoteBase
{
    [System.Diagnostics.Conditional("REMOTE")]
    public void OnData(IRemotePayload data)
    {
        _onData(data);
    }

    public Action<IRemotePayload> _onData = p => { };
}

public interface IRemotePayload { }