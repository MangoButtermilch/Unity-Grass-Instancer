# Unity Grass Instancer
## Contains C# scripts and shaders for GPU instanced grass (or any other mesh)
### Made with Unity 2020.3.4f1 and HDRP 10.4.0
### Also tested with Unity 2022.3.40f1 and URP 14.0.11

![Alt text](Screenshots/showcase.gif?raw=true "Showcase")

Video showcase: https://www.youtube.com/watch?v=3SGxhRqzCm8


## Project contains 4 approaches

- Plain instanced rendering using `DrawMeshInstanced` and `DrawMeshInstancedIndirect` without any optimizations ([click here to read more](https://github.com/MangoButtermilch/Unity-Grass-Instancer/tree/main/No%20Optimizations))
- Frustum culled approach ([click here to read more](https://github.com/MangoButtermilch/Unity-Grass-Instancer/tree/main/Frustum%20Culling))
- Frustum culled approach with chunking ([click here to read more](https://github.com/MangoButtermilch/Unity-Grass-Instancer/tree/main/Frustum%20Culling%20%2B%20Chunking))
- Occlusion culled approach with frustum culling and chunking ([click here to read more](https://github.com/MangoButtermilch/Unity-Grass-Instancer/tree/main/Occlusion%20Culling))


## Glossary (terms I often use)
- `_trsBuffer`: a compute buffer which holds all `transformation matrices` for the instances and can be accessed like an array inside shaders
- `_argsBuffer`: buffer that holds information about what mesh and how many to render for `DrawMeshInstancedIndirect`
- `batch`: a collection of `transformation matrices` for `DrawMeshInstanced`

## Resources
- I got started with this project by this great video about GPU instancing: https://www.youtube.com/watch?v=eyaxqo9JV4w
- I found a lot of information about grass rendering on Acerola's youtube channel: https://www.youtube.com/c/Acerola_t
  - I'm also using his 3D-Models in my showcase

