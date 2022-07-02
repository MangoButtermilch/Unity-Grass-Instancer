# Unity Grass Instancer
## Contains a C# script and a shader for GPU instanced grass
### Made with Unity 2020.3.4f1 and HDRP 10.4.0

![Alt text](Screenshots/showcase.gif?raw=true "Showcase")
Video showcase: https://www.youtube.com/watch?v=pJSKUQJqBUs

## Resources
- I got started with this project by this great video about GPU instancing: https://www.youtube.com/watch?v=eyaxqo9JV4w
- I found a lot of information about grass rendering on Acerola's youtube channel: https://www.youtube.com/c/Acerola_t
  - I'm also using his 3D-Models in my showcase

## How to use
- Drag the script and the shader into your project
- Create an empty transform and attach the script
- Adjust the settings
- Add a spriterenderer 
  - with this simple trick we can get some frustum culling for free and don't have to implement it ourselves. Not the best solution but it works alright.
- Create a material with the shader (or your own shader)

## Example settings
![Alt text](Screenshots/Settings.png?raw=true "Settings")

## Behind the scenes
- The script will create a box where it shoots down raycasts to detect possible mesh positions
![Alt text](Screenshots/Volume_box.png?raw=true "Volume box")

- I got the best results with overlapping volumes
![Alt text](Screenshots/Voumes_overlap.png?raw=true "Volumes overlapping")
![Alt text](Screenshots/Rendering.png?raw=true "Volumes overlapping")

## Known issues
- The grass does not always bend in the correct wind direction inside the shader
![Alt text](Screenshots/Issue.png?raw=true "Grass bending")

- My approach on grass rendering isn't that optimized. You might want to have a look at Acerola's channel, he has also uploaded his code to Github: https://www.youtube.com/c/Acerola_t
