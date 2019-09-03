using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class ARFaceTrackingSample : MonoBehaviour
{
    private ARFaceTracking arFaceTracking;

#if UNITY_EDITOR
    IEnumerator Start()
    {
        yield return new WaitForSeconds(1);
        var fTrack = new ARFaceTracking();

        // 普通に開始させる
        fTrack.StartTracking(
            () =>
            {
                Debug.Log("start face tracking.");
            },
            (faceMat4x4, faceBlendShapes, cameraPosAndRot) =>
            {
                // 受け取ってから普通に何かするルート、コードを書いておく。
                OnFaceTrackingDataReceived(faceMat4x4, faceBlendShapes, cameraPosAndRot);
            }
        );

        // このブロックはREMOTE ScriptDebugSymbolを消したら自動的に消える。
        var faceBlendShapeDict = new Dictionary<string, float>();

        A_npanRemote.Setup<FaceTrackingPayload>(
            string.Empty,
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
#endif

    public void Connect(TMP_InputField textHolder)
    {
        var validInput = textHolder.text;
        var fTrack = new ARFaceTracking();

        // 普通に開始させる
        fTrack.StartTracking(
            () =>
            {
                Debug.Log("start face tracking.");
            },
            (faceMat4x4, faceBlendShapes, cameraPosAndRot) =>
            {
                // 受け取ってから普通に何かするルート、コードを書いておく。
                OnFaceTrackingDataReceived(faceMat4x4, faceBlendShapes, cameraPosAndRot);
            }
        );

        // このブロックはREMOTE ScriptDebugSymbolを消したら自動的に消える。
        var faceBlendShapeDict = new Dictionary<string, float>();

        A_npanRemote.Setup<FaceTrackingPayload>(
            validInput,
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

    private void OnFaceTrackingDataReceived(Matrix4x4 faceMat, Dictionary<string, float> face, PosAndRot cameraPosAndRot)
    {
        Debug.Log("face data received.");
    }

    void OnDestroy()
    {
        arFaceTracking?.Dispose();
        A_npanRemote.Teardown();
    }

}