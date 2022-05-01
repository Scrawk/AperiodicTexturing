
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
        private static Texture2D m_source;

        private static Texture2D[] m_tileables;

        private static int m_numHColors = 2;

        private static int m_numVColors = 2;

        private static int m_tileSize = 256;

        private static int m_samples = 100;

        private static EXEMPLAR_VARIANT m_varients;

        private static bool m_useThreading = true;

        private static int m_seed = 0;

        private static string m_folderName = "Textures Results";

        private static string m_tileFileName = "AperiodicTile";

        private bool m_isRunning;

        private ColorImage2D[] m_tileableImages;

        private ColorImage2D m_sourceImage;

        private WangTileSet m_tileSet;

        private ExemplarSet m_exemplarSet;

        private Exception m_exception;

        private ThreadingToken m_token;

        private int NumTileables => Math.Max(m_numHColors, m_numVColors);

        [MenuItem("Window/Aperiodic Texturing/Create Aperiodic Tiles")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(CreateAperiodicTilesWidow));
        }

        private void OnGUI()
        {
            titleContent.text = "Aperiodic tile creator";
            EditorGUILayout.LabelField("Create the aperoidic tile textures.");

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(m_isRunning);

            m_numHColors = Mathf.Clamp(EditorGUILayout.IntField("Number of horizonal colors", m_numHColors), 2, 4);
            m_numVColors = Mathf.Clamp(EditorGUILayout.IntField("Number of vertical colors", m_numVColors), 2, 4);
            m_tileSize = Mathf.Max(EditorGUILayout.IntField("Tile size", m_tileSize), 64);
            m_samples = Mathf.Max(EditorGUILayout.IntField("Samples", m_samples), 1);
            m_varients = (EXEMPLAR_VARIANT)EditorGUILayout.EnumFlagsField("Varients", m_varients);
            m_useThreading = EditorGUILayout.Toggle("Use multi-threading", m_useThreading);

            EditorGUILayout.Space();

            m_seed = EditorGUILayout.IntField("Seed", m_seed);
            if (GUILayout.Button("Generate seed"))
                m_seed = GUID.Generate().GetHashCode();

           EditorGUILayout.Space();

            m_folderName = EditorGUILayout.TextField("Output folder", m_folderName);
            m_tileFileName = EditorGUILayout.TextField("Tile file name", m_tileFileName);

            EditorGUILayout.Space();

            m_source = (Texture2D)EditorGUILayout.ObjectField("Source", m_source, typeof(Texture2D), false);

            EditorGUILayout.Space();

            if (m_tileables == null)
                m_tileables = new Texture2D[4];

            for(int i = 0; i < NumTileables; i++)
            {
                m_tileables[i] = (Texture2D)EditorGUILayout.ObjectField("Tileable"+i, m_tileables[i], typeof(Texture2D), false);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button(GetRunButtonText()))
            {
                if (Validate())
                {
                    m_sourceImage = ToImage(m_source);

                    m_exemplarSet = new ExemplarSet(m_sourceImage, m_tileSize);
                    m_exemplarSet.CreateExemplarsFromRandom(m_samples, m_seed, 0.25f);
                    m_exemplarSet.CreateVariants(m_varients);
                    Debug.Log(m_exemplarSet);

                    m_tileableImages = new ColorImage2D[NumTileables];
                    for (int i = 0; i < NumTileables; i++)
                        m_tileableImages[i] = ToImage(m_tileables[i]);

                    m_tileSet = new WangTileSet(m_numHColors, m_numVColors, m_tileSize);
                    Debug.Log(m_tileSet);

                    m_isRunning = true;
                    m_exception = null;
                    m_token = new ThreadingToken();
                    m_token.UseThreading = m_useThreading;
                    m_token.TimePeriodFormat = TIME_PERIOD.SECONDS;

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

                if (progress < 0.1f)
                    estimatedTime = m_token.EstimatedCompletionTime() + m_token.TimePeriodUnit;
                else
                    estimatedTime = "(Calculating...)";

                EditorUtility.DisplayProgressBar("Creating tiles", "Estimated completion time " + estimatedTime, progress);
            }
        }

        private void OnDestroy()
        {
            EditorUtility.ClearProgressBar();

            if(m_token != null)
                m_token.Cancelled = true;
        }

        private string GetRunButtonText()
        {
            if (!m_isRunning)
                return "Create";
            else
                return "Running";
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

                if (m_tileables[i].width != m_tileSize || m_tileables[i].height != m_tileSize)
                {
                    Debug.Log("Tileable" + i + " texture must have the same dimensions as the tile size.");
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
                    m_token.StartTimer();
     
                    ImageSynthesis.CreateWangTileImage(m_tileSet, m_tileableImages, m_exemplarSet, m_token);

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
            string fileName = folderName + "/" + m_tileFileName + hv + ".png";

            System.IO.File.WriteAllBytes(fileName, tex.EncodeToPNG());

            Debug.Log("Saved texture " + fileName);

            AssetDatabase.Refresh();
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
