using System;
using System.Collections.Generic;

using Common.Core.Numerics;
using Common.Core.Colors;
using ImageProcessing.Images;

namespace AperiodicTexturing
{
	/// <summary>
	/// A set of wang tiles.
	/// </summary>
	public class WangTileSet
	{
		/// <summary>
		/// Create a new set of wang tiles.
		/// </summary>
		/// <param name="numHColors">The number of colors on the x axis (must be 2-4)</param>
		/// <param name="numVColors">The number of colors on the y axis (must be 2-4)</param>
		/// <param name="tileSize">The tiles image data size.</param>
		public WangTileSet(int numHColors, int numVColors, int tileSize)
		{
			NumHColors = Math.Clamp(numHColors, 2, 4);
			NumVColors = Math.Clamp(numVColors, 2, 4);
			NumTiles = numHColors * numHColors * numVColors * numVColors;
			TileSize = tileSize;

			Tiles = new WangTile[NumHTiles, NumVTiles];

			//Get the order the tiles need to be on the each axis
			//so each tiles edge color matchs is neighour tiles.
			var HEdges = GetEdgeOrder(NumHColors);
			var VEdges = GetEdgeOrder(NumVColors);

			//Create the 2D array of tiles where each tiles edge colors match its 4 surrounding neighbours.

			for (int j = 0; j < NumVTiles; j++)
			{
				for (int i = 0; i < NumHTiles; i++)
				{
					int left = HEdges[i];
					int bottom = VEdges[j];
					int right = HEdges[i + 1];
					int top = VEdges[j + 1];

					var tile = new WangTile(left, bottom, right, top, tileSize);
					tile.Index1 = i + j * NumHTiles;
					tile.Index2 = new Point2i(i, j);

					Tiles[i, j] = tile;
				}
			}

		}

		/// <summary>
		/// The number of colors on the horizontal xaxis.
		/// </summary>
		public int NumHColors { get; private set; }

		/// <summary>
		/// The number of colors on the vertical yaxis.
		/// </summary>
		public int NumVColors { get; private set; }

		/// <summary>
		/// The number of tiles on the horizontal xaxis.
		/// </summary>
		public int NumHTiles => NumHColors * NumHColors;

		/// <summary>
		/// The number of tiles on the vertical yaxis.
		/// </summary>
		public int NumVTiles => NumVColors * NumVColors;

		/// <summary>
		/// The number of images in each tile.
		/// </summary>
		public int NumTileImages => Tiles[0, 0].Tile.Images.Count;

		/// <summary>
		/// The total number of tiles in the set.
		/// </summary>
		public int NumTiles { get; private set; }

		/// <summary>
		/// The size of a tiles image data.
		/// </summary>
		public int TileSize { get; private set; }

		/// <summary>
		/// The 2D array of the tiles.
		/// </summary>
		public WangTile[,] Tiles { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return String.Format("[WangTileSet: NumHColors={0}, NumVColors={1}, NumHTiles={2}, NumVTiles={3}, TileSize={4}]", 
				NumHColors, NumVColors, NumHTiles, NumVTiles, TileSize);
		}

		/// <summary>
		/// Get the 2D array of tiles as a 1D list.
		/// </summary>
		/// <returns></returns>
		public List<WangTile> ToFlattenedList()
        {
			var tiles = new List<WangTile>(Tiles.Length);
			foreach (var tile in Tiles)
				tiles.Add(tile);

			return tiles;
		}

		/// <summary>
		/// Add the edge colors to all the tiles in the set.
		/// </summary>
		/// <param name="thickness">The thickness of the border.</param>
		/// <param name="alpha">The colors alpha.</param>
		public void AddEdgeColor(int thickness, float alpha)
        {
			foreach (var tile in Tiles)
				tile.AddEdgeColor(thickness, alpha);
        }

		/// <summary>
		/// Create a new tile mapping where the mapping is a 2D array of 
		/// randomly selected tiles with the constraint that each tiles 
		/// edge colors must match is 4 neighbours colors.
		/// </summary>
		/// <param name="numHTiles">The number of tiles on the horizontal xaxis.</param>
		/// <param name="numVTiles">The number of tiles on the vertical yaxis.</param>
		/// <param name="seed">The random generators seed.</param>
		/// <returns></returns>
		public WangTile[,] CreateTileMap(int numHTiles, int numVTiles, int seed)
		{
			var tiles = new WangTile[numHTiles, numVTiles];

			var rnd = new Random(seed);

			for (int j = 0; j < numVTiles; j++)
			{
				for (int i = 0; i < numHTiles; i++)
				{
					WangTile tile = null;

					//Safty break in case a new tile can not be found
					//that is not the same as one of its neigbours.
					int count = 0;

					do
					{
						//Get a new tile with randomly selected edge colors.
						tile = GetNextTile(i, j, tiles, rnd);

						//If the same tile is the same as one of its neighbours 
						//then try again. This will help break up repeating patterns.
						//Might fail (will ty 10 times) to find a different tile if this is the only
						//tile that can match up its edge colors with its neighbours.
					}
					while (IsNeighbourTile(i, j, tiles, tile) && count++ < 10);

					tiles[i, j] = tile.Copy();
				}
			}

			return tiles;
		}

		/// <summary>
		/// Get a new tile with random edge colors with the constraint that the edges colors
		/// match its 4 neighbours edge colors.
		/// </summary>
		/// <param name="i">The first index of the tile in the tiles array.</param>
		/// <param name="j">The second index of the tile in the tiles array.</param>
		/// <param name="tiles"></param>
		/// <param name="rnd"></param>
		/// <returns></returns>
		private WangTile GetNextTile(int i, int j, WangTile[,] tiles, Random rnd)
        {
			int numHTiles = tiles.GetLength(0);
			int numVTiles = tiles.GetLength(1);

			//Get the tiles 4 neighbours indices. 
			//The indices are wrapped around the tile array.
			int im1 = MathUtil.Wrap(i - 1, numHTiles);
			int ip1 = MathUtil.Wrap(i + 1, numHTiles);
			int jm1 = MathUtil.Wrap(j - 1, numVTiles);
			int jp1 = MathUtil.Wrap(j + 1, numVTiles);

			//Get the tiles neighbours edge color. ie the tile to the left
			//must have the same color on its right edge.
			//Tiles might be null if they have not been created yet and get a -1.
			int left = tiles[im1, j] != null ? tiles[im1, j].Right : -1;
			int bottom = tiles[i, jm1] != null ? tiles[i, jm1].Top : -1;
			int right = tiles[ip1, j] != null ? tiles[ip1, j].Left : -1;
			int top = tiles[i, jp1] != null ? tiles[i, jp1].Bottom : -1;

			//For each edge if the color is -1 any random color can be assigned.
			if (left < 0) left = rnd.Next(0, NumHColors);
			if (bottom < 0) bottom = rnd.Next(0, NumVColors);
			if (right < 0) right = rnd.Next(0, NumHColors);
			if (top < 0) top = rnd.Next(0, NumVColors);

			//Create that index the edge colors make up and return the tile at that index.
			var index = TileIndex2D(left, bottom, right, top);

			return Tiles[index.x, index.y];
		}

		/// <summary>
		/// Determine if any of a tiles neighbours are the same.
		/// A tile counts as being the same if all the edge colors
		/// are the same and can be determined by comparing the tiles index.
		/// </summary>
		/// <param name="i"></param>
		/// <param name="j"></param>
		/// <param name="tiles"></param>
		/// <param name="tile"></param>
		/// <returns></returns>
		private bool IsNeighbourTile(int i, int j, WangTile[,] tiles, WangTile tile)
        {
			int numHTiles = tiles.GetLength(0);
			int numVTiles = tiles.GetLength(1);

			for (int x = -1; x <= 1; x++)
            {
				for (int y = -1; y <= 1; y++)
				{
					if (x == 0 && y == 0) continue;

					int ix = MathUtil.Wrap(i - x, numHTiles);
					int jy = MathUtil.Wrap(j - y, numVTiles);

					if (tiles[ix, jy] == null)
						continue;

					if (tiles[ix, jy].Index2 == tile.Index2)
						return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Given a number of edge colors then create a array
		/// where each edge color is used and matched its neighbours edges.
		/// </summary>
		/// <param name="numColors"></param>
		/// <returns></returns>
		private int[] GetEdgeOrder(int numColors)
		{
			var edges = new int[numColors * numColors + 1];

			for (int i = 0; i < numColors; i++)
			{
				for (int j = 0; j < numColors; j++)
				{
					int index = TileIndex1D(i, j);

					//The first number represents the tiles color
					//on the left side and the second on the right side.
					edges[index] = i;
					edges[index + 1] = j;
				}
			}

			return edges;
		}

		/// <summary>
		/// Given the 4 edges colors get the index the tile 
		/// should be in a 2D array where its colors will match its neighbour tiles.
		/// </summary>
		private Point2i TileIndex2D(int left, int bottom, int right, int top)
		{
			Point2i index;
			index.x = TileIndex1D(left, right);
			index.y = TileIndex1D(bottom, top);

			return index;
		}

		/// <summary>
		/// Given the 4 edges colors get the index the tile 
		/// should be in a 1D array where its colors will match its neighbour tiles.
		/// </summary>
		/// <param name="left"></param>
		/// <param name="bottom"></param>
		/// <param name="right"></param>
		/// <param name="top"></param>
		/// <returns></returns>
		public int TileIndex1D(int left, int bottom, int right, int top)
		{
			return (left * (NumVColors * NumHColors * NumVColors) + bottom * (NumHColors * NumVColors) + right * (NumVColors) + top);
		}

		/// <summary>
		/// Given the 2 edges colors get the index the tile 
		/// should be in a 1D array where its colors will match its neighbour tiles.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		private int TileIndex1D(int x, int y)
		{
			int index;

			if (x < y)
				index = (2 * x + y * y);
			else if (x == y)
			{
				if (x > 0)
					index = ((x + 1) * (x + 1) - 2);
				else
					index = 0;
			}
			else if (y > 0)
				index = (x * x + 2 * y - 1);
			else
				index = ((x + 1) * (x + 1) - 1);

			return index;
		}

	}

}
