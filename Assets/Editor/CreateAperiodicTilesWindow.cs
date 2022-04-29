
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

using UnityEngine;
using UnityEditor;

using Common.Core.Colors;
using ImageProcessing.Images;

namespace AperiodicTexturing
{
    class CreateAperiodicTilesWidow : EditorWindow
    {
        private static Texture2D m_source;

        private static Texture2D[] m_tileables;

        private static int m_numHColors = 2;

        private static int m_numVColors = 2;

        private static int m_mappingWidth = 8;

        private static int m_mappingHeight = 8;

        private static int m_tileSize = 128;

        private static int m_seed = 0;

        private static bool m_addEdgeColors;

        private static string m_folderName = "Textures Results";

        private static string m_tileFileName = "AperiodicTile";

        private static string m_mappingFileName = "AperiodicMapping";

        private bool m_isRunning;

        private ColorImage2D[] m_tileableImages;

        private ColorImage2D m_sourceImage;

        private WangTile[,] m_mapping;

        private WangTileSet m_tileSet;

        private ExemplarSet m_exemplarSet;

        private Exception m_exception;

        private int NumTileables => Math.Max(m_numHColors, m_numVColors);

        [MenuItem("Window/Aperiodic Texturing/Create Aperiodic Tiles")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(CreateAperiodicTilesWidow));
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Create the aperoidic tile and mapping texture.");

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(m_isRunning);

            m_numHColors = Mathf.Clamp(EditorGUILayout.IntField("Number of horizonal colors", m_numHColors), 2, 4);
            m_numVColors = Mathf.Clamp(EditorGUILayout.IntField("Number of vertical colors", m_numVColors), 2, 4);
            m_mappingWidth = Mathf.Max(EditorGUILayout.IntField("Mapping texture width", m_mappingWidth), 2);
            m_mappingHeight = Mathf.Max(EditorGUILayout.IntField("Mapping texture height", m_mappingHeight), 2);
            m_tileSize = Mathf.Max(EditorGUILayout.IntField("Tile size", m_tileSize), 64);
            m_addEdgeColors = EditorGUILayout.Toggle("Create edge color tile", m_addEdgeColors);

            EditorGUILayout.Space();

            m_seed = EditorGUILayout.IntField("Seed", m_seed);
            if (GUILayout.Button("Generate seed"))
                m_seed = GUID.Generate().GetHashCode();

           EditorGUILayout.Space();

            m_folderName = EditorGUILayout.TextField("Output folder", m_folderName);
            m_tileFileName = EditorGUILayout.TextField("Tile file name", m_tileFileName);
            m_mappingFileName = EditorGUILayout.TextField("Mapping file name", m_mappingFileName);

            EditorGUILayout.Space();

            m_source = (Texture2D)EditorGUILayout.ObjectField("Source", m_source, typeof(Texture2D), false);

            if (m_tileables == null)
                m_tileables = new Texture2D[4];

            m_tileables[0] = (Texture2D)EditorGUILayout.ObjectField("Tileable0", m_tileables[0], typeof(Texture2D), false);
            m_tileables[1] = (Texture2D)EditorGUILayout.ObjectField("Tileable1", m_tileables[1], typeof(Texture2D), false);
            m_tileables[2] = (Texture2D)EditorGUILayout.ObjectField("Tileable2", m_tileables[2], typeof(Texture2D), false);
            m_tileables[3] = (Texture2D)EditorGUILayout.ObjectField("Tileable3", m_tileables[3], typeof(Texture2D), false);

            EditorGUILayout.Space();

            if (GUILayout.Button(GetButtonText()))
            {
                if (Validate())
                {
                    m_sourceImage = ToImage(m_source);

                    m_exemplarSet = new ExemplarSet(m_sourceImage, m_tileSize);
                    m_exemplarSet.CreateExemplarsFromRandom(m_seed, 32, 0.5f);

                    m_tileableImages = new ColorImage2D[NumTileables];
                    for (int i = 0; i < NumTileables; i++)
                        m_tileableImages[i] = ToImage(m_tileables[i]);

                    m_tileSet = new WangTileSet(m_numHColors, m_numVColors, m_tileSize);

                    m_isRunning = true;
                    m_exception = null;

                    Run();
                }
            }

            EditorGUI.EndDisabledGroup();

        }

        private string GetButtonText()
        {
            if (!m_isRunning)
                return "Create";
            else
                return "Running (This could take awhile)";
        }

        private bool Validate()
        {
            for (int i = 0; i < NumTileables; i++)
            {
                if (m_tileables[i] == null)
                {
                    Debug.Log("Tileable" + i + " texture is null.");
                    return false;
                }

                if (!m_tileables[i].isReadable)
                {
                    Debug.Log("Tileable" + i + " texture is not readable.");
                    return false;
                }
            }

            string folderName = Application.dataPath + "/" + m_folderName;

            if (!System.IO.Directory.Exists(folderName))
            {
                Debug.Log("Output folder does not exist.");
                Debug.Log(folderName);
                return false;
            }

            return true;
        }

        private async void Run()
        {

            await Task.Run(() =>
            {
                try
                {

                    foreach (var tile in m_tileSet.Tiles)
                    {
                        ImageSynthesis.CreateWangTileImage(tile, m_tileableImages, m_exemplarSet);
                    }

                    m_mapping = m_tileSet.CreateTileMap(m_mappingHeight, m_mappingWidth, m_seed);
                }
                catch(Exception e)
                {
                    m_exception = e;
                }

            }).ContinueWith((task) =>
            {
                if (m_exception != null)
                {
                    Debug.Log("Failed to create textures due to a exception.");
                    Debug.Log(m_exception);
                }
                else
                {
                    SaveTiles(false);

                    if (m_addEdgeColors)
                    {
                        foreach (var tile in m_tileSet.Tiles)
                            tile.AddEdgeColor(4, 0.5f);

                        SaveTiles(true);
                    }

                    SaveMapping();
                    m_isRunning = false;
                }

            }, TaskScheduler.FromCurrentSynchronizationContext());

        }

        private void SaveTiles(bool hasEdgeColors)
        {
            int tileTextureWidth = m_tileSet.NumHTiles;
            int tileTextureHeight = m_tileSet.NumVTiles;

            int width = m_tileSize * tileTextureWidth;
            int height = m_tileSize * tileTextureHeight;

            Color[] pixels = new Color[width * height];

            for (int x = 0; x < tileTextureWidth; x++)
            {
                for (int y = 0; y < tileTextureHeight; y++)
                {
                    var tile = m_tileSet.Tiles[x, y];

                    for (int i = 0; i < m_tileSize; i++)
                    {
                        for (int j = 0; j < m_tileSize; j++)
                        {
                            int xi = x * m_tileSize + i;
                            int yj = y * m_tileSize + j;

                            pixels[xi + yj * width] = tile.Image[i, j].ToColor();
                        }
                    }
                }
            }

            var tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            tex.SetPixels(pixels);
            tex.Apply();

            string folderName = Application.dataPath + "/" + m_folderName;
            string hv = m_numHColors + "x" + m_numVColors;
            string colors = hasEdgeColors ? "_Colors" : "";
            string fileName = folderName + "/" + m_tileFileName + hv + colors  +".png";

            System.IO.File.WriteAllBytes(fileName, tex.EncodeToPNG());

            Debug.Log("Saved texture " + fileName);

            AssetDatabase.Refresh();
        }

        private void SaveMapping()
        {
            int width = m_mapping.GetLength(0);
            int height = m_mapping.GetLength(1);

            Color32[] pixels = new Color32[width * height];

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    var index = m_mapping[i, j].Index;
                    pixels[i + j * width] = new Color32((byte)index.x, (byte)index.y, 0, 0);
                }
            }

            var tex = new Texture2D(width, height, TextureFormat.RGB24, false, true);
            tex.SetPixels32(pixels);
            tex.Apply();

            string folderName = Application.dataPath + "/" + m_folderName;
            string hv = m_numHColors + "x" + m_numVColors;
            string fileName = folderName + "/" + m_mappingFileName + hv + ".png";

            System.IO.File.WriteAllBytes(fileName, tex.EncodeToPNG());

            Debug.Log("Saved texture " + fileName);

        }

        private ColorImage2D ToImage(Texture2D tex)
        {
            var image = new ColorImage2D(tex.width, tex.height);

            image.Fill((x, y) =>
            {
                return tex.GetPixel(x, y).ToColorRGB();
            });

            return image;
        }

    }

}
