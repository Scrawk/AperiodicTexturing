
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
        /// <summary>
        /// The source images the tiles will be created from.
        /// </summary>
        private static Texture2D[] m_source = new Texture2D[4];

        /// <summary>
        /// 
        /// </summary>
        private static List<Texture2D[]> m_textures;

        /// <summary>
        /// The number of horizontal colors used for the wang tiles.
        /// </summary>
        private static int m_numHColors = 2;

        /// <summary>
        /// The number of vertical colors used for the wang tiles.
        /// </summary>
        private static int m_numVColors = 2;

        /// <summary>
        /// The size of the tiles created.
        /// </summary>
        private static int m_tileSize = 256;

        /// <summary>
        /// The size of the exemplats used when filling patches.
        /// </summary>
        private static int m_exemplarSize = 32;

        /// <summary>
        /// The maximum number of exemplars to use.
        /// </summary>
        private static int m_maxExemplars = 1000;

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
        /// The seed used for the random generator.
        /// </summary>
        private static int m_seed = 0;

        /// <summary>
        /// Folder the results will be output to.
        /// The default folder contains a preset that will 
        /// automatically change the textures settings to what is reconmmended.
        /// </summary>
        private static string m_folderName = "Textures Results";

        /// <summary>
        /// The files names of the output tiles
        /// </summary>
        private static string m_tileFileName = "AperiodicTile";

        private bool m_isRunning;

        private List<Tile> m_tiles;

        private List<ColorImage2D> m_images;

        private WangTileSet m_tileSet;

        //private ExemplarSet m_patchSet;

        private Exception m_exception;

        private ThreadingToken m_token;

        private string m_message;

        /// <summary>
        /// The number of tiles needed.
        /// </summary>
        private int NumTileables => Math.Max(m_numHColors, m_numVColors);

        /// <summary>
        /// 
        /// </summary>
        [MenuItem("Window/Aperiodic Texturing/Create Aperiodic Tiles")]
        public static void ShowWindow()
        {
            var window = EditorWindow.GetWindow(typeof(CreateAperiodicTilesWidow));
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

            DrawTextureLayout("Source textures. Albedo then 3 optional textures.", m_source);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            CreateTextures();
            for (int i = 0; i < m_textures.Count; i++)
                DrawTextureLayout($"Tileable {i} textures. Albedo then 3 optional textures.", m_textures[i]);
                
            EditorGUILayout.Space();

            if (GUILayout.Button(GetRunButtonText()))
            {
                if (Validate())
                {

                    m_images = AperiodicTilesEditorUtility.CreateImages(m_source);
                    m_tiles = AperiodicTilesEditorUtility.CreateTiles(m_textures);

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
                m_token.TimePeriodFormat = TIME_PERIOD.MINUTES;

                //Find the amount of progress that has happened from the token.
                float progress = m_token.PercentageProgress();
                string estimatedTime = "";

                //If the algorithm has progressed at least 10% then
                //dispay this as the progress, if not just show thats its calculating.
                //if (progress > 0.1f)
                    estimatedTime = m_token.EstimatedCompletionTime().ToString("F2") + m_token.TimePeriodUnit;
                //else
                //    estimatedTime = "(Calculating...)";

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

            if(m_token != null)
                m_token.Cancelled = true;
        }

        /// <summary>
        /// Draw the title and description.
        /// </summary>
        private void DrawTitle()
        {
            var wrapStyle = new GUIStyle(GUI.skin.GetStyle("label"));
            wrapStyle.wordWrap = true;

            titleContent.text = "Aperiodic tile creator";
            EditorGUILayout.LabelField("Create the aperoidic tile textures.", wrapStyle);
        }

        /// <summary>
        /// Draw the main block of properties.
        /// </summary>
        private void DrawMain()
        {
            m_numHColors = Mathf.Clamp(EditorGUILayout.IntField("Number of horizonal colors", m_numHColors), 2, 4);
            m_numVColors = Mathf.Clamp(EditorGUILayout.IntField("Number of vertical colors", m_numVColors), 2, 4);
            m_tileSize = Mathf.Max(EditorGUILayout.IntField("Tile Size", m_tileSize), 128);
            m_exemplarSize = Mathf.Clamp(EditorGUILayout.IntField("Exemplar size", m_exemplarSize), 16, 32);
            m_maxExemplars = Mathf.Max(EditorGUILayout.IntField("Max exemplars", m_maxExemplars), 10);
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
            m_tileFileName = EditorGUILayout.TextField("Tile file name", m_tileFileName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="label"></param>
        /// <param name="textures"></param>
        private void DrawTextureLayout(string label, IList<Texture2D> textures)
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

        /// <summary>
        /// 
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
        /// Create the texture array if null or not the same size as num tiles.
        /// </summary>
        private void CreateTextures()
        {
            if (m_textures == null || m_textures.Count != NumTileables)
            {
                m_textures = new List<Texture2D[]>();

                for (int i = 0; i < NumTileables; i++)
                    m_textures.Add(new Texture2D[4]);
            }
        }

        /// <summary>
        /// Resets before running.
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

        /// <summary>
        /// Check that the input is valid before running the algorithm.
        /// </summary>
        /// <returns></returns>
        private bool Validate()
        {
            int width = 0;
            int height = 0;

            for (int i = 0; i < NumTileables; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    var tex = m_textures[i][j];

                    //The first texture can not be null, all others are optional.
                    //Must have the same number of textures.

                    if (j == 0 && tex == null)
                    {
                        Debug.Log("The first tileable texture must not be null.");
                        return false;
                    }

                    //If this texture is null its optional so continue.
                    if (tex == null && j != 0) continue;

                    //Save what the texture size is.
                    //All other textures must be the same size.

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

                    //The texture can not be null and must be readable.

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

            //Check that the output foldere exists.

            string folderName = Application.dataPath + "/AperiodicalTexturing/" + m_folderName;

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
            //Run the task on a seperate thread as to not block main thread.
            await Task.Run(() =>
            {
                try
                {
                    m_token.StartTimer();

                    //Create the exemplar set that will be used to fill patchs in the tiles.
                    var patchSet = AperiodicTilesEditorUtility.CreateExemplarSetByCropping(m_images, m_sourceIsTileable, m_exemplarSize, m_varients);
                    patchSet.Shuffle(m_seed);
                    patchSet.Trim(m_maxExemplars);
                    //Debug.Log("Patch Set");
                    //Debug.Log(patchSet);

                    var exemplarSet = AperiodicTilesEditorUtility.CreateExemplarSetByRandomSampling(m_images, m_sourceIsTileable, m_tileSize, m_seed, m_varients);
                    //Debug.Log("Exemplar Set");
                    //Debug.Log(exemplarSet);

                    //Create the wang tile set that contains the tiles to patch
                    m_tileSet = new WangTileSet(m_numHColors, m_numVColors, m_tileSize);
                    //Debug.Log(m_tileSet);

                    //Create the wang tiles.
                    ImageSynthesis.CreateWangTileImage(m_tileSet, m_tiles, patchSet, exemplarSet, m_seed, m_token);

                    Debug.Log("Tile creation time: " + m_token.StopTimer() + "s");
                }
                catch(Exception e)
                {
                    //Catch any exeception so it can be passed back to main thread.
                    m_exception = e;
                }

                //Once finshed continue back onto main thread.

            }).ContinueWith((task) =>
            {
                //Once the tiles have been created then continue onto the main thread.
                //Check if the algorithm had a exception while running.
                //If not save the tiles.

                if (m_exception != null)
                {
                    Debug.Log("Failed to create textures due to a exception.");
                    Debug.Log(m_exception);
                }
                else
                {
                    AperiodicTilesEditorUtility.SaveTiles(m_tileSet, m_folderName, m_tileFileName);
                }

                m_isRunning = false;
                EditorUtility.ClearProgressBar();

            }, TaskScheduler.FromCurrentSynchronizationContext());

        }

    }

}
