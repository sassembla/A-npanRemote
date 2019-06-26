using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class VRTransform
{
    [SerializeField] public Vector3 pos;
    [SerializeField] public Quaternion rot;
}

[Serializable]
public class HandInput
{
    [SerializeField] public VRTransform trans;
}

[Serializable]
public class VRTrackingPayload : IRemotePayload
{
    [SerializeField] public VRTransform headCamera;
    [SerializeField] public HandInput leftHand;
    [SerializeField] public HandInput rightHand;
}

public class VRTrackingSample : MonoBehaviour
{
    public GameObject cubeHead;
    public GameObject cubeLeftHand;
    public GameObject cubeRightHand;

    private VRTracking vrTracking;

    // Start is called before the first frame update
    private void Start()
    {
        var go = new GameObject("core");
        vrTracking = go.AddComponent<VRTracking>();
        vrTracking.StartTracking(
            new GameObject[] { cubeHead, cubeLeftHand, cubeRightHand },
            data =>
            {
                OnTrackingMove(data);
            }
        );

        A_npanRemote.Setup<VRTrackingPayload>(
            "192.168.11.17",
            vrTracking,
            data =>
            {
                OnTrackingMove(data);
            }
        );
    }

    private void OnTrackingMove(VRTrackingPayload update)
    {
        // ここで適当にキューブを動かそう。
        var head = update.headCamera;
        var leftHand = update.leftHand;
        var rightHand = update.rightHand;

        cubeHead.transform.position = head.pos;
        cubeLeftHand.transform.position = leftHand.trans.pos;
        cubeRightHand.transform.position = rightHand.trans.pos;
    }




    private void OnApplicationQuit()
    {
        vrTracking?.Dispose();
    }
}
