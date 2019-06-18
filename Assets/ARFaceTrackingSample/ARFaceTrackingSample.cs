using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ARFaceTrackingSample : MonoBehaviour
{
    private ARFaceTracking arFaceTracking;

    public void Connect(TextMeshProUGUI textHolder)
    {
        var fTrack = new ARFaceTracking();

        // 普通に開始させる
        fTrack.StartTracking(
            () => { },
            (faceMat4x4, faceBlendShapes, cameraPosAndRot) =>
            {
                // 受け取ってから普通に何かするルート、コードを書いておく。
                OnFaceTrackingDataReceived(faceMat4x4, faceBlendShapes, cameraPosAndRot);
            }
        );

        // このブロックはデバッグや調整、制作が完了したらまるっと消せる。
        {
            Debug.Log("textHolder.text:" + textHolder.text);
            var faceBlendShapeDict = new Dictionary<string, float>();

            A_npanRemote.Setup<FaceTrackingPayload>(
                textHolder.text,
                fTrack,
                data =>
                {
                    // エディタの場合は、実機からのデータがくる。通常のデータ受け取りと同じコードを書いておく。
                    // 実機の場合も同じで、通常のデータ受け取りと同じコードを書いておくと良い。
                    faceBlendShapeDict.Clear();
                    for (var i = 0; i < data.keys.Length; i++)
                    {
                        faceBlendShapeDict[data.keys[i]] = data.values[i];
                    }
                    OnFaceTrackingDataReceived(data.faceMat4x4, faceBlendShapeDict, data.cameraPosAndRot);
                }
            );
        }

    }

    private void OnFaceTrackingDataReceived(Matrix4x4 faceMat, Dictionary<string, float> face, PosAndRot cameraPosAndRot)
    {
        Debug.Log("到達してる");
    }

    void OnDestroy()
    {
        arFaceTracking?.Dispose();
        A_npanRemote.Teardown();
    }

}