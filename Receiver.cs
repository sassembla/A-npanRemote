using UnityEngine;

public class Receiver : MonoBehaviour
{
    ARKitFaceTracking faceTracking;

    public void Start()
    {
        faceTracking = new ARKitFaceTracking();
#if UNITY_EDITOR
        faceTracking.OpenBackDoor(
#elif UNITY_IOS
        faceTracking.StartTracking(
#endif
            (matrix, blendshape, posAndRot) =>
            {
                Debug.Log("受け取り m:" + posAndRot.rot);
            }
        );
    }

    public void OnDestroy()
    {
        faceTracking?.Dispose();
    }
}