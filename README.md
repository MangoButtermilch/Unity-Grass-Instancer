# Unity Grass Instancer
## Contains C# scripts and shaders for GPU instanced grass (or any other mesh)
### Made with Unity 2020.3.4f1 and HDRP 10.4.0

![Alt text](Screenshots/showcase.gif?raw=true "Showcase")

Video showcase: https://www.youtube.com/watch?v=pJSKUQJqBUs

## Project contains 2 approaches
- GrassInstancer.cs is using Unitys DrawMeshInstanced function which needs an array of Matrix4x4 to render the grass with a maximum batch size of 1023
  - This one is slower but has some dummy frustum culling
- GrassInstancerIndirect.cs is using Unitys DrawMeshInstancedIndirect function which uses compute buffers and has basically a unlimited batch size 
  - This one is way faster (about 20x times with my setup) but has no frustum culling (yet)

## Resources
- I got started with this project by this great video about GPU instancing: https://www.youtube.com/watch?v=eyaxqo9JV4w
- I found a lot of information about grass rendering on Acerola's youtube channel: https://www.youtube.com/c/Acerola_t
  - I'm also using his 3D-Models in my showcase

# Information for GrassInstancer.cs
## How to use
- Drag the script and the shader into your project
- Create an empty transform and attach the script
- Adjust the settings
- Add a spriterenderer 
  - with this simple trick we can get some frustum culling for free and don't have to implement it ourselves. Not the best solution but it works alright.
- Create a material with the shader (or your own shader)

## Example settings
![Alt text](Screenshots/Settings.png?raw=true "Settings")

## Properties explained
- Draw Gizmos - Will draw a bounding box inside OnDrawGizmos() for debugging
- Batch Size - The size you want your batches to be. Unity's max is 1023
- Instances - The amount of grass blades or other meshes
- Range - Range of the bounding box where rays for mesh positions will be tested
- Scale min/max - You can scale your meshes randomly between a min and max value
- Steepness - Limit the steepness your meshes can be spawned on
- Rotate to ground normal - Rotates the mesh towards the ground 
- Random Y axis rotation - Rotates the mesh randomly on the Y axis between a negative and positive value
- Max Y rotation - Max random Y rotation
- Recieve shadows - Depends on your material
- Terrain/Ground layer - You can define layers where your meshes can be spawned
- Material - The material you want your meshes to have
- Meshes array
  - Set the mesh and shadow option for each LOD individually

## Behind the scenes
The script will create a box where it shoots down raycasts to detect possible mesh positions
![Alt text](Screenshots/Volume_box.png?raw=true "Volume box")

I got the best results with overlapping volumes
![Alt text](Screenshots/Voumes_overlap.png?raw=true "Volumes overlapping")
![Alt text](Screenshots/Rendering.png?raw=true "Volumes overlapping")

## Material config
Be sure to check GPU instancing on your material or it can't be instanced

![Alt text](Screenshots/Material.png?raw=true "Material")


 
# Information for GrassInstancerIndirect.cs
## How to use
- Drag the script and the GrassIndirect.shader file into your project
- Create an empty transform and attach the script
- Adjust the settings
- Create a material with the shader (or your own shader)

## Example settings
![Alt text](Screenshots/Indirect_settings.png?raw=true "Indirect settings")

## Properties explained
- Draw Gizmos - Will draw a bounding box inside OnDrawGizmos() for debugging
- Instances - The amount of grass blades or other meshes
- True Instance Count - Displays the amount of instances that were actually instanced
- Range - Range of the bounding box where rays for mesh positions will be tested
- Scale min/max - You can scale your meshes randomly between a min and max value
- Steepness - Limit the steepness your meshes can be spawned on
- Rotate to ground normal - Rotates the mesh towards the ground 
- Random Y axis rotation - Rotates the mesh randomly on the Y axis between a negative and positive value
- Max Y rotation - Max random Y rotation
- Recieve shadows - Depends on your material
- Terrain/Ground layer - You can define layers where your meshes can be spawned
- Material - The material you want your meshes to have
- Main Light - The main light transform of your scene
- Mesh - The mesh you want to render

## Behind the scenes
The script will also create a box where it shoots down raycasts to detect possible mesh positions.

For this approach I recommend using one big volume
![Alt text](Screenshots/Indirect_bhs.png?raw=true "Indirect behind the scenes")

Approximately 2 million grass blades at 60 FPS
![Alt text](Screenshots/Indirect_rendering.png?raw=true "Volumes overlapping")


## Material config
![Alt text](Screenshots/Indirect_Material.png?raw=true "Material")

## Shader info

### GrassIndirect.shader
Custom URP unlit shader without shadows which is basically a recreation of the shadergraph.

### GrassInstancerIndirect_Lighting.shader
Custom URP unlit shader with shadow casting and recieving.
Only tested with Unity 2021.3.6f1 and URP 12.7.1.
Fog not included.

### Grass.shadergraph
You can use the shadergraph if you are using Unity 2021.2 or higher, otherwise you won't have access to the InstanceID.

Here's the graph itself:

Fragment shader
![Alt text](Screenshots/Fragment_shader.png?raw=true "Fragment shader")

Vertex shader part 1
![Alt text](Screenshots/Vertex_shader_1.png?raw=true "Vertex shader 1")

Vertex shader part 2
![Alt text](Screenshots/Vertex_shader_2.png?raw=true "Vertex shader 2")

## Known issues
My approach on grass rendering isn't that optimized. You might want to have a look at Acerola's channel, he has also uploaded his code to Github: https://www.youtube.com/c/Acerola_t