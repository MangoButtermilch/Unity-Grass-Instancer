# Frustum Culling
## This approach uses DrawMeshInstancedIndirect with a dynamic AppendStructuredBuffer and a compute shader


![Alt text](../Screenshots/Frustum_culling_example.png?raw=true "Frustum culling example")


## Rough explanation
- First we initialize our instances same as before
- All generated instances will be stored in the `_trsBuffer`
- `_visibleBuffer` new (dynamic buffer) of type `AppendStructuredBuffer` needs to be created
- The `_visibleBuffer` is shared with the compute shader and the instance shader for rendering
- The `_trsBuffer` is now only shared with the compute shader
- The compute shader decides wether or not an instance is visible by iterating over the `_trsBuffer` and checking if it is in the `view frustum`. If it is, *`append`* it to the `_visibleBuffer`
- Now we need an additional `_readBackArgsBuffer` to read back the amount of items inside the `_visibleBuffer`
- `_argsBuffer` will then recieve the new data with the amount of visible items
- Call DrawMeshInstancedIndirect with the `_argsBuffer` and let instance rendering shader do the rest

## [See in combination with chunking](https://github.com/MangoButtermilch/Unity-Grass-Instancer/tree/main/Frustum%20Culling)

### How do we know that an instance is inside the view frustum?
We provide this array to the compute shader: `float4 viewFrustmPlanes[6]` which is filled with data from `GeometryUtility.CalculateFrustumPlanes`.

Inside of each element is a Vector4 where xyz represent the normal of the `view frustum plane` and w contains the `distance` measured from the Plane to the origin, along the Plane's normal.

Now inside the `compute shader` we simply check if the instance is in front of all planes to determine if it is visible.
This is done with a simple `dot product` and the `w` component of the plane.
````
float planeDistance(float4 plane, float3 p) {
    return dot(plane.xyz, p) + plane.w;
} 

bool isInFrontOfPlane(float4 plane, float3 p) {
    return planeDistance(plane, p) > 0;
}
````

### About the AppendStructuredBuffer
In simple terms: it is a dynamic buffer that grows or shrinks on its own. We simply need to reset its `Counter value` to 0 everytime before we dispatch the compute shader.

Just note: to read back the amount of entries inside the buffer we need an additional buffer:
````
    private void SetVisibleInstanceCount()
    {
        ComputeBuffer.CopyCount(_visibleBuffer, _readBackArgsBuffer, 0);
        int[] appendBufferCount = new int[1];
        _readBackArgsBuffer.GetData(appendBufferCount);
        _visibleInstanceCount = appendBufferCount[0];
    }
````


## TODOs
- Occlusion culling is next on my bucket list
- `_recieveShadow` causes some weird artifacts on the instances -> can't figure out why