# Infinite grass rendering
## This approach extends and improves all previous techniques

## Showcase: https://www.youtube.com/watch?v=4A0QP5x0MOE
![Alt text](../Screenshots/infinite-grass.png?raw=true "")

## About this approach

#### Made with Unity 6 (6000.0.26f1) and URP 17.0.3

This system extends the last (no. 5) approach.
In here I've provided a whole Unity project to give you a kickstart if you want to use it for your project.



### What's new
- overall major performance increase
- rendering infinite amount of grass by loading and unloading big chunks that contain small chunks
- grass settings can be controlled via a scriptable object to create many possible foliage assets
- 3 LOD levels are now supported
- texture arrays supported
- implemented fading
- improved grass appearance
  - fixed shadow coords causing weird artifacts
  - distortion based on view angle [from this GDC tech talk](https://youtu.be/wavnKZNSYqU?t=1155)
  - fixed diffuse lighting issues

### How it works

In principle the rendering works the same as the previous (no. 5) approach, except we are now using a new layer of bigger chunks
[(Read detailed description of previous system)](https://github.com/MangoButtermilch/Unity-Grass-Instancer/blob/main/5.%20Occlusion%20Culling%20%2B%20High%20performance/README.md#detailed-explanation).

I can't write up how it works in detail right now. I am though working on a script for a video that will explain everything.
With that I'll also rephrase the script for just a plain text description. 

But that'll take some time.

## Known issues

### Large chunks appear to "flicker" depending on view angle

This can happen if the instancer is not positioned correctly. It should be placed in the very center (x, y and z) of your terrain.

### Small chunks appear to "flicker" depending on view angle

This can happend if the `depth bias` value is not fine tuned. You can play around with it in play mode to find a suiting value.

Another issue could be the `camera depth texture` if you have both scene and game view open or switch between these two during testing. Somehow the creation of this texture can be messed up.

### Grass placement seems repetitive

This gets noticable at a small `sub chunk size` at like `1` to `8`. This has to do with how the noise functions are seeded inside the compute shader threads and is an issue I could not find a fix for. In my showcases I've been using `16`.

### [More issues can be found here.](https://github.com/MangoButtermilch/Unity-Grass-Instancer/blob/main/README.md#faq)


## Asset credits

### Foliage 

#### Some foliage textures made by FabinhoSC used under CC0 license:
- https://opengameart.org/users/fabinhosc
- https://opengameart.org/content/stylized-grass-and-bush-textures-0

#### Some foliage textures made by Reiner used under CC0 license:
- https://www.reinerstilesets.de/new-textures-billboard-grass/

#### Terrain height map used under CC0 license:
Rugged Terrain with Rocky Peaks Height Map
- https://www.motionforgepictures.com/height-maps/

#### Terrain rock texture by ambientCG used under CC0 license:
- https://ambientcg.com/view?id=Rocks011