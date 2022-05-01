
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

using UnityEngine;
using UnityEditor;

using ImageProcessing.Images;

using TIMER = Common.Core.Time.Timer;

namespace AperiodicTexturing
{
    class CreateTileablesWidow : EditorWindow
    {

        private static Texture2D m_source;

        private static int m_numTiles = 2;

        private static int m_tileSize = 256;

        private static int m_seed = 0;

        private static EXEMPLAR_VARIANT m_varients;

        private static string m_folderName = "Textures Results";

        private static string m_fileName = "Tileable";

        private ColorImage2D[] m_tiles;

        private ColorImage2D m_image;

        private ExemplarSet m_set;

        private bool m_isRunning;

        private Exception m_exception;

        [MenuItem("Window/Aperiodic Texturing/Create Tileable Images")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(CreateTileablesWidow));
        }

        private void OnGUI()
        {
            titleContent.text = "Tileable texture creator";
            EditorGUILayout.LabelField("Create a number of tileable textures from the source texture.");

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(m_isRunning);

            m_numTiles = Mathf.Max(EditorGUILayout.IntField("Number of tiles", m_numTiles), 1);
            m_tileSize = Mathf.Max(EditorGUILayout.IntField("Tile Size", m_tileSize), 64);
            m_varients = (EXEMPLAR_VARIANT)EditorGUILayout.EnumFlagsField("Varients", m_varients);

            EditorGUILayout.Space();

            m_seed = EditorGUILayout.IntField("Seed", m_seed);
            if (GUILayout.Button("Generate seed"))
                m_seed = GUID.Generate().GetHashCode();

            EditorGUILayout.Space();

            m_folderName = EditorGUILayout.TextField("Output folder", m_folderName);
            m_fileName = EditorGUILayout.TextField("File name", m_fileName);

            EditorGUILayout.Space();

            m_source = (Texture2D)EditorGUILayout.ObjectField("Source", m_source, typeof(Texture2D), false);

            EditorGUILayout.Space();

            if (GUILayout.Button(GetRunButtonText()))
            {
                if(Validate())
                {
                    m_image = ToImage(m_source);

                    m_set = new ExemplarSet(m_image, m_tileSize);
                    m_set.CreateExemplarsFromRandom(m_numTiles, m_seed, 0.25f);
                    m_set.CreateVariants(m_varients);

                    if (m_set.Count < m_numTiles)
                    {
                        Debug.Log("Failed to find the required number of tiles in sources texture. Use a larger source texture or a smaller tile size.");
                        return;
                    }

                    m_tiles = new ColorImage2D[m_set.Count];
                    m_isRunning = true;
                    m_exception = null;

                    Run();
                }
            }

            EditorGUI.EndDisabledGroup();

        }

        private string GetRunButtonText()
        {
            if (!m_isRunning)
                return "Create";
            else
                return "Running (This could take awhile)";
        }

        private bool Validate()
        {
            if (m_source == null)
            {
                Debug.Log("Source texture is null.");
                return false;
            }

            if (!m_source.isReadable)
            {
                Debug.Log("Source texture is not readable.");
                return false;
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
                    var timer = new TIMER();
                    timer.Start();

                    ImageSynthesis.CreateTileableImages(m_tiles, m_set);

                    timer.Stop();
                    Debug.Log("Tile creation time: " + timer.ElapsedSeconds + "s");

                }
                catch(Exception e)
                {
                    m_exception = e;
                }

            }).ContinueWith((task) =>
            {
                if(m_exception != null)
                {
                    Debug.Log("Failed to create textures due to a exception.");
                    Debug.Log(m_exception);
                }
                else
                {
                    SaveTiles();
                }

                m_isRunning = false;

            }, TaskScheduler.FromCurrentSynchronizationContext());

        }

        private void SaveTiles()
        {
            string folderName = Application.dataPath + "/" + m_folderName;

            for (int i = 0; i < m_tiles.Length; i++)
            {
                var tile = m_tiles[i];
                if (tile == null) continue;

                var tex = ToTexture(tile);

                string fileName = folderName + "/" + m_fileName + i + ".png";

                System.IO.File.WriteAllBytes(fileName, tex.EncodeToPNG());

                Debug.Log("Saved texture " + fileName);
            }

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

        private Texture2D ToTexture(ColorImage2D image)
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
