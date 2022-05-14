
using System;
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

    /// <summary>
    /// A editoer script that takes a series of images and creates tileable textures.
    /// </summary>
    class CreateTileablesWidow : EditorWindow
    {

        /// <summary>
        /// The source images the tiles will be created from.
        /// </summary>
        private static Texture2D[] m_source;

        /// <summary>
        /// The number of tiles to be created.
        /// </summary>
        private static int m_numTiles = 1;

        /// <summary>
        /// THe tiles size.
        /// </summary>
        private static int m_tileSize = 256;

        /// <summary>
        /// The size of the exemplats used when filling patches.
        /// </summary>
        private static int m_exemplarSize = 32;

        /// <summary>
        /// The seed used for the random generator.
        /// </summary>
        private static int m_seed = 0;

        /// <summary>
        /// What variants should the exemplar set create.
        /// Will results in more variations being create
        /// that could result in better matches.
        /// </summary>
        private static EXEMPLAR_VARIANT m_varients;

        /// <summary>
        /// Should muti-threading be used when creating the tiles.
        /// </summary>
        private static bool m_useThreading = true;

        /// <summary>
        /// Is the source image provided tileable.
        /// This can makes sampling the texture easier if true.
        /// </summary>
        private static bool m_sourceIsTileable;

        /// <summary>
        /// Folder the results will be output to.
        /// The default folder contains a preset that will 
        /// automatically change the textures settings to what is reconmmended.
        /// </summary>
        private static string m_folderName = "Textures Results";

        /// <summary>
        /// The files names of the output tiles
        /// </summary>
        private static string[] m_fileNames;

        /// <summary>
        /// The tiles images created from source texture.
        /// </summary>
        private List<ColorImage2D> m_images;

        /// <summary>
        /// The tiles passed into the algorithm to make tileable.
        /// </summary>
        private List<Tile> m_tiles;

        /// <summary>
        /// The tiles created by the algorithm.
        /// </summary>
        private Tile[] m_tileables;

        private bool m_isRunning;

        private Exception m_exception;

        private ThreadingToken m_token;

        private string m_message;

        /// <summary>
        /// 
        /// </summary>
        [MenuItem("Window/Aperiodic Texturing/Create Tileable Images")]
        public static void ShowWindow()
        {
            var window = EditorWindow.GetWindow(typeof(CreateTileablesWidow));
            window.minSize = new Vector2(500, 700);
            window.maxSize = new Vector2(500, 700);
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnGUI()
        {
            DrawTitle();

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(m_isRunning);

            DrawMain();

            EditorGUILayout.Space();

            DrawSeed();

            EditorGUILayout.Space();

            DrawFileNames();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            DrawSourceTextures();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            if (GUILayout.Button(GetRunButtonText()))
            {
                if(Validate())
                {
                    m_images = AperiodicTilesEditorUtility.CreateImages(m_source);
           
                    ResetBeforeRunning();
                    Run();
                }
            }

            EditorGUI.EndDisabledGroup();

        }

        /// <summary>
        /// 
        /// </summary>
        private void Update()
        {
            // if running update the progress bar.

            if (m_isRunning && m_token != null)
            {
                //Find the amount of progress that has happened from the token.
                float progress = m_token.PercentageProgress();
                string estimatedTime = "";

                //If the algorithm has progressed at least 10% then
                //dispay this as the progress, if not just show thats its calculating.
                if (progress > 0.1f)
                    estimatedTime = m_token.EstimatedCompletionTime().ToString("F2") + m_token.TimePeriodUnit;
                else
                    estimatedTime = "(Calculating...)";

                //There will be messages from the token about what stage the agorithm is at.
                if (m_token.NumMessages > 0)
                    m_message = m_token.DequeueMessage();

                EditorUtility.DisplayProgressBar("Creating tiles", m_message + " Estimated completion time " + estimatedTime, progress);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnDestroy()
        {
            EditorUtility.ClearProgressBar();

            if (m_token != null)
                m_token.Cancelled = true;
        }

        /// <summary>
        /// Draw the title and description.
        /// </summary>
        private void DrawTitle()
        {
            var wrapStyle = new GUIStyle(GUI.skin.GetStyle("label"));
            wrapStyle.wordWrap = true;

            titleContent.text = "Tileable texture creator";
            EditorGUILayout.LabelField("Create a number of tileable textures from the source texture.", wrapStyle);
        }

        /// <summary>
        /// Draw the main block of properties.
        /// </summary>
        private void DrawMain()
        {
            m_numTiles = Mathf.Max(EditorGUILayout.IntField("Number of tiles", m_numTiles), 1);
            m_tileSize = Mathf.Max(EditorGUILayout.IntField("Tile Size", m_tileSize), 128);
            m_exemplarSize = Mathf.Clamp(EditorGUILayout.IntField("Exemplar Size", m_exemplarSize), 8, 64);
            m_varients = (EXEMPLAR_VARIANT)EditorGUILayout.EnumFlagsField("Varients", m_varients);
            m_useThreading = EditorGUILayout.Toggle("Use multi-threading", m_useThreading);
            m_sourceIsTileable = EditorGUILayout.Toggle("Source is tileable", m_sourceIsTileable);
        }

        /// <summary>
        /// Draw the seed and the generate button.
        /// </summary>
        private void DrawSeed()
        {
            m_seed = EditorGUILayout.IntField("Seed", m_seed);
            if (GUILayout.Button("Generate seed"))
                m_seed = GUID.Generate().GetHashCode();
        }

        /// <summary>
        /// Draw the folder location and the tile names.
        /// </summary>
        private void DrawFileNames()
        {
            m_folderName = EditorGUILayout.TextField("Output folder", m_folderName);

            if (m_fileNames == null)
            {
                m_fileNames = new string[]
                {
                    "Tileable",
                    "Tileable",
                    "Tileable",
                    "Tileable",
                };
            }

            for (int i = 0; i < 4; i++)
                m_fileNames[i] = EditorGUILayout.TextField("File name " + i, m_fileNames[i]);
        }

        /// <summary>
        /// Draw the sources images.
        /// </summary>
        private void DrawSourceTextures()
        {
            var wrapStyle = new GUIStyle(GUI.skin.GetStyle("label"));
            wrapStyle.wordWrap = true;

            EditorGUILayout.LabelField("Source textures. Albedo then 3 optional textures.", wrapStyle, GUILayout.Width(150));

            var texOptions = new GUILayoutOption[]
            {
                GUILayout.Width(64),
                GUILayout.Height(64)
            };

            if (m_source == null)
                m_source = new Texture2D[4];

            for (int i = 0; i < 4; i++)
                m_source[i] = (Texture2D)EditorGUILayout.ObjectField(m_source[i], typeof(Texture2D), false, texOptions);
        }

        /// <summary>
        /// Gets what the text on the run button should be.
        /// </summary>
        /// <returns></returns>
        private string GetRunButtonText()
        {
            if (!m_isRunning)
                return "Create";
            else
                return "Running";
        }

        /// <summary>
        /// Check that the input is valid before running the algorithm.
        /// </summary>
        /// <returns></returns>
        private bool Validate()
        {
            int width = 0;
            int height = 0;

            for(int i = 0; i < 4; i++)
            {
                //The first texture can not be null but all the others are optional.
                var tex = m_source[i];
                if (tex == null && i != 0) continue;

                //The texture can not be null.
                if (tex == null)
                {
                    Debug.Log($"Source texture {i} is null.");
                    return false;
                }

                //The texture must be readable
                if (!tex.isReadable)
                {
                    Debug.Log($"Source texture {i} is not readable.");
                    return false;
                }

                //If the is the first texture get its size.
                //If not the first texture then check thats its the same size as first.
                if (i == 0)
                {
                    width = tex.width;
                    height = tex.height;
                }
                else if (tex.width != width || tex.height != height)
                {
                    Debug.Log("Source textures must all be the same size .");
                    return false;
                }

            }

            //Check that the output foldere exists.

            string folderName = Application.dataPath + "/" + m_folderName;

            if (!System.IO.Directory.Exists(folderName))
            {
                Debug.Log("Output folder does not exist.");
                Debug.Log(folderName);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Run the algorithm on a seperate thread as to 
        /// not block the main application.
        /// </summary>
        private async void Run()
        {

            //Run the task on a seperate thread as to not block main thread.
            await Task.Run(() =>
            {
                try
                {
                    m_token.StartTimer();

                    //Create the tiles that are going to be made tileable from random samples of the exemplar set.
                    m_tiles = AperiodicTilesEditorUtility.CreateTilesByRandomSampling(m_images, m_sourceIsTileable, m_tileSize, m_numTiles, m_seed);

                    //Create the exemplar set used to patch the tiles.
                    var set = AperiodicTilesEditorUtility.CreateExemplarSetByCropping(m_images, m_sourceIsTileable, m_exemplarSize, m_varients);
                    Debug.Log(set);

                    //Make the tiles tileable.
                    m_tileables = ImageSynthesis.CreateTileableImages(m_tiles, set, m_seed, m_token);

                    Debug.Log("Tile creation time: " + m_token.StopTimer() + "s");
                }
                catch(Exception e)
                {
                    m_exception = e;
                }

                //Once finshed continue back onto main thread.

            }).ContinueWith((task) =>
            {
                //Once the tiles have been created then continue onto the main thread.
                //Check if the algorithm had a exception while running.
                //If not save the tiles.

                if(m_exception != null)
                {
                    Debug.Log("Failed to create textures due to a exception.");
                    Debug.Log(m_exception);
                }
                else
                {
                    AperiodicTilesEditorUtility.SaveTiles(m_tileables, m_folderName, m_fileNames);
                }

                m_isRunning = false;
                EditorUtility.ClearProgressBar();

            }, TaskScheduler.FromCurrentSynchronizationContext());

        }

        /// <summary>
        /// 
        /// </summary>
        private void ResetBeforeRunning()
        {
            m_isRunning = true;
            m_exception = null;
            m_message = "";
            m_token = new ThreadingToken();
            m_token.UseThreading = m_useThreading;
            m_token.TimePeriodFormat = TIME_PERIOD.SECONDS;
        }

    }

}
