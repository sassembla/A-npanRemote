namespace ChanquoCore
{
    public enum ThreadMode
    {
        Default,// run on actual called thread. by default, OnUpdate is choosed.
        OnUpdate,
        OnLateUpdate,
        OnEndOfFrame,
        OnApplicationQuit
    }
}