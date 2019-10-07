using System.Collections.Generic;
using TMPro;
using UnityEngine;

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

        // 普通に開始させる
        fTrack.StartTracking(
            () =>
            {
                Debug.Log("start face tracking.");
            },
            (facePosAndRot, faceBlendShapes, cameraRot) =>
            {
                // 送り出しを行う( REMOTE ScriptDebugSymbolがある時のみ実行される)
                A_npanRemote.SendToEditor<FaceTrackingPayload>(new FaceTrackingPayload(facePosAndRot, faceBlendShapes, cameraRot));

                // 受け取ってから普通に何かするルート、コードを書いておく。
                OnFaceTrackingDataReceived(facePosAndRot, faceBlendShapes, cameraRot);
            }
        );

        // このブロックは REMOTE ScriptDebugSymbol を消したら自動的に消える。
        {
            A_npanRemote.Setup<FaceTrackingPayload>(
                ipText,
                data =>
                {
                    // エディタの場合は、実機からのデータがくる。通常のデータ受け取りと同じコードを書いておく。
                    OnFaceTrackingDataReceived(data.facePosAndRot, data.FaceBlendShapeDict, data.cameraRot);
                }
            );
        }
    }

    private void OnFaceTrackingDataReceived(PosAndRot facePosAndRot, Dictionary<string, float> face, Quaternion cameraRot)
    {
        Debug.Log("face data received. facePosAndRot:" + facePosAndRot + " cameraRot:" + cameraRot);
    }

    void OnDestroy()
    {
        arFaceTracking?.Dispose();
        A_npanRemote.Teardown();
    }

}