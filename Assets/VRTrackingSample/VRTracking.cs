using System;
using System.Collections;
using UnityEngine;

public class VRTracking : MonoBehaviour, IDisposable
{
    public void StartTracking(GameObject[] cameraLeftAndRight, Action<VRTrackingPayload> update)
    {
        // 取得を開始する = タイミングタイミングでOnDataを叩く。
        StartCoroutine(OnUpdate(cameraLeftAndRight, update));
    }

    private IEnumerator OnUpdate(GameObject[] cameraLeftAndRight, Action<VRTrackingPayload> update)
    {
        while (true)
        {
            update(
                new VRTrackingPayload()
                {
                    headCamera = new VRTransform()
                    {
                        pos = cameraLeftAndRight[0].transform.position,
                        rot = cameraLeftAndRight[0].transform.rotation,
                    },
                    leftHand = new HandInput()
                    {
                        trans = new VRTransform()
                        {
                            pos = cameraLeftAndRight[1].transform.position,
                            rot = cameraLeftAndRight[1].transform.rotation,
                        }
                    },
                    rightHand = new HandInput()
                    {
                        trans = new VRTransform()
                        {
                            pos = cameraLeftAndRight[2].transform.position,
                            rot = cameraLeftAndRight[2].transform.rotation,
                        }
                    }
                }
            );
            yield return null;
        }
    }

    #region IDisposable Support
    private bool disposedValue = false; // To detect redundant calls

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            disposedValue = true;
        }
    }

    // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
    // ~VRTracking()
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
