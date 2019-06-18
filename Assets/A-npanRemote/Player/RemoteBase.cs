using System;

public class RemoteBase
{
    public Action<IRemotePayload> OnData;
}

public interface IRemotePayload { }