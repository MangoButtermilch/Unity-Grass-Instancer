# Frustum Culling + Chunks
## This approach uses DrawMeshInstancedIndirect with a dynamic AppendStructuredBuffer and a compute shader


![Alt text](../Screenshots/Frustum_culling_chunk_example.png?raw=true "Frustum culling example")


## Rough explanation
- The approach is really similliar to the plain frustum culling approach: [Read about frustum culling approach](https://github.com/MangoButtermilch/Unity-Grass-Instancer/tree/main/Frustum%20Culling)
- The main differences are:
    - world is divided in to a grid of chunks
    - each chunk holds a `start` index and `counter` variable
    - these represent the range inside the global `_trsBuffer` which contains all grass instances
    - the compute shader can then check first if all chunks are visible instead of all instances 
    - if a chunk is visible, we can use its `start` and `count` variables to determine which instances we want to append to the `visibleList` which is a `AppendStructuredBuffer`
 