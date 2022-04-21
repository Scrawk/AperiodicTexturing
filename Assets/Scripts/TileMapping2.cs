
using System;
using System.Collections.Generic;

using UnityEngine;

using Common.Core.Colors;
using ImageProcessing.Images;

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
	
	WangTileSet m_tileSet;
	WangTile[,] m_tileCompaction;
	WangTile[,] m_tileMapping;
	
	Texture2D m_tileTexture;
	Texture2D m_colorTileTexture;
	Texture2D m_tileMappingTexture;
	
	void Start () 
	{

		m_tileSet = new WangTileSet(m_numHColors, m_numVColors, m_tileSize);

		m_tileCompaction = m_tileSet.OrthogonalCompaction();

		m_tileMapping = m_tileSet.SequentialTiling(m_mappingHeight, m_mappingWidth, m_mappingSeed);

		m_tileTexture = CreateTileTexture(m_tileCompaction);

		m_colorTileTexture = CreateTileTexture(m_tileCompaction);

		m_tileMappingTexture = CreateMappingTexture(m_tileMapping);

		
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

	}
	
	void OnGUI()
	{
		int size = 384;
		
		if(m_tileTexture != null)
			GUI.DrawTexture(new Rect(0, 0, size, size), m_tileTexture);

	}

	Texture2D CreateTileTexture(WangTile[,] compaction)
	{

		int tileTextureHeight = compaction.GetLength(0);
		int tileTextureWidth = compaction.GetLength(1);

		int glTileTextureHeight = m_tileSize * tileTextureHeight;
		int glTileTextureWidth = m_tileSize * tileTextureWidth;

		Color[] tileData = new Color[glTileTextureWidth * glTileTextureHeight];

		for (int x = 0; x < tileTextureWidth; x++)
		{
			for (int y = 0; y < tileTextureHeight; y++)
			{
				//int idx = set.GetIndex(compaction[y, x]);
				var tile = compaction[y, x];

				for (int i = 0; i < m_tileSize; i++)
				{
					for (int j = 0; j < m_tileSize; j++)
					{
						tileData[(x * m_tileSize + i) + (y * m_tileSize + j) * glTileTextureWidth] = tile.Image[m_tileSize - 1 - i, j].ToColor();
					}
				}
			}
		}

		var tileTexture = new Texture2D(glTileTextureWidth, glTileTextureHeight, TextureFormat.ARGB32, true);
		tileTexture.SetPixels(tileData);
		tileTexture.Apply();

		return tileTexture;
	}


	Texture2D CreateMappingTexture(WangTile[,] tiles)
	{
		int width = tiles.GetLength(0);
		int height = tiles.GetLength(1);

		Color32[] data = new Color32[width * height];

        for(int i = 0; i < width; i++)
		{
            for(int j = 0; j < height; j++)
            {
                int id = tiles[j,i].id;
                int row = 0, col = 0;

				WangTileSet.TileLocation(m_tileCompaction, id, ref row, ref col);
                
                data[i+j*width] = new Color32( (byte)col, (byte)row, 0, 0);
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
