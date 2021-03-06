## Aperiodical texture tiling in Unity

This project is based on the rather old aperiodical texturing project found on the [GPU 
gems website](https://developer.nvidia.com/gpugems/gpugems2/part-ii-shading-lighting-and-shadows/chapter-12-tile-based-texture-mapping) which is in turn based on [wang tiles](https://en.wikipedia.org/wiki/Wang_tile) and the [fast texture synthesis paper](https://graphics.stanford.edu/papers/texture-synthesis-sig00/texture.pdf).

This project also depends on the [image processing](https://github.com/Scrawk/ImageProcessing) and [common code](https://github.com/Scrawk/Common) library's.

**Note: The current method does not handle creating tileable height maps very well and  
creating the aperiodic tiles also takes a very long time. I am still looking into the best way to do this.**

There are a lot of methods used to help break up the repeating patterns when texturing but they 
all involve resampling the texture multiple times and blending the results somehow.
The good thing about this method is it only takes one extra texture sample per shader and 
requires no blending of textures.

I think this method had a lot of promise and never got the attention it deserved. 
I think this comes down to the fact its quite difficult to make the textures. 
The wang tiles need to be constucted in a special way.
To help remedy this I have created a series of editor scripts in Unity that will automatically 
generate the tiles for you. Its not perfect but any issues can easily be cleaned up in photoshop.

So first you will need a series of tileable textures. You will need one for each 'color' the wang 
tiles have. The project supports upto 4 colors. 
Technically you can have more but the number of tiles generated quickly becomes to large to be 
practical.

You can provide your own tileable texturs and three optional textures are supported like normals 
or roughness. If so you can move onto the next stage. You just need to make sure the textures are 
not to big. I have provide some free textures found on [PolyHaven](https://polyhaven.com/).

These textures will be stitched together to form the finally wang tile so with two colors the 
final tile will be 4 by 4. That means a 256 texture will end up being 1025 by 1024.

If you need to generate the textures you can use the editor script. 

# Tileable Texture Generation.

Go to windows->Aperiodical texturing->Create tileable images

![tileableEditorWindow](https://github.com/Scrawk/AperiodicTexturing/blob/master/Media/CreateTileableTexturesWindow.png)

**Number of tiles:** The number of tileable textures to create from the source images. you will need at least one for each tile color.

**Tile size:** The size of the tiles created. 

**Exemplar size:** The size of the exemplars used to fill patches when creating the tileable textures.

**Max exemplars:** The max number of exemplars to create in the set.

**Variants:** What variants of the exemplars should be generated in the set. Theses can be rotations of the original and can increase the chance of finding better matches when patch filling.

**Use multi-threading:** Should multi-threading be used. Can be turnned of for debugging.

**Source is tileable:** Are the source images tileable. If so this makes sampling the images easier when creating the exemplars.

**Seed:** A seed used for the random generator. a different seed will produce different results.

**Output folder:** Where the resulting textures are output.

**File name:** The resulting textues names.

**Source textures:** These are the source images used to generate the tiles. The first is required and the following 3 are optional. All the textures must have read/write enabled.

Press create to make the textures. The results will be saved in the results folder. This may take a long time to compute.

# Aperiodic Texture Generation

The next stage requires using the tileable textures just generated to make the wang tile and mapping texture.

Go to windows->Aperiodical texturing->Create aperiodic tiles

![AperiodirEditorWindow](https://github.com/Scrawk/AperiodicTexturing/blob/master/Media/CreateAperiodicTexturesWindow.png)

**Number of horizontal colors:** The wang tile will have this number of colors squared (ie if for 2 colors you get 4 tiles) along its width.

**Number of vertical colors:** The wang tile will have this number of colors squared (ie if for 2 colors you get 4 tiles) along its height.

**Tile size:** The size of the tiles created. 

**Exemplar size:** The size of the exemplars used to fill patches when creating the tileable textures.

**Max exemplars:** The max number of exemplars to create in the set.

**Variants:** what variants of the exemplars should be generated in the set. theses can be rotations of the original and can increase the chance of finding better matches when patch filling.

**Use multi-threading:** Should multi-threading be used. Can be turnned of for debugging.

**Source is tileable:** Are the source images tileable. If so this makes sampling the images easier when creating the exemplars.

**Seed:** A seed used for the random generator. A different seed will produce different results.

**Output folder:** Where the resulting textures are output.

**File name:** The resulting textues names.

**Source textures:** These are the source images used to generate the tiles. The first is required and the following 3 are optional. All the textures must have read/write enabled. These textures should be the same ones that were used to create the orginal tileable textures if possible.

**Tileable textures:** These are the tileable textures generated in the previous stage and like the source textures The first is required and the next tree are optional. All the textures must have the same number of optional textures however.

Press create to make the textures. The results will be saved in the results folder. This may take a long time to compute.

# Creating the mapping texture

Once you have created the wang tile all thats needed now is a mapping texture. You can create one using the provided editor script or you can use a pre-generated one found in the follow folder 'AperiodicalTexturing/Textures/Mappings'. You will need to pick one that matches the settings you used to create the wang tile. So if you picked 2 colors use the 2x2 texture. The number on end of the texture, ie 256 is just the size of the virtual texture the mapping creates. So for example if you used a tile size of 256 and a mapping texture size of 256 that means the virtual texture created is 256 by 256 = 65536 in size before it repeats.

The mapping texture must be point sampled, repeat, mipmaps disabled and have no compression with a format of RGBA 32 bit.

To create you own mapping texture go to windows->aperiodical texturing->create tile mapping

![CreateMappingTexture](https://github.com/Scrawk/AperiodicTexturing/blob/master/Media/CreateMappingTexturesWindow.png)

**Number of horizontal colors:** The wang tile will have this number of colors squared (ie if for 2 colors you get 4 tiles) along its width. Must match the settings you used to create the wang tiles.

**Number of vertical colors:** The wang tile will have this number of colors squared (ie if for 2 colors you get 4 tiles) along its height. Must match the settings you used to create the wang tiles.

**Mapping texture width:** This will be the number of tiles along the virtual textures width. 256 or 512 is a good size.

**Mapping texture height:** This will be the number of tiles along the virtual textures height. 256 or 512 is a good size.

**Seed:** A seed used for the random generator. A different seed will produce different results.

**Output folder:** Where the resulting textures are output.

**Mapping file name:** The resulting textues names.

Press create to make the textures. The results will be saved in the results folder.

# Creating the material.

The last step is to create the material. 

![creatematerial](https://github.com/Scrawk/AperiodicTexturing/blob/master/Media/MaterialSettings.png)

**Albedo Texture:** This is the aperiodic albedo texture.

**Smoothness Texture:** This is the aperiodic smoothness texture.

**Bumpmap Texture:** This is the aperiodic bump map texture.

**Tile Mapping Texture:** This is the mapping texture.

**Tile Scale:** The x value should be set to the horizontal edge colors squared. 
So if you 2 edge colors when creating the aperiodic texture you need to enter 4 here, 3 colors enter 9, 4 colors enter 16.
Its the same for the y value exected it should be the number of vertical colors squared.

**Metallic Scale:** The metallicnesss value used.

**Smoothness Scale:** The smoothness value from the soomthness texture is multiplied by this value.

# Summary

At this point you maybe wondering how the wang tiles are created. Heres a example of a 2 by 2 color wang tile. You can see that it consists of 4 by 4 tiles and they always have the same edge colors when they meet another tile.

![wangtile](https://github.com/Scrawk/AperiodicTexturing/blob/master/Media/SolidColorTile2x2_256.png)

Heres another example that just shows the edges.

![wangtileedge](https://github.com/Scrawk/AperiodicTexturing/blob/master/Media/ColorTile2x2_256.png)

Heres a few screen shots of the materials on a plane with a 1m cube for scale. Here the material is using 2 edge colors you can still see some repeating but there is a nice breaking up of the tiles.

![AperiodicTexturing2x2](https://github.com/Scrawk/AperiodicTexturing/blob/master/Media/AperiodicTexturing2x2.png)

With the edge colors showing.

![Colors2x2](https://github.com/Scrawk/AperiodicTexturing/blob/master/Media/Colors2x2.png)

And here a material with 3 colors. Again some repeating but much better than the 2 colors.

![AperiodicTexturing3x3](https://github.com/Scrawk/AperiodicTexturing/blob/master/Media/AperiodicTexturing3x3.png)

With the edge colors showing.

![Colors3x3](https://github.com/Scrawk/AperiodicTexturing/blob/master/Media/Colors3x3.png)

It can be hard to create a tile that has no artifacts or obvious repeating features. As a optional steep the textures can be cleaned up in photoshop and you can used this edge color texture as a guide of where you can edit and what areas can no be changed. Basically you can not change the pixels on the edges of the tile where they are colored.

I have included a bunch of pre-generated textures and materials which you can use and they can be found in the textures and materials folder.




