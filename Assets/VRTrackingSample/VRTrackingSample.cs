using System;
using System.Collections;
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


public class VRTrackingPayload : IRemotePayload3
{
    [SerializeField] public VRTransform headCamera;
    [SerializeField] public HandInput leftHand;
    [SerializeField] public HandInput rightHand;

    public object Param0()
    {
        return headCamera;
    }

    public object Param1()
    {
        return leftHand;
    }

    public object Param2()
    {
        return rightHand;
    }
}


public class VRTrackingSample : MonoBehaviour
{
    public GameObject cubeHead;
    public GameObject cubeLeftHand;
    public GameObject cubeRightHand;

    private VRTracking vrTracking;

    // Start is called before the first frame update
    private IEnumerator Start()
    {
        var go = new GameObject("core");
        vrTracking = go.AddComponent<VRTracking>();
        vrTracking.StartTracking(
            new GameObject[] { cubeHead, cubeLeftHand, cubeRightHand },
            (head, leftHand, rightHand) =>
            {
                OnTrackingMove(head, leftHand, rightHand);
            }
        );

        yield return new WaitForSeconds(1);

        A_npanRemote.Setup<VRTransform, HandInput, HandInput, VRTrackingPayload>(
            "192.168.11.17",
            ref vrTracking.OnTracking,
            OnTrackingMove
        );
    }

    private void OnTrackingMove(VRTransform head, HandInput leftHand, HandInput rightHand)
    {
        Debug.Log("here! update head.pos:" + head.rot + " l pos:" + leftHand.trans.pos + " r pos:" + rightHand.trans.pos);
        cubeHead.transform.position = head.pos;
        cubeLeftHand.transform.position = leftHand.trans.pos;
        cubeRightHand.transform.position = rightHand.trans.pos;
    }




    private void OnApplicationQuit()
    {
        vrTracking?.Dispose();
    }
}
