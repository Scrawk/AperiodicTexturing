This project is based on the rather old aperiodical texturing project found on the GPU 
gems website which is in turn based on wang tiles and the image wuiting paper.
There are a lot of methods used to help break up the repeating patterns when texturing but they 
all involve resample the texture miltle times and blending the results somhow.
The good thing abount this method is it only takes one extra texture sample per shader and 
request no blending of textures.

I think think this method had a lot of promise and never got the attention it deserved. 
I think this comes down to the fact its quite difficult to make the textures. 
The wang tiles need to be constucted in a special way.
To help remedy this I have created a series of editor scripts in Unity that will automatically 
generate the tiles for you. Its not perfert but any issues can easily be cleaned up in photoshop.

So first you will need a series of tileable textures. You will need one for each 'color' the wang 
tiles have. The project supports upto 4 colors. 
Technically you can have more but the number of tiles generated quickly becomes to large to be 
practical.

you can provide your own tileable texturs and three optional textures are supported like normals 
or roughness. If so you can move unto the new stage. You just need to make sure the textures are 
not to big. 
these textures will be stitched together to form the finally wang tile so with two colors the 
final tile will be 4 by 4. That means a 256 texture will end up being 1025 by 1024.


if you need to generate the textures you can use the editor script. 

Go to windows->aperiodical texturing->create tileables

![tileableEditorWindow](https://github.com/Scrawk/AperiodicTexturing/blob/master/Media/CreateTileableTexturesWindow.png)

Press create to make the textures. The results will be saved in the results folder. 
