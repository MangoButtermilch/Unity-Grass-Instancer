# Occlusion Culling
## This approach uses DrawMeshInstancedIndirect with a dynamic AppendStructuredBuffer and a compute shader

Showcase: https://www.youtube.com/watch?v=3SGxhRqzCm8
![Alt text](../Screenshots/Occlusion_culling.png?raw=true "Frustum culling example")


## Rough explanation
- The approach extends my frustum culling with chunks approach: ([click here to read more](https://github.com/MangoButtermilch/Unity-Grass-Instancer/tree/main/Frustum%20Culling%20%2B%20Chunking))
- The compute shader now does an additional check with the scene depth texture to see if a chunk is occluded
- This works by:
    - converting chunk position to view space
    - project view space position to UV coords
    - sampling depth texture wit UVs
    - calculating and comparing chunk depth to sampled depth

## Info
- converting the chunk position to view space is done with a custom matrix `vpMatrix` since `UNITY_MATRIX_VP` does not work in the compute shader context
- before using the depth texture it needs to initialize one frame before we start rendering


## TODOs
- `_recieveShadow` still causes some weird artifacts on the instances
- implement `Mipmap levels` for sampling the depth texture
