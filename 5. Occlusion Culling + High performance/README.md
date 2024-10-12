# High performance grass rendering
## This approach extends and improves all previous techniques

## Showcase: https://www.youtube.com/watch?v=15BYZ1IBxzM
![Alt text](../Screenshots/Highperformance_preview.png?raw=true "")

### Showcase details:
- Terrain size: `4096m x 4096m`
- Max. amount of instances possible: `2²⁵ = 33,554,432`
- True amount of instances created:  `18,473,153`
- Amount of chunks: `262,144`
- Chunk size: `8m x 8m`
- Max instances per chunk: `128`
- Time for initializing all chunks and instances: `~ 13ms`
- Bytes per chunk: `20`
- Vertices per instance: `8`
- Shadow casting: `enabled`
- Average minimum FPS: `50 - 60`
- GPU: `GTX 1070`

## About this approach
- All instances are initialized on the GPU with the terrain data. This was previously done on the CPU via `RayCasts` (that was obviously way to slow).
- Renderer is using `billboards` to cover huge areas. Previously a grass blade model was used but it's just not feasable for such large areas (for now).

 
### Properties explained
- `Instances` - The max amount of grass blades that can be instanced. Works best with powers of 2
- `True instance count` - Will be set by the script. Contains the true count of instances since some can be skipped. For example if there's no grass texture
- `Visible instance count` - Will be set by the script. Contains the amount of instances currently visible and thus being rendered
- `Scale min and scale max` - Used to create a random size for each grass blade
- `Scale Noise Scale` - Value for the global noise scale that is used to generate a random scale between `Scale min` and `Scale max`
- `Min Grass Height` - All instances smaller than this will be skipped
- `Material` - Material instance with the `GrassBlillboardIndirect.shader` or `GrassBladeIndirect_Lighting.shader`
- `Mesh` - the 3D model for your grass
- `Max view distance` - How far you are able to see chunks
- `Grass compute shader` - Assign the `GrassRenderer.compute` shader to this
- `Tex Noise Layer 1 and 2` - Values for creating some sort of noise for the `billboard shader`
- `Chunk size` - The size in meters of a chunk. Works best with powers of 2.
- `LOD Level 1 and 2` - Values used to reduce the amount of instances in a chunk the further away it is. Level 1 starts at 2/3 of `Max view distance` and Level 2 at 3/3 
- `Threads chunk render` - The amount of thread groups when rendering. This works best with powers of 2.
- `Threads chunk init` - The amount of thread groups for initializing the chunks. This also works best with powers of 2.
- `Depth bias` - value that will be subtracted from the sampled depth texture. Used to fine tune the culling of occluded chunks.

- `Terrain` - Your terrain
- `Grass Threshhold` - The minimum value for sampling the `splatmap` for skipping instances (aka. how much of the grass texture is required for an instance). 

## TODOs
- implement `Mipmap levels` for sampling the depth texture
- remove artifacts from shadow cascades


## Detailed explanation

### Chunk initialization
Grass is rendered in chunks. Each `chunk` stores a `Vector3 position`, `uint instanceStartIndex` and `uint instanceCount`. The position is self-explanatory.
`instanceStartIndex` and `instanceCount` are used to define a range (start - end) inside a buffer that contains all transformation matrices.

First, the chunks will be initialized on the CPU by just setting their position to form a grid on the terrain. This happens in `InitializeGrassChunkPositions()`.

Then the compute shader figures out how many instances a chunk has.
This works by simply looping over the `instancesPerChunk`, which has been calculated earlier on the CPU. For each successfull iteration, the `instanceCount` needs to be increased.

After that loop, it keeps track of the total amount of instances and sets the start index for each chunk. This is a little more complex:
- counting the instances needs to be done with an `atomic operation`. This basically means that the operation is thread safe.
- To keep track of the count, we need to use an additional buffer `instanceCounter`.
- The value for the `instanceStartIndex` is then retrieved with the last parameter by calling `InterlockedAdd(instanceCounter, chunk.instanceCount, out <value_before_add>)`.

This is all happens inside the kernel `InitChunkInstanceCount`.


After dispatching that kernel, the compute shader runs again and iterates over the amount of instances per chunk. For each instance, a transformation (`TRS`) matrix is created and stored inside a global buffer (`trsBuffer`). Writing to that buffer is done with the previously calculated `instanceCount` and `instanceStartIndex`.
This runs in a new kernel `InitInstanceTransforms`.

All this needs to run in two separate kernels to prevent race conditions.

### Chunk rendering

If the chunks are initialized, a new kernel `ChunkRender` needs to be dispatched every frame to figure out what instances can be seen by the camera.

This works by sending over the chunks to the compute shader and checking if:
- it's instance count is greater than `0`
- it's to far away from the camera
- it's inside the view frustum
- it's occluded by geometry

The visibility check also needs to run in that specific order since each check costs more and more performance.
If the visibility check was successfull, we iterate over the instances inside the chunk and simply append it to a new buffer `visibleList`. This buffer is an `AppendStructuredBuffer` and simply stores all indices of visible instances. To put it simply:

```
uint start = chunk.instanceStartIndex;
uint end = start + chunk.instanceCount;

for (uint i = start; i < end; i++) {
  visibleList.Append(i);
}
```

Now everything for the chunk rendering is done.

### Instance rendering

Rendering each instance will now happend via the `material` shader. That material/shader is called after the `ChunkRender` kernel has finished from the C# script with `Graphics.DrawMeshInstancedIndirect`.

Inside the `vertex shader` of that material we can now access the `uint instanceID : SV_InstanceID` which simply contains an ID for every instance. We use that ID to access the `visibleList` - Remember that this list stores `indices` of all the visible TRS matrices.

So we can figure out what matrix to render with:
```
uint index = visibleList[instanceID];
float4x4 transformationMatrix = trsBuffer[index];
```
After that we can simply apply the transformation to the instance like this:
```
float3 positionWorldSpace = mul(mat, float4(v.vertex.xyz, 1));
o.vertex = mul(UNITY_MATRIX_VP, float4(positionWorldSpace, 1));
```

Everything else is just applying some `wind noise` and calculating the `shadowCoord` to render shadows on the object.

Side note: all buffers can be accessed on the material shader and compute shader. So there's no need to copy any data from CPU to GPU and back. You just need to call `<>.SetBuffer` on the material oder shader instance before. 


## Final words
I don't consider this production ready yet but I hope that'll change in the future. There are still many things I want to improve.
But for now I hope it can at least give you a starting point on grass rendering.