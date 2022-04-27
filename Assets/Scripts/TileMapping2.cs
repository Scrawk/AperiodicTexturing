
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;

using Common.Core.Colors;
using ImageProcessing.Images;

namespace AperiodicTexturing
{
	public class TileMapping2 : MonoBehaviour
	{

		//This is the source image the tiles are created from. 
		//You must enable read/write and if it is a non-pow2 you must set that to none in advanced settings
		public Texture2D m_sampleTexture;
		//This is just the material used to display the results.
		public Material m_tileMappingMat;
		//The number of horizontal colors
		public int m_numHColors = 2;
		//The number of vertical colors
		public int m_numVColors = 2;

		//This is the size of each individual wang tile
		public int m_tileSize = 128;

		//Each number will produce a different mapping index
		public int m_mappingSeed = 0;
		//The mapping texture size
		public int m_mappingWidth = 8;
		public int m_mappingHeight = 8;

		//If you save the textures generated this will be the file name
		public string m_resultFileName = "Result Textures/ResultTiles";

		Texture2D m_tileTexture;
		Texture2D m_tileMappingTexture;

		void Start()
		{

			var source = ToImage(m_sampleTexture);

			var exemplarSet = new ExemplarSet(source, m_tileSize);
			exemplarSet.CreateExemplarsFromRandom(0, 8);

			var exemplars = exemplarSet.GetRandomExemplars(Math.Max(m_numHColors, m_numVColors), 0);

			var tileables = new ColorImage2D[exemplars.Count];

			float startTime = Time.realtimeSinceStartup;

			for (int i = 0; i < tileables.Length; i++)
			{
				var exemplar = exemplars[i];

				var tileable = Tileable.MakeImageTileable(exemplar.Image, exemplarSet);

				tileables[i] = tileable;

				tileable.SaveAsRaw("C:/Users/Justin/OneDrive/Desktop/tileable" + i + ".raw");
			}

			float endTime = Time.realtimeSinceStartup;
			Debug.Log("Created tilables in " + (endTime - startTime) + " seconds");

			exemplarSet.ResetUsedCount();

			var tileSet = new WangTileSet(m_numHColors, m_numVColors, m_tileSize);

			startTime = Time.realtimeSinceStartup;

			foreach (var tile in tileSet.Tiles)
			{
				ImageSynthesis.CreateTileImage(tile, tileables);

				if(!tile.IsConst)
					break;
			}

			endTime = Time.realtimeSinceStartup;
			Debug.Log("Created tiles in " + (endTime - startTime) + " seconds");

			var tileMapping = tileSet.CreateTileMap(m_mappingHeight, m_mappingWidth, m_mappingSeed);

			m_tileTexture = CreateTileTexture(tileSet);

			m_tileMappingTexture = CreateMappingTexture(tileMapping, tileSet);

			float tileTexWidth = m_tileTexture.width / m_tileSize;
			float tileTexHeight = m_tileTexture.height / m_tileSize;
			Vector2 tileScale = new Vector2(tileTexWidth, tileTexHeight);
			Vector2 tileMappingScale = new Vector2(m_tileMappingTexture.width, m_tileMappingTexture.height);

			m_tileMappingMat.SetTexture("_TilesTexture", m_tileTexture);
			m_tileMappingMat.SetTexture("_TileMappingTexture", m_tileMappingTexture);
			m_tileMappingMat.SetVector("_TileScale", tileScale);
			m_tileMappingMat.SetVector("_TileMappingScale", tileMappingScale);
			m_tileMappingMat.SetFloat("_UseCornerMapping", 0.0f);

			System.IO.File.WriteAllBytes(Application.dataPath + "/" + m_resultFileName + "_WangTiles.png", m_tileTexture.EncodeToPNG());
			System.IO.File.WriteAllBytes(Application.dataPath + "/" + m_resultFileName + "_WangMapping.png", m_tileMappingTexture.EncodeToPNG());

			foreach (var tile in tileSet.Tiles)
			{
				tile.AddEdgeColor(4, 0.5f);
			}

			m_tileTexture = CreateTileTexture(tileSet);
			System.IO.File.WriteAllBytes(Application.dataPath + "/" + m_resultFileName + "_WangTiles_Colors.png", m_tileTexture.EncodeToPNG());

		}

        private void Update()
        {
			return;
			float tileTexWidth = m_tileTexture.width / m_tileSize;
			float tileTexHeight = m_tileTexture.height / m_tileSize;
			Vector2 tileScale = new Vector2(tileTexWidth, tileTexHeight);
			Vector2 tileMappingScale = new Vector2(m_tileMappingTexture.width, m_tileMappingTexture.height);

			m_tileMappingMat.SetTexture("_TilesTexture", m_tileTexture);
			m_tileMappingMat.SetTexture("_TileMappingTexture", m_tileMappingTexture);
			m_tileMappingMat.SetVector("_TileScale", tileScale);
			m_tileMappingMat.SetVector("_TileMappingScale", tileMappingScale);
		}

        void OnGUI()
		{
			int size = 384;

			if (m_tileTexture != null)
				GUI.DrawTexture(new Rect(0, 0, size, size), m_tileTexture);

		}

		ColorImage2D ToImage(Texture2D tex)
        {
			var image = new ColorImage2D(tex.width, tex.height);

			image.Fill((x, y) =>
			{
				return tex.GetPixel(x, y).ToColorRGB();
			});

			return image;
        }

		Texture2D CreateTileTexture(WangTileSet set)
		{
			int tileTextureWidth = set.NumHTiles;
			int tileTextureHeight = set.NumVTiles;

			int width = m_tileSize * tileTextureWidth;
			int height = m_tileSize * tileTextureHeight;

			Color[] tileData = new Color[width * height];

			for (int x = 0; x < tileTextureWidth; x++)
			{
				for (int y = 0; y < tileTextureHeight; y++)
				{
					var tile = set.Tiles[x, y];

					for (int i = 0; i < m_tileSize; i++)
					{
						for (int j = 0; j < m_tileSize; j++)
						{
							int xi = x * m_tileSize + i;
							int yj = y * m_tileSize + j;

							tileData[xi + yj * width] = tile.Image[i, j].ToColor();
						}
					}
				}
			}

			var tileTexture = new Texture2D(width, height, TextureFormat.ARGB32, true);
			tileTexture.SetPixels(tileData);
			tileTexture.Apply();

			return tileTexture;
		}


		Texture2D CreateMappingTexture(WangTile[,] tiles, WangTileSet set)
		{
			int width = tiles.GetLength(0);
			int height = tiles.GetLength(1);

			Color32[] data = new Color32[width * height];

			for (int i = 0; i < width; i++)
			{
				for (int j = 0; j < height; j++)
				{
					var index = tiles[i, j].Index;
					data[i + j * width] = new Color32((byte)index.x, (byte)index.y, 0, 0);
				}
			}

			var tileMappingTexture = new Texture2D(width, height, TextureFormat.RGB24, false, true);
			tileMappingTexture.SetPixels32(data);
			tileMappingTexture.filterMode = FilterMode.Point;
			tileMappingTexture.wrapMode = TextureWrapMode.Repeat;
			tileMappingTexture.Apply();

			return tileMappingTexture;
		}

	}
}
