using System;

public class RemoteBase
{
    public Action<IRemotePayload> OnData = p => { };
}

public interface IRemotePayload { }