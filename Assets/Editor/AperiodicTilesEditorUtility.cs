
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

using UnityEngine;
using UnityEditor;

using Common.Core.Threading;
using Common.Core.Time;
using ImageProcessing.Images;

namespace AperiodicTexturing
{

    public static class AperiodicTilesEditorUtility
    {

        /// <summary>
        /// Create the exemplar set the tiles will use to find matches.
        /// </summary>
        /// <param name="images"></param>
        /// <param name="isTileable"></param>
        /// <param name="size"></param>
        /// <param name="variants"></param>
        /// <returns></returns>
        public static ExemplarSet CreateExemplarSetByCropping(IList<ColorImage2D> images, bool isTileable, int size, EXEMPLAR_VARIANT variants)
        {
            //Create the exemplars from the images.
            var set = new ExemplarSet(images, isTileable, size);

            //Create the required number of tiles by cropping source  image.
            set.CreateExemplarsFromCrop();

            //Create the variants from the exempars in the set.
            set.CreateVariants(variants);

            return set;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="images"></param>
        /// <param name="isTileable"></param>
        /// <param name="size"></param>
        /// <param name="seed"></param>
        /// <param name="variants"></param>
        /// <returns></returns>
        public static ExemplarSet CreateExemplarSetByRandomSampling(IList<ColorImage2D> images, bool isTileable, int size, int seed, EXEMPLAR_VARIANT variants)
        {
            var set = new ExemplarSet(images, isTileable, size);

            set.CreateExemplarsFromRandom(100, seed, 0.25f);
            set.CreateVariants(variants);

            return set;
        }

        /// <summary>
        /// Create the tiles to make tileable by taking 
        /// random samples from a exemplar set of the source images.
        /// </summary>
        /// <param name="images"></param>
        /// <param name="isTileable"></param>
        /// <param name="size"></param>
        /// <param name="numTiles"></param>
        /// <param name="seed"></param>
        /// <returns></returns>
        public static List<Tile> CreateTilesByRandomSampling(IList<ColorImage2D> images, bool isTileable, int size, int numTiles, int seed)
        {
            var set = new ExemplarSet(images, isTileable, size);

            set.CreateExemplarsFromRandom(numTiles, seed, 0.1f);
            var tiles = set.GetRandomTiles(numTiles, seed);

            //Name the images (for debugging).
            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];

                for (int j = 0; j < tile.Images.Count; j++)
                {
                    tile.Images[j].Name = $"Tile{i}_Image{j}";
                }
            }

            return tiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tileSet"></param>
        /// <param name="folderName"></param>
        /// <param name="fileName"></param>
        public static void SaveTiles(WangTileSet tileSet, string folderName, string fileName)
        {
            int tileTextureWidth = tileSet.NumHTiles;
            int tileTextureHeight = tileSet.NumVTiles;

            int tileSize = tileSet.TileSize;
            int width = tileSize * tileTextureWidth;
            int height = tileSize * tileTextureHeight;

            for (int k = 0; k < tileSet.NumTileImages; k++)
            {
                Color[] pixels = new Color[width * height];

                for (int x = 0; x < tileTextureWidth; x++)
                {
                    for (int y = 0; y < tileTextureHeight; y++)
                    {
                        var tile = tileSet.Tiles[x, y];

                        for (int i = 0; i < tileSize; i++)
                        {
                            for (int j = 0; j < tileSize; j++)
                            {
                                int xi = x * tileSize + i;
                                int yj = y * tileSize + j;

                                pixels[xi + yj * width] = tile.Tile.Images[k][i, j].ToColor();
                            }
                        }
                    }
                }

                var tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
                tex.SetPixels(pixels);
                tex.Apply();

                string folder = Application.dataPath + "/" + folderName;
                string hv = tileSet.NumHColors + "x" + tileSet.NumVColors;
                string name = folder + "/" + fileName + k + "_" + hv + ".png";

                System.IO.File.WriteAllBytes(name, tex.EncodeToPNG());

                Debug.Log("Saved texture " + name);
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 
        /// </summary>
        public static void SaveTiles(IList<Tile> tiles, string folderName, string[] fileNames)
        {
            folderName = Application.dataPath + "/" + folderName;

            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];

                for (int j = 0; j < tile.Count; j++)
                {
                    var tex = ToTexture(tile.Images[j]);
                    var id = i.ToString() + j.ToString();

                    string fileName = folderName + "/" + fileNames[j] + id + ".png";

                    System.IO.File.WriteAllBytes(fileName, tex.EncodeToPNG());

                    Debug.Log("Saved texture " + fileName);
                }

            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Create images from the source textures.
        /// </summary>
        /// <returns></returns>
        public static List<ColorImage2D> CreateImages(IList<Texture2D> textures)
        {
            var images = new List<ColorImage2D>();
            for (int i = 0; i < textures.Count; i++)
            {
                if (textures[i] == null) continue;
                images.Add(ToImage(textures[i]));
            }

            return images;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="textures"></param>
        /// <returns></returns>
        public static List<Tile> CreateTiles(IList<Texture2D[]> textures)
        {
            var tiles = new List<Tile>();

            foreach (var texArray in textures)
            {
                var images = new List<ColorImage2D>();

                foreach (var tex in texArray)
                {
                    if (tex == null) continue;
                    images.Add(ToImage(tex));
                }

                var tile = new Tile(images);
                tiles.Add(tile);
            }

            return tiles;
        }

        /// <summary>
        /// Create a image a textures.
        /// </summary>
        /// <param name="tex"></param>
        /// <returns></returns>
        public static ColorImage2D ToImage(Texture2D tex)
        {
            var image = new ColorImage2D(tex.width, tex.height);

            image.Fill((x, y) =>
            {
                return tex.GetPixel(x, y).ToColorRGBA();
            });

            image.Name = tex.name;

            return image;
        }

        /// <summary>
        /// Create a texture from a image.
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public static Texture2D ToTexture(ColorImage2D image)
        {
            var tex = new Texture2D(image.Width, image.Height, TextureFormat.ARGB32, false);

            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    tex.SetPixel(x, y, image[x, y].ToColor());
                }
            }

            tex.Apply();

            return tex;
        }

    }

}
