using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;


// A_npanRemoteが使うためのデータ構造。
[Serializable]
public class FaceTrackingPayload : IRemotePayload3
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


        /*
            実機からエディタは、データを送り出す
            エディタは実機からのデータを受け取って動作する

            同じ関数へと送り出せればOKで、実機のみ、さらに転送する責務を負う。

            このパターンはなんだろうな、、、、
            
            理想形は、
            ・実機側はトラッキングを開始したら勝手にエディタへとデータが送られる
            ・エディタ側はトラッキングを開始したらデータを受け取る

            前者が大変なんだよな。一つの出力を2つに、自然に増やすのを求められてる。
            取り出せればいけるな
         */



        // 普通に開始させる
        fTrack.StartTracking(
            () =>
            {
                Debug.Log("start face tracking.");
            },
            (facePosAndRot, faceBlendShapes, cameraRot) =>
            {
                OnFaceTrackingDataReceived(facePosAndRot, faceBlendShapes, cameraRot);
            }
        );

        // このブロックは REMOTE ScriptingDefineSymbol を消したら自動的に消える。
        A_npanRemote.Setup<PosAndRot, Dictionary<string, float>, Quaternion, FaceTrackingPayload>(
            ipText,
            ref fTrack.OnTrackingUpdate,
            OnFaceTrackingDataReceived
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