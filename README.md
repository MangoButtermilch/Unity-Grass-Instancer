# Unity Grass Instancer
## Contains C# scripts and shaders for GPU instanced grass (or any other mesh)
### Made with Unity 2020.3.4f1 and HDRP 10.4.0
### Also tested with Unity 2022.3.40f1 and URP 14.0.11

![Alt text](Screenshots/showcase.gif?raw=true "Showcase")

Video showcase: https://www.youtube.com/watch?v=3SGxhRqzCm8


## Project contains 4 approaches
(Performance increases every step)
- (old) Plain instanced rendering using `DrawMeshInstanced` and `DrawMeshInstancedIndirect` without any optimizations ([click here to read more](https://github.com/MangoButtermilch/Unity-Grass-Instancer/tree/main/No%20Optimizations))
- Frustum culled approach ([click here to read more](https://github.com/MangoButtermilch/Unity-Grass-Instancer/tree/main/Frustum%20Culling))
- Frustum culled approach with chunking ([click here to read more](https://github.com/MangoButtermilch/Unity-Grass-Instancer/tree/main/Frustum%20Culling%20%2B%20Chunking))
- Occlusion culled approach with frustum culling and chunking ([click here to read more](https://github.com/MangoButtermilch/Unity-Grass-Instancer/tree/main/Occlusion%20Culling))
- High performance approach that extends all others ([click here to read more](https://github.com/MangoButtermilch/Unity-Grass-Instancer/tree/experimentation/Occlusion%20Culling%20%2B%20High%20performance))



## Requirements
- Any render pipeline (only URP and simple HDRP shader provided)
- A terrain
- SRP Batcher needs to be disabled
- Enable GPU instancing on the material
![Material GPU instancing setting](Screenshots/Material.png?raw=true "Material")

## Setup

- Drag the 3D Model + the scripts of any approach into your project
- Adjust the 3D model `scale factor` in the import settings to `100`
- Create a new material and assign the shader
- Attach the script to an empty game object
  - enable draw gizmos
  - position it at the center of your terrain
  - configure the range so it fits your terrain size
![Scene Setup](Screenshots/setup-scene.png?raw=true "Scene setup")


### Important settings
Note: some settings don't apply to all approaches.

- `Instances` - The amount of grass blades that can be instanced. Works best with powers of 2
- `True instance count` - Will be set by the script. Contains the true count of instances since some can be skipped. For example if the terrain is to steep.
- `Visible instance count` - Will be set by the script. Contains the amount of instances currently visible and thus being rendered.
- `Scale min and scale max` - Used to create a random size for each grass blade
- `Ground layer` - The layer of your terrain.
- `Chunk size` - The size in meters of a chunk. Works best with powers of 2.
- `Threads chunk render` - The amount of thread groups set in the compute shader. [Has to match this setting](https://github.com/MangoButtermilch/Unity-Grass-Instancer/blob/1a2fd0a4ba08cdaf32833794a49ec49a165c8667/Occlusion%20Culling/Visibility.compute#L3). This also works best with powers of 2.
- `Depth bias` - value that will be subtracted from the sampled depth texture. Used to fine tune the culling of occluded chunks.

## FAQ
### Why can't I see any instances?
This can have numerous reasons.

First be sure to check the requirements and setup sections.
Make sure the mesh is scaled correctly.

Adjust the `scale factor` in the import settings to your needs or use `100` if you are using the provided 3D model.

Your mesh could also be not placed or rotated correctly:
- [rotation adjustments](https://github.com/MangoButtermilch/Unity-Grass-Instancer/blob/1a2fd0a4ba08cdaf32833794a49ec49a165c8667/Occlusion%20Culling/GrassInstancerIndirect.cs#L371)
- [position adjustments](https://github.com/MangoButtermilch/Unity-Grass-Instancer/blob/1a2fd0a4ba08cdaf32833794a49ec49a165c8667/Occlusion%20Culling/GrassInstancerIndirect.cs#L297) 

### Why are all instances invisible or black?
This may happend, because you're placing the objects with the material by hand. This does not work because the shader needs and instance ID which is only provided by calling `DrawMeshInstanced` and `DrawMeshinstancedIndirect` for rendering. Also the `_trsBuffer` which contains all the data for the instances is only initialized at start and it's needed for rendering. 

Another reason could be that the shader is not compatible with your render pipeline. I currently only provide a shader for URP and a [shadergraph for HDRP](https://github.com/MangoButtermilch/Unity-Grass-Instancer/tree/main/No%20Optimizations). The HDRP shadergraph only works with `Unity 2021.2` or higher since the instance ID node does not exist in earlier versions. 


### If you have more issues, please open an issue here on Github.

## What's next/TODOs
- Initializing the grass instances completley on the GPU to avoid using Raycasts. [see progress here](https://github.com/MangoButtermilch/Unity-Grass-Instancer/tree/experimentation/Occlusion%20Culling)
- New shader for HDRP
- New shader for default render pipeline
- Improve the readme files 

## Resources
- I got started with this project by this great video about GPU instancing: https://www.youtube.com/watch?v=eyaxqo9JV4w
- I found a lot of information about grass rendering on Acerola's youtube channel: https://www.youtube.com/c/Acerola_t
  - I'm also using his 3D-Models in my showcase

 
## Glossary (terms I often use)
- `TRS`: Transformation, rotation and scale matrix.
- `_trsBuffer`: a compute buffer which holds all `TRS matrices` for the instances and can be accessed like an array inside shaders
- `_argsBuffer`: buffer that holds information about what mesh and how many to render for `DrawMeshInstancedIndirect`
- `batch`: a collection of `transformation matrices` for `DrawMeshInstanced`
