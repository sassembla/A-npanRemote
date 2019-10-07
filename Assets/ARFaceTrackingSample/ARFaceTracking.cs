using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.iOS;


[Serializable]
public class FaceTrackingPayload : IRemotePayload
{
    [SerializeField] public PosAndRot facePosAndRot;
    [SerializeField] public string[] keys;
    [SerializeField] public float[] values;
    [SerializeField] public Quaternion cameraRot;

    public FaceTrackingPayload(PosAndRot facePosAndRot, Dictionary<string, float> faceBlendShapes, Quaternion cameraRot)
    {
        this.facePosAndRot = facePosAndRot;
        this.keys = faceBlendShapes.Keys.ToArray();
        this.values = faceBlendShapes.Values.ToArray();
        this.cameraRot = cameraRot;
    }

    public Dictionary<string, float> FaceBlendShapeDict
    {
        get
        {
            var faceBlendShapeDict = new Dictionary<string, float>();
            for (var i = 0; i < keys.Length; i++)
            {
                faceBlendShapeDict[keys[i]] = values[i];
            }
            return faceBlendShapeDict;
        }
    }

    internal Dictionary<string, float> GenerateFaceBlendShapeDict()
    {
        var faceBlendShapeDict = new Dictionary<string, float>();
        for (var i = 0; i < keys.Length; i++)
        {
            faceBlendShapeDict[keys[i]] = values[i];
        }
        return faceBlendShapeDict;
    }
}


[Serializable]
public struct PosAndRot
{
    [SerializeField] public Vector3 pos;
    [SerializeField] public Quaternion rot;

    public PosAndRot(Vector3 pos, Quaternion rot)
    {
        this.pos = pos;
        this.rot = rot;
    }

    public PosAndRot(Matrix4x4 mat)
    {
        this.pos = UnityARMatrixOps.GetPosition(mat);
        this.rot = UnityARMatrixOps.GetRotation(mat);
    }

    public override string ToString()
    {
        return "pos:" + pos + " rot:" + rot;
    }
}


public class ARFaceTracking : IDisposable
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


    private Action<UnityARCamera> _frameUpdated = x => { };
    private Action<ARFaceAnchor> _faceAdded = x => { };
    private Action<ARFaceAnchor> _faceUpdated = x => { };
    private Action<ARFaceAnchor> _faceRemoved = x => { };


    static ARFaceTracking()
    {
        UnityARSessionNativeInterface.ARFaceAnchorAddedEvent += FaceAdded;
        UnityARSessionNativeInterface.ARFaceAnchorUpdatedEvent += FaceUpdated;
        UnityARSessionNativeInterface.ARFaceAnchorRemovedEvent += FaceRemoved;
        UnityARSessionNativeInterface.ARFrameUpdatedEvent += FrameUpdated;
    }


    public void StartTracking(
        Action onStartTracking,
        Action<PosAndRot, Dictionary<string, float>, Quaternion> OnTrackingUpdate
    )
    {
        _this = this;
        _frameUpdated = x =>
        {
            // 自身の再度実行を防ぐ
            _frameUpdated = x2 => { };

            onStartTracking();

            // added, update時に実行される関数をセット
            _faceAdded = p =>
            {
                var posAndRot = new PosAndRot(p.transform);
                OnTrackingUpdate(posAndRot, p.blendShapes, GetCameraRot());
            };
            _faceUpdated = p =>
            {
                var posAndRot = new PosAndRot(p.transform);
                OnTrackingUpdate(posAndRot, p.blendShapes, GetCameraRot());
            };
            _faceRemoved = p => { };
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
            _session = UnityARSessionNativeInterface.GetARSessionNativeInterface();
            _session.RunWithConfig(config);
        }

        _state = TrackingState.RUNNING;
    }

    public void StopTracking()
    {
        _frameUpdated = x => { };
        _faceAdded = p => { };
        _faceUpdated = p => { };
        _faceRemoved = p => { };
    }


    public Quaternion GetCameraRot()
    {
        var pose = _session.GetCameraPose();
        var rot = UnityARMatrixOps.GetRotation(pose);
        return rot;
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
                _session?.Pause();

                _this = new ARFaceTracking();

                _frameUpdated = (x) => { };
                _faceAdded = (x) => { };
                _faceUpdated = (x) => { };
                _faceRemoved = (x) => { };

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
