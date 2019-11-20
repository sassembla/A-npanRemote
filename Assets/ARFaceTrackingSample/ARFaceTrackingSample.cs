using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;


// A_npanRemoteが使うためのデータ構造。
[Serializable]
public class FTPayload : IRemotePayload3
{
    [SerializeField] public Matrix4x4 facePosAndRot;
    [SerializeField] public string[] keys;
    [SerializeField] public float[] values;
    [SerializeField] public Quaternion cameraRot;

    public FTPayload(Matrix4x4 facePosAndRot, Dictionary<string, float> faceBlendShapes, Quaternion cameraRot)
    {
        this.facePosAndRot = facePosAndRot;
        this.keys = faceBlendShapes.Keys.ToArray();
        this.values = faceBlendShapes.Values.ToArray();
        this.cameraRot = cameraRot;
    }

    public object Param0()
    {
        return this.facePosAndRot;
    }

    public object Param1()
    {
        return GenerateFaceBlendShapeDict();
    }

    public object Param2()
    {
        return this.cameraRot;
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

public class ARFaceTrackingSample : MonoBehaviour
{
    private ARFaceTracking arFaceTracking;

    public void Start()
    {
        // 過去に接続に成功した記録があれば、UIに入れる。
        var oldIp = string.Empty;
        A_npanRemote.LatestConnectionRecord(ref oldIp);

        if (!string.IsNullOrEmpty(oldIp))
        {
            var tmInput = GameObject.Find("InputField (TMP)").GetComponent<TMP_InputField>();
            tmInput.text = oldIp;
        }
    }

    public void Connect(TMP_InputField textHolder)
    {
        var ipText = textHolder.text;
        var fTrack = new ARFaceTracking();

        // 普通に顔認識(FaceTracking)を開始させる
        fTrack.StartTracking(
            () =>
            {
                // 顔認識開始 
            },
            (Matrix4x4 facePosAndRot, Dictionary<string, float> faceBlendShapes, Quaternion cameraRot) =>
            {
                // 顔認識のUpdate
            }
        );

        A_npanRemote.Setup<Matrix4x4, Dictionary<string, float>, Quaternion, FTPayload>(
            ipText,
            ref fTrack.OnTrackingUpdate
        );
    }

    private void OnFaceTrackingDataReceived(PosAndRot facePosAndRot, Dictionary<string, float> faceBlendShapes, Quaternion cameraRot)
    {
        Debug.Log("face data received. facePosAndRot:" + facePosAndRot + " cameraRot:" + cameraRot);
    }

    void OnDestroy()
    {
        arFaceTracking?.Dispose();
        A_npanRemote.Teardown();
    }

}