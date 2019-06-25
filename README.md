


# A-npanRemote

ARKit parameter streaming kit for Unity.  
enable to receive ARKit parameter from iOS device to Unity Editor easily.  


## example(ARKitFaceTracking) usage


1. add https://github.com/statianzo/Fleck src folder into Assets/A-npanRemote/A-npanRemote/Editor.
2. the "REMOTE" Scripting Debug symbol is defined for enable remote tracking in this project. remote this when you remove A-npanRemote feature.
3. build iOS project and install it into your iOS device.
4. play __Assets/ARFaceTrackingSample/ARFaceTrackingSampleScene.unity__ scene in Unity Editor
5. play iOS app on device. then input PC's IP such as 192.168.1.11 into input field and hit Connect button. 

so now you can receive Face tracking data on your Unity Editor from the iOS device.


## installation
unitypackage is not ready yet. copy A-npan remote folder manually.

1. copy Assets/A-npanRemote folder into your Project.
1. write your own remote receiving feature like the ARFaceTrackingSample. see __extend__.


## extend
A-npanRemote is easy to extend for other remote system. like VR.

step is below.

1. make your own sencing system. e,g, FaceTracking, VR head & hand position tracking.
1. extend your system by extends __RemoteBase__ class. this allow to call __OnData__ method.
1. call __OnData__ method where your system is receiving data from data source. [see example.](https://github.com/sassembla/A-npanRemote/blob/master/Assets/ARFaceTrackingSample/ARFaceTracking.cs#L84) 
1. call A_npanRemote.Setup method on your project. [see example.](https://github.com/sassembla/A-npanRemote/blob/master/Assets/ARFaceTrackingSample/ARFaceTrackingSample.cs#L34)

```csharp
var validInput = "Unity Editor IP(x.x.x.x)";

// init facetracking.
var fTrack = new ARFaceTracking();
fTrack.StartTracking(
    () =>
    {
    },
    (faceMat4x4, faceBlendShapes, cameraPosAndRot) =>
    {
        OnFaceTrackingDataReceived(faceMat4x4, faceBlendShapes, cameraPosAndRot);
    }
);

var faceBlendShapeDict = new Dictionary<string, float>();

// these block will be disappeared when "REMOTE" scriptingDefineSymbol is removed. 
A_npanRemote.Setup<FaceTrackingPayload>(
    validInput,
    fTrack,
    data =>
    {
        faceBlendShapeDict.Clear();
        for (var i = 0; i < data.keys.Length; i++)
        {
            faceBlendShapeDict[data.keys[i]] = data.values[i];
        }
        OnFaceTrackingDataReceived(data.faceMat4x4, faceBlendShapeDict, data.cameraPosAndRot);
    }
);
```

## disable
remote "REMOTE" 

## license
MIT
