using System.Collections;
using System.Collections.Generic;
using AutoyaFramework.Persistence.Files;
using UnityEngine;
using UnityEngine.UI;
using WebuSocketCore;

public class Main : MonoBehaviour
{
    private WebuSocket ws;
    private ARKitFaceTracking faceTracking;
    private bool connected = false;
    private FilePersistence filePersistence;

    public InputField urlTextHolder;

    IEnumerator Start()
    {
        filePersistence = new FilePersistence(Application.persistentDataPath);
        var url = filePersistence.Load("record", "setting");
        Debug.Log("url:" + url);

        urlTextHolder.text = url;

        while (true)
        {
            yield return null;
            if (connected)
            {
                break;
            }
        }

        // start Tracking tracking.
        // choose Face Tracking this time.
        faceTracking = new ARKitFaceTracking();
        faceTracking.StartTracking(
            (matrix, blendshape, posAndRot) =>
            {
                if (connected)
                {
                    var matAndShape = new MatAndShape(matrix, blendshape, posAndRot);
                    var json = JsonUtility.ToJson(matAndShape);

                    ws.SendString(json);
                }
            }
        );
    }


    public void Connect(Text urlText)
    {
        if (ws != null && ws.IsConnected())
        {
            return;
        }

        ws = new WebuSocket(
            "ws://" + urlText.text + ":1129",
            1024,
            () =>
            {
                var result = filePersistence.Update("record", "setting", urlText.text);
                connected = true;
            },
            (a) => { },
            () => { },
            closeReason =>
            {
                Debug.Log("closeReason:" + closeReason);
            },
            (error, reason) =>
            {
                Debug.Log("error:" + error + " reason:" + reason);
            }
        );
    }


    void OnDestroy()
    {
        ws?.Disconnect();
        faceTracking?.Dispose();
    }

}