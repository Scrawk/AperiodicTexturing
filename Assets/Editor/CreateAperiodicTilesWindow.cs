
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
    class CreateAperiodicTilesWidow : EditorWindow
    {
        private static Texture2D[] m_source = new Texture2D[4];

        private static List<Texture2D[]> m_textures;

        private static int m_numHColors = 2;

        private static int m_numVColors = 2;

        private static int m_tileSize = 256;

        private static int m_blendArea = 16;

        private static int m_samples = 100;

        private static EXEMPLAR_VARIANT m_varients;

        private static bool m_useThreading = true;

        private static bool m_sourceIsTileable;

        private static int m_seed = 0;

        private static string m_folderName = "Textures Results";

        private static string m_tileFileName = "AperiodicTile";

        private bool m_isRunning;

        private List<Tile> m_tiles;

        private WangTileSet m_tileSet;

        private ExemplarSet m_exemplarSet;

        private Exception m_exception;

        private ThreadingToken m_token;

        private string m_message;

        private int NumTileables => Math.Max(m_numHColors, m_numVColors);

        [MenuItem("Window/Aperiodic Texturing/Create Aperiodic Tiles")]
        public static void ShowWindow()
        {
            var window = EditorWindow.GetWindow(typeof(CreateAperiodicTilesWidow));
            window.minSize = new Vector2(500, 700);
            window.maxSize = new Vector2(500, 700);
        }

        private void OnGUI()
        {
            titleContent.text = "Aperiodic tile creator";
            EditorGUILayout.LabelField("Create the aperoidic tile textures.");

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(m_isRunning);

            m_numHColors = Mathf.Clamp(EditorGUILayout.IntField("Number of horizonal colors", m_numHColors), 2, 4);
            m_numVColors = Mathf.Clamp(EditorGUILayout.IntField("Number of vertical colors", m_numVColors), 2, 4);
            m_tileSize = Mathf.Max(EditorGUILayout.IntField("Tile Size", m_tileSize), 128);
            m_blendArea = Mathf.Clamp(EditorGUILayout.IntField("Blend area", m_blendArea), 8, 32);
            m_samples = Mathf.Max(EditorGUILayout.IntField("Samples", m_samples), 1);
            m_varients = (EXEMPLAR_VARIANT)EditorGUILayout.EnumFlagsField("Varients", m_varients);
            m_useThreading = EditorGUILayout.Toggle("Use multi-threading", m_useThreading);
            m_sourceIsTileable = EditorGUILayout.Toggle("Source is tileable", m_sourceIsTileable);

            EditorGUILayout.Space();

            m_seed = EditorGUILayout.IntField("Seed", m_seed);
            if (GUILayout.Button("Generate seed"))
                m_seed = GUID.Generate().GetHashCode();

           EditorGUILayout.Space();

            m_folderName = EditorGUILayout.TextField("Output folder", m_folderName);
            m_tileFileName = EditorGUILayout.TextField("Tile file name", m_tileFileName);

            EditorGUILayout.Space();

            TextureLayout("Source textures. Albedo then 3 optional textures.", m_source);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            CreateTextures();
            for (int i = 0; i < m_textures.Count; i++)
                TextureLayout($"Tileable {i} textures. Albedo then 3 optional textures.", m_textures[i]);
                
            EditorGUILayout.Space();

            if (GUILayout.Button(GetRunButtonText()))
            {
                if (Validate())
                {

                    m_exemplarSet = new ExemplarSet(GetSourceImages(), m_sourceIsTileable, m_tileSize);
                    m_exemplarSet.CreateExemplarsFromRandom(m_samples, m_seed, 0.25f);
                    m_exemplarSet.CreateVariants(m_varients);
                    Debug.Log(m_exemplarSet);

                    m_tileSet = new WangTileSet(m_numHColors, m_numVColors, m_tileSize);
                    Debug.Log(m_tileSet);

                    m_isRunning = true;
                    m_exception = null;
                    m_message = "";
                    m_token = new ThreadingToken();
                    m_token.UseThreading = m_useThreading;
                    m_token.TimePeriodFormat = TIME_PERIOD.SECONDS;

                    CreateTiles();
                    Run();
                }
            }

            EditorGUI.EndDisabledGroup();
        }

        private void Update()
        {
            if(m_isRunning && m_token != null)
            {
                float  progress = m_token.PercentageProgress();
                string estimatedTime = "";

                if (progress > 0.1f)
                    estimatedTime = m_token.EstimatedCompletionTime().ToString("F2") + m_token.TimePeriodUnit;
                else
                    estimatedTime = "(Calculating...)";

                if (m_token.NumMessages > 0)
                    m_message = m_token.DequeueMessage();

                EditorUtility.DisplayProgressBar("Creating tiles", m_message + " Estimated completion time " + estimatedTime, progress);
            }
        }

        private void OnDestroy()
        {
            EditorUtility.ClearProgressBar();

            if(m_token != null)
                m_token.Cancelled = true;
        }

        private void TextureLayout(string label, IList<Texture2D> textures)
        {
            var style = new GUIStyle(GUI.skin.GetStyle("label"));
            style.wordWrap = true;
            style.fixedHeight = 64;

            var options = new GUILayoutOption[]
            {
                GUILayout.Width(64),
                GUILayout.Height(64)
            };

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(label, style, GUILayout.Width(150));

            for (int i = 0; i < textures.Count; i++)
                textures[i] = (Texture2D)EditorGUILayout.ObjectField(textures[i], typeof(Texture2D), false, options);

            EditorGUILayout.EndHorizontal();
        }

        private string GetRunButtonText()
        {
            if (!m_isRunning)
                return "Create";
            else
                return "Running";
        }

        private void CreateTextures()
        {
            if (m_textures == null || m_textures.Count != NumTileables)
            {
                m_textures = new List<Texture2D[]>();

                for (int i = 0; i < NumTileables; i++)
                    m_textures.Add(new Texture2D[4]);
            }
        }

        private void CreateTiles()
        {
            m_tiles = new List<Tile>();

            foreach(var texArray in m_textures)
            {
                var images = new List<ColorImage2D>();

                foreach (var tex in texArray)
                {
                    if (tex == null) continue;
                    images.Add(ToImage(tex));
                }

                var tile = new Tile(images);
                m_tiles.Add(tile);
            }
        }

        private bool Validate()
        {
            int width = 0;
            int height = 0;
            bool[] hasTile = new bool[4];

            for (int i = 0; i < NumTileables; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    var tex = m_textures[i][j];

                    if (i == 0)
                        hasTile[j] = true;
                    else if ((tex == null && hasTile[j]) || tex != null && !hasTile[j])
                    {
                        Debug.Log("Tileable textures must all the same number of tiles.");
                        return false;
                    }

                    if (tex == null && j != 0) continue;

                    if (j == 0)
                    {
                        width = tex.width;
                        height = tex.height;
                    }
                    else if (tex.width != width || tex.height != height)
                    {
                        Debug.Log("Tileable textures must all be the same size.");
                        return false;
                    }

                    if (tex == null)
                    {
                        Debug.Log($"Tileable texture {j} is null.");
                        return false;
                    }
                    if (!tex.isReadable)
                    {
                        Debug.Log($"Tileable texture {j} is not readable.");
                        return false;
                    }

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
                    m_token.StartTimer();
     
                    ImageSynthesis.CreateWangTileImage(m_tileSet, m_tiles, m_exemplarSet, m_blendArea, m_token);

                    Debug.Log("Tile creation time: " + m_token.StopTimer() + "s");
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
                    SaveTiles();
                }

                m_isRunning = false;
                EditorUtility.ClearProgressBar();

            }, TaskScheduler.FromCurrentSynchronizationContext());

        }

        private void SaveTiles()
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

                            pixels[xi + yj * width] = tile.Tile.Image[i, j].ToColor();
                        }
                    }
                }
            }

            var tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            tex.SetPixels(pixels);
            tex.Apply();

            string folderName = Application.dataPath + "/" + m_folderName;
            string hv = m_numHColors + "x" + m_numVColors;
            string fileName = folderName + "/" + m_tileFileName + hv + ".png";

            System.IO.File.WriteAllBytes(fileName, tex.EncodeToPNG());

            Debug.Log("Saved texture " + fileName);

            AssetDatabase.Refresh();
        }

        private List<ColorImage2D> GetSourceImages()
        {
            var images = new List<ColorImage2D>();
            for (int i = 0; i < m_source.Length; i++)
            {
                if (m_source[i] == null) continue;
                images.Add(ToImage(m_source[i]));
            }

            return images;
        }

        private ColorImage2D ToImage(Texture2D tex)
        {
            var image = new ColorImage2D(tex.width, tex.height);

            image.Fill((x, y) =>
            {
                return tex.GetPixel(x, y).ToColorRGBA();
            });

            return image;
        }

    }

}
