using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.iOS;

[Serializable]
public class FaceTrackingPayload : IRemotePayload
{
    [SerializeField] public Matrix4x4 faceMat4x4;
    [SerializeField] public string[] keys;
    [SerializeField] public float[] values;
    [SerializeField] public PosAndRot cameraPosAndRot;
    public FaceTrackingPayload(Matrix4x4 faceMat4x4, Dictionary<string, float> faceBlendShapes, PosAndRot cameraPosAndRot)
    {
        this.faceMat4x4 = faceMat4x4;
        this.keys = faceBlendShapes.Keys.ToArray();
        this.values = faceBlendShapes.Values.ToArray();
        this.cameraPosAndRot = cameraPosAndRot;
    }
}

[Serializable]
public class PosAndRot
{
    [SerializeField] public Vector3 pos;
    [SerializeField] public Quaternion rot;

    public PosAndRot(Vector3 pos, Quaternion rot)
    {
        this.pos = pos;
        this.rot = rot;
    }
}


public class ARFaceTracking : RemoteBase, IDisposable
{
    private enum TrackingState
    {
        NONE,
        RUNNING,
        DISPOSED
    }

    private TrackingState _state = TrackingState.NONE;
    private UnityARSessionNativeInterface _session;
    private static ARFaceTracking _this;

    private Action<ARFaceAnchor> _faceAdded;
    private Action<ARFaceAnchor> _faceUpdated;
    private Action<ARFaceAnchor> _faceRemoved;
    private Action<UnityARCamera> _frameUpdated;


    public void StartTracking(
        Action OnStartTracking,
        Action<Matrix4x4, Dictionary<string, float>, PosAndRot> OnTrackingUpdate
    )
    {
        Debug.Log("StartTracking");
        _this = this;

        UnityARSessionNativeInterface.ARFaceAnchorAddedEvent += FaceAdded;
        UnityARSessionNativeInterface.ARFaceAnchorUpdatedEvent += FaceUpdated;
        UnityARSessionNativeInterface.ARFaceAnchorRemovedEvent += FaceRemoved;
        UnityARSessionNativeInterface.ARFrameUpdatedEvent += FrameUpdated;
        Debug.Log("StartTracking2");
        _frameUpdated = x =>
        {
            // 自身の再度実行を防ぐ
            _frameUpdated = x2 => { };

            Debug.Log("StartTracking3");

            OnStartTracking();

            // added, update時に実行される関数をセット
            _faceAdded = p =>
            {
                OnTrackingUpdate(p.transform, p.blendShapes, GetCameraPosAndRot());
                OnData(new FaceTrackingPayload(p.transform, p.blendShapes, GetCameraPosAndRot()));
            };
            _faceUpdated = p =>
            {
                OnTrackingUpdate(p.transform, p.blendShapes, GetCameraPosAndRot());
                OnData(new FaceTrackingPayload(p.transform, p.blendShapes, GetCameraPosAndRot()));
            };
        };

        if (_state == TrackingState.RUNNING)
        {
            return;
        }

        // sessionのスタートを行う。
        var config = new ARKitFaceTrackingConfiguration();
        config.alignment = UnityARAlignment.UnityARAlignmentGravity;
        config.enableLightEstimation = false;
        if (config.IsSupported)
        {
            Debug.Log("StartTracking5");
            _session = UnityARSessionNativeInterface.GetARSessionNativeInterface();
            Debug.Log("StartTracking6");
            _session.RunWithConfig(config);
            Debug.Log("StartTracking7");
        }

        Debug.Log("StartTracking8");

        _state = TrackingState.RUNNING;
    }

    public void StopTracking()
    {
        _frameUpdated = x => { };
        _faceAdded = p => { };
        _faceUpdated = p => { };
    }


    public PosAndRot GetCameraPosAndRot()
    {
        var pose = _session.GetCameraPose();
        var pos = UnityARMatrixOps.GetPosition(pose);
        var rot = UnityARMatrixOps.GetRotation(pose);
        return new PosAndRot(pos, rot);
    }



    private static void FaceAdded(ARFaceAnchor anchor)
    {
        _this._faceAdded(anchor);
    }

    private static void FaceUpdated(ARFaceAnchor anchor)
    {
        _this._faceUpdated(anchor);
    }

    private static void FaceRemoved(ARFaceAnchor anchor)
    {
        _this._faceRemoved(anchor);
    }

    private static void FrameUpdated(UnityARCamera camera)
    {
        _this._frameUpdated(camera);
    }




    #region IDisposable Support
    private bool disposedValue = false; // To detect redundant calls

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _faceAdded = (x) => { };
                _faceUpdated = (x) => { };
                _faceRemoved = (x) => { };
                _frameUpdated = (x) => { };

                UnityARSessionNativeInterface.ARFaceAnchorAddedEvent -= FaceAdded;
                UnityARSessionNativeInterface.ARFaceAnchorUpdatedEvent -= FaceUpdated;
                UnityARSessionNativeInterface.ARFaceAnchorRemovedEvent -= FaceRemoved;
                UnityARSessionNativeInterface.ARFrameUpdatedEvent -= FrameUpdated;

                _session?.Pause();

                _state = TrackingState.DISPOSED;
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            disposedValue = true;
        }
    }

    // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
    // ~ARSomething()
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
