using System;

using Common.Core.Numerics;
using Common.Core.Colors;
using ImageProcessing.Images;

public class WangTile
{

	public WangTile()
	{
		id = -1;
		left = -1;
		bottom = -1;
		right = -1;
		top = -1;
	}

	public WangTile(int id, int left, int bottom, int right, int top, int tileSize)
	{
		this.id = id;
		this.left = left;
		this.bottom = bottom;
		this.right = right;
		this.top = top;

		TileSize = tileSize;
		Image = new ColorImage2D(tileSize, tileSize);
	}

	public readonly int id, left, bottom, right, top;

	public readonly int TileSize;

	public ColorImage2D Image { get; private set; }

    public override string ToString()
    {
		return string.Format("[WangTile: id={0}, bottom={1}, top={2}]", 
			id, bottom, top);
    }

    public WangTile Copy()
    {
		var copy = new WangTile(id, left, bottom, right, top, TileSize);
		copy.Image = Image.Copy();
		
		return copy;
    }

}

public class WangTileSet
{
	
	public int NumHColors { get; private set; }

	public int NumVColors { get; private set; }

	public int NumHTiles => NumHColors * NumHColors;

	public int NumVTiles => NumVColors * NumVColors;

	public int NumTiles => Tiles.Length;

	public int TileSize { get; private set; }

	public WangTile[] Tiles { get; private set; }

	public WangTile[,] Tiles2 { get; private set; }

	private static ColorRGBA[] Colors = new ColorRGBA[]
	{
			new ColorRGBA(1,0,0,1), new ColorRGBA(0,1,0,1), new ColorRGBA(0,0,1,1),
			new ColorRGBA(1,1,0,1), new ColorRGBA(1,0,1,1), new ColorRGBA(0,1,1,1)
	};

	public WangTileSet(int numHColors, int numVColors, int tileSize)
	{
		NumHColors = numHColors;
		NumVColors = numVColors;
		TileSize = tileSize;

		int size = numHColors*numHColors*numVColors*numVColors;
		
		Tiles = new WangTile[size];

		for (int left = 0; left < NumHColors; left++)
		{
			for (int bottom = 0; bottom < NumVColors; bottom++)
			{
				for (int right = 0; right < NumHColors; right++)
				{
					for (int top = 0; top < NumVColors; top++)
					{
						int index = GetIndex(left, bottom, right, top);
						var tile = new WangTile(index, left, bottom, right, top, tileSize);

						AddEdgeColor(left, bottom, right, top, tile.Image);

						Tiles[index] = tile;
					}
				}
			}
		}

		Tiles2 = new WangTile[NumHTiles, NumVTiles];

	}
	
	public int GetIndex(int left, int bottom, int right, int top)
	{
		return (left*(NumVColors * NumHColors * NumVColors) + bottom*(NumHColors * NumVColors) + right*(NumVColors) + top);
	}

	public int GetIndex(WangTile tile)
	{
		return (tile.left * (NumVColors * NumHColors * NumVColors) + tile.bottom * (NumHColors * NumVColors) + tile.right * (NumVColors) + tile.top);
	}

	private void AddEdgeColor(int left, int bottom, int right, int top, ColorImage2D col)
	{
		int border = 4;
		int size = TileSize;
		ColorRGBA a = new ColorRGBA(1, 1, 1, 0.5);

		col.DrawBox(0, border, border, size - border, Colors[left] * a, true);
		col.DrawBox(border, 0, size - border, border, Colors[bottom] * a, true);
		col.DrawBox(size - border, border, size, size - border, Colors[right] * a, true);
		col.DrawBox(border, size - border, size - border, size, Colors[top] * a, true);
	}

	public WangTile[,] OrthogonalCompaction()
	{
		int width = NumHTiles;
		int height = NumVTiles;
        var result = new WangTile[width, height];

		var HEdges = TravelEdges(NumHColors);
		var VEdges = TravelEdges(NumVColors);

		for (int j = 0; j < height; j++)
		{
			for (int i = 0; i < width; i++)
			{
				int left = HEdges[i];
				int bottom = VEdges[j];
				int right = HEdges[i + 1];
				int top = VEdges[j + 1];

				int index = GetIndex(left, bottom, right, top);

				result[i, j] = Tiles[index].Copy();
			}
		}

		return result;
	}
	
	public WangTile[,] SequentialTiling(int numHTiles, int numVTiles, int seed)
	{
        var result = new WangTile[numHTiles, numVTiles];

		for (int i = 0; i < numHTiles; i++)
		{
			for (int j = 0; j < numVTiles; j++)
			{
				result[i, j] = new WangTile();
			}
		}

		var rnd = new System.Random(seed);

		for (int j = 0; j < numVTiles; j++)
		{
			for (int i = 0; i < numHTiles; i++)
			{
				int im1 = MathUtil.Wrap(i - 1, numHTiles);
				int ip1 = MathUtil.Wrap(i + 1, numHTiles);
				int jm1 = MathUtil.Wrap(j - 1, numVTiles);
				int jp1 = MathUtil.Wrap(j + 1, numVTiles);

				int left = result[im1, j].right;
				int bottom = result[i, jm1].top;
				int right = result[ip1, j].left;
				int top = result[i, jp1].bottom;

				if (left < 0) left = rnd.Next(0, NumHColors);
				if (bottom < 0) bottom = rnd.Next(0, NumVColors);
				if (right < 0) right = rnd.Next(0, NumHColors);
				if (top < 0) top = rnd.Next(0, NumVColors);

				int index = GetIndex(left, bottom, right, top);

				result[i, j] = Tiles[index].Copy();
			}
		}

		return result;
	}
	
	private static int[] TravelEdges(int numColors)
	{
        var result = new int[numColors * numColors + 1];
	    
	    for(int i = 0; i < numColors; i++)
		{
	        for(int j = 0; j < numColors; j++)
	        {
	            int index = EdgeOrdering(i, j);

	            result[index] = i;
	            result[index + 1] = j;
	        }
		}
	    
	    return result;
	}

	private static int EdgeOrdering(int x, int y)
	{
	    if(x < y)
	        return (2*x + y*y);
	    else if(x == y)
	    {
	        if(x > 0)
	            return ((x+1)*(x+1) - 2);
	        else
	            return 0;
	    }
	    else
	    {
	        if(y > 0)
	            return (x*x + 2*y - 1);
	        else
	            return ((x+1)*(x+1) - 1);
	    }
	}

	public static void TileLocation(WangTile[,] tiling, int id, ref int row, ref int col)
	{
	    for(int i = 0; i < tiling.GetLength(0); i++)
		{
	        for(int j = 0; j < tiling.GetLength(1); j++)
	        {
	            if(tiling[i,j].id == id)
	            {
	                row = i; col = j;
	            }
	        }
		}
	}

}
