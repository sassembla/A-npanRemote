


# A-npanRemote

REMOTE to UnityEditor parameter streaming kit.


## example(ARKitFaceTracking) usage

1. sample codes are below.

```csharp
var ip = "Unity Editor IP(x.x.x.x)";

// init facetracking.
var fTrack = new ARFaceTracking();
fTrack.StartTracking(
    () =>
    {
        // on start face tracking.
    },
    (faceMat4x4, faceBlendShapes, cameraRot) =>
    {
        // on update face tracking.
    }
);

// add A_npanRemote setup method.
// these block will be disappeared when "REMOTE" scriptingDefineSymbol is removed. 
A_npanRemote.Setup<Matrix4x4, Dictionary<string, float>, Quaternion, FaceTrackingPayload>(
    validInput,
    ref fTrack.OnUpdate
);
```

2. run iOS test app with A-npanRemote and input Unity Editor IP.
3. run same apps on UnityEditor too.

so now you can receive Face tracking data on your Unity Editor from the iOS device.




## installation
use unitypackage.



## application for your parameter streaming
A-npanRemote is easy to extend for other remote system. like VR.

```csharp
class VRTracking 
{
    public void StartTracking(
        GameObject[] cameraLeftAndRight, 
        Action<
            VRTransform, // head position and rotation
            HandInput, // left hand position and rotation and triggers
            HandInput // right hand position and rotation and triggers
        > update)
    {
        // start tracking.
        StartCoroutine(OnUpdate(cameraLeftAndRight, update));
    }
    ...
}

// usage
var vrTrack = new VRTracking();
vrTrack.StartTracking(
    objects,
    (head, leftHand, rightHand) =>
    {
        // on update vr tracking.
    }
);

```

step is below.

1. define IRemotePayload1, 2 or 3 payload type for streaming.

IRemotePayload types are container of parameters for streming.
```csharp
IRemotePayload1 : convert 1 parameter into 1 remotePayload.
IRemotePayload2 : convert 2 parameters into 1 remotePayload.
IRemotePayload3 : convert 3 parameters into 1 remotePayload.
```

in this case, 3 parameters required and convert it into IRemotePayload3.
```csharp
public class VRTrackingPayload : IRemotePayload3
{
    [SerializeField] public VRTransform headCamera;// 0
    [SerializeField] public HandInput leftHand;// 1
    [SerializeField] public HandInput rightHand;// 2

    public object Param0()
    {
        return headCamera;// return 0 index data.
    }

    public object Param1()
    {
        return leftHand;// return 1 index data.
    }

    public object Param2()
    {
        return rightHand;// return 2 index data.
    }
}
```

2. expose the method for receiving original 3parameters. e,g, 

```csharp
class VRTracking 
{
    // expose OnTracking method.
    public Action<VRTransform, HandInput, HandInput> OnTracking;

    public void StartTracking(
        GameObject[] cameraLeftAndRight, 
        Action<
            VRTransform, // head position and rotation
            HandInput, // left hand position and rotation and triggers
            HandInput // right hand position and rotation and triggers
        > update)
    {
        // set OnTracking method.
        this.OnTracking = update;

        // start tracking.
        StartCoroutine(OnUpdate(cameraLeftAndRight, this.OnTracking));
    }
    ...
}
```

3. write setup code of A-npanRemote.


```csharp
var ip = "Unity Editor IP(x.x.x.x)";

var vrTrack = new VRTracking();
vrTrack.StartTracking(
    objects,
    (head, leftHand, rightHand) =>
    {
        // on update vr tracking.
    }
);

// setup A-npanRemote.
A_npanRemote.Setup<VRTransform, HandInput, HandInput, VRTrackingPayload>(
    ip,
    ref vrTrack.OnUpdate// set reference of exposed Update method.
);
```


4. define "REMOTE" scriptingDefineSymbol for remote tracking

A-npanRemote runs when "REMOTE" symbol is set.

it's done! vr tracking data from OculusQuest will reaches your UnityEditor.


## license
MIT
