using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChanquoCore;
using UnityEngine;
#if UNITY_IOS
using UnityEngine.XR.iOS;


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
}

[Serializable]
public struct MatAndShape
{
    [SerializeField] public Matrix4x4 m4x4;
    [SerializeField] public string[] blendKeys;
    [SerializeField] public float[] blendValues;
    [SerializeField] public PosAndRot posAndRot;

    public MatAndShape(Matrix4x4 mat, Dictionary<string, float> blend, PosAndRot posAndRot)
    {
        this.m4x4 = mat;
        this.blendKeys = blend.Keys.ToArray();
        this.blendValues = blend.Values.ToArray();
        this.posAndRot = posAndRot;
    }
}

/**
    StartTracking, StopTracking
    Dispose
 */
public class ARKitFaceTracking
{
    private enum TrackingState
    {
        NONE,
        RUNNING,
        DISPOSED
    }

    private TrackingState _state = TrackingState.NONE;
    private UnityARSessionNativeInterface _session;
    private static ARKitFaceTracking _this;

    private Action<ARFaceAnchor> _faceAdded;
    private Action<ARFaceAnchor> _faceUpdated;
    private Action<ARFaceAnchor> _faceRemoved;
    private Action<UnityARCamera> _frameUpdated;


    static ARKitFaceTracking()
    {
        // このクラスの最初の初期化時にセットしてしまう。特に取り除かない。
        UnityARSessionNativeInterface.ARFaceAnchorAddedEvent += FaceAdded;
        UnityARSessionNativeInterface.ARFaceAnchorUpdatedEvent += FaceUpdated;
        UnityARSessionNativeInterface.ARFaceAnchorRemovedEvent += FaceRemoved;
        UnityARSessionNativeInterface.ARFrameUpdatedEvent += FrameUpdated;
    }


    public void StartTracking(
        Action<Matrix4x4, Dictionary<string, float>, PosAndRot> OnTrackingUpdate
    )
    {
        _this = this;

        _frameUpdated = x =>
        {
            // 自身の再度実行を防ぐ
            _frameUpdated = x2 => { };

            // added, update時に実行される関数をセット
            _faceAdded = p => OnTrackingUpdate(p.transform, p.blendShapes, GetCameraPosAndRot());
            _faceUpdated = p => OnTrackingUpdate(p.transform, p.blendShapes, GetCameraPosAndRot());
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
    }

    private class UpdatePayload : IChanquoBase
    {
        public string current;
    }

    // このメソッドがEditorだったら自動的に動作する、みたいな仕掛けが欲しい。まーー書くしかないにしても、同じようなことを書かされる。
    public void OpenBackDoor(Action<Matrix4x4, Dictionary<string, float>, PosAndRot> OnTrackingUpdate)
    {
        var blendShapeDict = new Dictionary<string, float>();
        var act = Chanquo.Select<UpdatePayload>(
            (data, ok) =>
            {
                // このブロックはUpdateで実行される
                var deser = JsonUtility.FromJson<MatAndShape>(data.current);
                var transform = deser.m4x4;
                blendShapeDict.Clear();
                for (var i = 0; i < deser.blendKeys.Length; i++)
                {
                    var key = deser.blendKeys[i];
                    var val = deser.blendValues[i];
                    blendShapeDict[key] = val;
                }
                var posAndRot = deser.posAndRot;

                OnTrackingUpdate(transform, blendShapeDict, posAndRot);
            }
        );

        var updatePayload = new UpdatePayload();

        var chan = Chanquo.MakeChannel<UpdatePayload>();

        _frameUpdated = x => { };
        _faceAdded = p => { };
        _faceUpdated = p => { };

        var ws = new WebuSocketCore.WebuSocket(
            "ws://127.0.0.1:1129",
            1024,
            () => { },
            data =>
            {
                try
                {
                    while (0 < data.Count)
                    {
                        var arraySegment = data.Dequeue();
                        var bytes = new byte[arraySegment.Count];
                        Buffer.BlockCopy(arraySegment.Array, arraySegment.Offset, bytes, 0, arraySegment.Count);

                        var jsonStr = Encoding.UTF8.GetString(bytes);
                        updatePayload.current = jsonStr;
                        chan.Send(updatePayload);
                    }
                }
                catch (Exception e)
                {
                    Debug.Log("e:" + e);
                }
            },
            null,
            null,
            null,
            new Dictionary<string, string> { { "receiver", "" } }
        );

    }

    private PosAndRot GetCameraPosAndRot()
    {
        var pose = _session.GetCameraPose();
        var pos = UnityARMatrixOps.GetPosition(pose);
        var rot = UnityARMatrixOps.GetRotation(pose);
        Debug.Log("pos:" + pos);
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
                _state = TrackingState.DISPOSED;

                _faceAdded = (x) => { };
                _faceUpdated = (x) => { };
                _faceRemoved = (x) => { };
                _frameUpdated = (x) => { };

                _session?.Pause();
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
#endif
