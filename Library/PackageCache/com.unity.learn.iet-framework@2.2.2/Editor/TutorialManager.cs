using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Tutorials.Core.Editor
{
    /// <summary>
    /// Manages the startup and transitions of tutorials.
    /// </summary>
    public class TutorialManager : ScriptableObject
    {
        [Serializable]
        struct SceneViewState
        {
            public bool In2DMode;
            public bool Orthographic;
            public float Size;
            public Vector3 Point;
            public Quaternion Direction;
        }

        [Serializable]
        struct SceneInfo
        {
            public bool Active;
            public string AssetPath;
            public bool WasLoaded;
        }

        [SerializeField]
        SceneViewState m_OriginalSceneView;

        [SerializeField]
        List<SceneInfo> m_OriginalScenes = new List<SceneInfo>();
        // The original layout files are copied into this folder for modifications.
        const string k_UserLayoutDirectory = "Temp";
        // The original/previous layout is stored into this when loading new layouts.
        internal static readonly string k_OriginalLayoutPath = $"{k_UserLayoutDirectory}/OriginalLayout.dwlt";
        const string k_DefaultsFolder = "Tutorial Defaults";

        /// <summary>
        /// The singleton instance.
        /// </summary>
        public static TutorialManager Instance
        {
            get
            {
                if (s_TutorialManager == null)
                {
                    s_TutorialManager = Resources.FindObjectsOfTypeAll<TutorialManager>().FirstOrDefault();
                    if (s_TutorialManager == null)
                    {
                        s_TutorialManager = CreateInstance<TutorialManager>();
                        s_TutorialManager.hideFlags = HideFlags.HideAndDontSave;
                    }
                }

                return s_TutorialManager;
            }
        }
        static TutorialManager s_TutorialManager;

        /// <summary>
        /// The currently active tutorial, if any.
        /// </summary>
        public Tutorial ActiveTutorial { get => m_Tutorial; }
        Tutorial m_Tutorial;

        /// <summary>
        /// Are we currently (during this frame) transitioning from one tutorial to another.
        /// </summary>
        /// <remarks>
        /// This transition typically happens when using a Switch Tutorial button on a tutorial page.
        /// </remarks>
        public bool IsTransitioningBetweenTutorials { get; internal set; }

        /// <summary>
        /// Are we currently loading a window layout.
        /// </summary>
        /// <remarks>
        /// A window layout load typically happens when the project is started for the first time
        /// and the project's startup settings specify a window layout for the project, or when entering
        /// or exiting a tutorial with a window layout specified.
        /// </remarks>
        public static bool IsLoadingLayout { get; private set; }

        internal static event Action AboutToLoadLayout;
        internal static event Action<bool> LayoutLoaded; // bool == successful

        bool m_SkipSceneSaveDialog;

        internal static TutorialWindow GetTutorialWindow()
        {
            return EditorWindowUtils.FindOpenInstance<TutorialWindow>();
        }

        /// <summary>
        /// Starts a tutorial.
        /// </summary>
        /// <param name="tutorial">The tutorial to be started.</param>
        /// <remarks>
        /// The caller of the funtion is responsible for positioning the TutorialWindow for the tutorials.
        /// If no TutorialWindow is visible, it is created and shown as a free-floating window.
        /// If the currently active scene has unsaved changes, the user is asked to save them.
        /// If we are in Play Mode, it is exited.
        /// Note that currently there is no explicit way to quit a tutorial. Instead, a tutorial should be quit either
        /// by user interaction or by closing the TutorialWindow programmatically.
        /// </remarks>
        public void StartTutorial(Tutorial tutorial)
        {
            if (tutorial == null)
            {
                Debug.LogError("Null Tutorial.");
                return;
            }

            // Early-out if user decides to cancel. Otherwise the user can get reset to the
            // main tutorial selection screen in cases where the user was about to switch to
            // another tutorial while finishing up another (typical use case would be having a
            // "start next tutorial" button at the last page of a tutorial).
            m_SkipSceneSaveDialog = true;
            if (!EditorApplication.isPlaying && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (m_Tutorial)
            {
                // Is the previous tutorial finished? Make sure to record the progress
                // by trying to progress to the next page which will take care of it.
                if (m_Tutorial.IsCompleted)
                    m_Tutorial.TryGoToNextPage(); // TODO this might be unnecessary now, double-check

                m_Tutorial.RaiseQuit();
            }
            m_Tutorial = tutorial;

            // Ensure we are in edit mode
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.playModeStateChanged += PostponeStartTutorialToEditMode;
            }
            else
                StartTutorialInEditMode();
        }

        void PostponeStartTutorialToEditMode(PlayModeStateChange playModeStateChange)
        {
            if (playModeStateChange == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged -= PostponeStartTutorialToEditMode;
                StartTutorialInEditMode();
            }
        }

        void StartTutorialInEditMode()
        {
            Debug.Assert(!EditorApplication.isPlaying);
            if (!m_SkipSceneSaveDialog)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
            }

            // NOTE maximizeOnPlay=true was causing problems at some point
            // (tutorial was closed for some reason) but that problem seems to be gone.
            // Keeping this here in case the problem returns.
            //GameViewProxy.maximizeOnPlay = false;

            // Prevent Game view flashing briefly when starting tutorial.
            EditorWindow.GetWindow<SceneView>().Focus();

            if (!IsTransitioningBetweenTutorials)
            {
                SaveOriginalScenes();
                SaveOriginalWindowLayout();
                SaveSceneViewState();
            }

            // Make sure the active container persist through potential window layout load.
            var activeContainer = GetTutorialWindow()?.ActiveContainer;

            UserStartupCode.PrepareWindowLayouts();
            m_Tutorial.LoadWindowLayout();

            // Ensure TutorialWindow is open and set the current tutorial
            var tutorialWindow = TutorialWindow.GetOrCreateWindow();
            tutorialWindow.ActiveContainer = activeContainer;
            tutorialWindow.SetTutorial(m_Tutorial);

            // Do not overwrite workspace in authoring mode, use version control instead.
            if (!ProjectMode.IsAuthoringMode())
                LoadTutorialDefaultsIntoAssetsFolder();
        }

        internal void RestoreOriginalState()
        {
            EditorCoroutines.Editor.EditorCoroutineUtility.StartCoroutineOwnerless(RestoreOriginalScenes());
            // Restore layout only if the tutorial used window layout, meaning, the new auto-docking mechanism was not used.
            if (m_Tutorial?.WindowLayout)
                RestoreOriginalWindowLayout();
            RestoreSceneViewState();
        }

        internal void ResetTutorial()
        {
            m_Tutorial = GetTutorialWindow()?.currentTutorial;
            if (m_Tutorial == null)
                return; // tutorial has been quit

            // Ensure we are in edit mode
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.playModeStateChanged += PostponeResetTutorialToEditMode;
            }
            else
                ResetTutorialInEditMode();
        }

        void PostponeResetTutorialToEditMode(PlayModeStateChange playModeStateChange)
        {
            if (playModeStateChange == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged -= PostponeStartTutorialToEditMode;
                ResetTutorialInEditMode();
            }
        }

        void ResetTutorialInEditMode()
        {
            Debug.Assert(!EditorApplication.isPlaying);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            m_Tutorial.LoadWindowLayout();
            m_Tutorial.ResetProgress();

            // Do not overwrite workspace in authoring mode, use version control instead.
            if (!ProjectMode.IsAuthoringMode())
                LoadTutorialDefaultsIntoAssetsFolder();
        }

        internal static void SaveOriginalWindowLayout()
        {
            TutorialModalWindow.Hide();
            WindowLayoutProxy.SaveWindowLayout(k_OriginalLayoutPath);
        }

        internal static void RestoreOriginalWindowLayout()
        {
            if (File.Exists(k_OriginalLayoutPath))
            {
                LoadWindowLayout(k_OriginalLayoutPath);
                File.Delete(k_OriginalLayoutPath);
            }
        }

        void SaveSceneViewState()
        {
            var sv = EditorWindow.GetWindow<SceneView>();
            m_OriginalSceneView.In2DMode = sv.in2DMode;
            m_OriginalSceneView.Point = sv.pivot;
            m_OriginalSceneView.Direction = sv.rotation;
            m_OriginalSceneView.Size = sv.size;
            m_OriginalSceneView.Orthographic = sv.orthographic;
        }

        void RestoreSceneViewState()
        {
            var sv = EditorWindow.GetWindow<SceneView>();
            sv.in2DMode = m_OriginalSceneView.In2DMode;
            sv.LookAt(
                m_OriginalSceneView.Point,
                m_OriginalSceneView.Direction,
                m_OriginalSceneView.Size,
                m_OriginalSceneView.Orthographic,
                instant: true
            );
        }

        internal static bool LoadWindowLayout(string path)
        {
            IsLoadingLayout = true;
            AboutToLoadLayout?.Invoke();
            bool successful = EditorUtility.LoadWindowLayout(path); // will log an error if fails
            LayoutLoaded?.Invoke(successful);
            IsLoadingLayout = false;
            return successful;
        }

        internal static bool LoadWindowLayoutWorkingCopy(string path) =>
            LoadWindowLayout(GetWorkingCopyWindowLayoutPath(path));

        internal static string GetWorkingCopyWindowLayoutPath(string layoutPath) =>
            $"{k_UserLayoutDirectory}/{new FileInfo(layoutPath).Name}";

        // Makes a copy of the window layout file and replaces LastProjectPaths in the window layout
        // so that pre-saved Project window states work correctly. Also resets TutorialWindow's readme in the layout.
        // Returns path to the new layout file.
        internal static string PrepareWindowLayout(string layoutPath)
        {
            try
            {
                if (!Directory.Exists(k_UserLayoutDirectory))
                    Directory.CreateDirectory(k_UserLayoutDirectory);

                var destinationPath = GetWorkingCopyWindowLayoutPath(layoutPath);
                File.Copy(layoutPath, destinationPath, overwrite: true);

                const string lastProjectPathProp = "m_LastProjectPath: ";
                const string readmeProp = "m_Readme: ";
                const string nullObject = "{fileID: 0}";
                string userProjectPath = Directory.GetCurrentDirectory();

                var fileContents = new List<string>();
                using (var reader = new StreamReader(destinationPath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = ReplaceAfter(lastProjectPathProp, userProjectPath, line);
                        line = ReplaceAfter(readmeProp, nullObject, line);
                        fileContents.Add(line);
                    }
                }

                using (var writer = new StreamWriter(destinationPath, append: false))
                {
                    fileContents.ForEach(writer.WriteLine);
                }
                return destinationPath;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return string.Empty;
            }
        }

        /// <summary>
        /// Saves current state of open/loaded scenes so we can restore later
        /// </summary>
        void SaveOriginalScenes()
        {
            m_OriginalScenes = GetCurrentScenes()
                .Select(scene =>
                    new SceneInfo
                    {
                        Active = scene == SceneManager.GetActiveScene(),
                        AssetPath = scene.path,
                        WasLoaded = scene.isLoaded,
                    })
                .ToList();
        }

        static List<Scene> GetCurrentScenes()
        {
            var scenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                scenes.Add(SceneManager.GetSceneAt(i));
            }
            return scenes;
        }

        internal IEnumerator RestoreOriginalScenes()
        {
            if (!m_OriginalScenes.Any())
                yield break;

            if (EditorApplication.isPlaying)
            {
                // Exit play mode so we can open scenes (without necessarily loading them)
                EditorApplication.isPlaying = false;

                int currentFrameCount = Time.frameCount;
                while (currentFrameCount == Time.frameCount)
                {
                    yield return null; //going out of play mode requires a frame
                }
            }
            else
            {
                yield return null;
            }

            if (IsTransitioningBetweenTutorials)
            {
                IsTransitioningBetweenTutorials = false;
                yield break;
            }

            // Close all existing scenes
            // Closing all scenes allows us to retain the original order of scenes if the original scenes,
            // would they contain same scenes as the tutorial. As we cannot remove all scenes, and must have
            // at least one scene open at all times, create a dummy scene for the time being.
            var dummySceneMode = SceneManager.GetActiveScene().path.IsNullOrEmpty()
                ? NewSceneMode.Single
                : NewSceneMode.Additive; // prevents potential "Cannot create a new scene additively with an untitled scene unsaved" error
            var dummyScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, dummySceneMode);

            GetCurrentScenes()
                .Where(scene => scene != dummyScene)
                .ToList()
                .ForEach(scene => EditorSceneManager.CloseScene(scene, true));

            // Load original scenes
            foreach (var sceneInfo in m_OriginalScenes)
            {
                if (sceneInfo.AssetPath.IsNullOrEmpty())
                    continue; // Skip new unsaved scenes

                var openSceneMode = sceneInfo.WasLoaded ? OpenSceneMode.Additive : OpenSceneMode.AdditiveWithoutLoading;
                EditorSceneManager.OpenScene(sceneInfo.AssetPath, openSceneMode);
            }

            // Set original active scene
            var originalActiveScenePath = m_OriginalScenes
                .Where(sceneInfo => sceneInfo.Active)
                .Select(sceneInfo => sceneInfo.AssetPath)
                .FirstOrDefault();

            foreach (var scene in GetCurrentScenes())
            {
                if (scene.path == originalActiveScenePath)
                {
                    SceneManager.SetActiveScene(scene);
                    break;
                }
            }

            // Clean up the dummy scene if we have real scenes.
            if (SceneManager.sceneCount > 1)
                EditorSceneManager.CloseScene(dummyScene, true);

            m_OriginalScenes.Clear();
        }

        static void LoadTutorialDefaultsIntoAssetsFolder()
        {
            if (!TutorialProjectSettings.Instance.RestoreDefaultAssetsOnTutorialReload)
                return;

            AssetDatabase.SaveAssets();
            string defaultsPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, k_DefaultsFolder);
            var dirtyMetaFiles = new HashSet<string>();
            DirectoryCopy(defaultsPath, Application.dataPath, dirtyMetaFiles);
            AssetDatabase.Refresh();
            int startIndex = Application.dataPath.Length - "Assets".Length;
            foreach (var dirtyMetaFile in dirtyMetaFiles)
                AssetDatabase.ImportAsset(Path.ChangeExtension(dirtyMetaFile.Substring(startIndex), null));
        }

        internal static void WriteAssetsToTutorialDefaultsFolder()
        {
            if (!TutorialProjectSettings.Instance.RestoreDefaultAssetsOnTutorialReload)
                return;

            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Defaults cannot be written during play mode");
                return;
            }

            string defaultsPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, k_DefaultsFolder);
            DirectoryInfo defaultsDirectory = new DirectoryInfo(defaultsPath);
            if (defaultsDirectory.Exists)
            {
                foreach (var file in defaultsDirectory.GetFiles())
                    file.Delete();
                foreach (var directory in defaultsDirectory.GetDirectories())
                    directory.Delete(true);
            }
            DirectoryCopy(Application.dataPath, defaultsPath);
        }

        internal static void DirectoryCopy(string sourceDirectory, string destinationDirectory, HashSet<string> dirtyMetaFiles = default)
        {
            var sourceDir = new DirectoryInfo(sourceDirectory);
            if (!sourceDir.Exists)
                return;

            if (!Directory.Exists(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            foreach (var file in sourceDir.GetFiles())
            {
                string tempPath = Path.Combine(destinationDirectory, file.Name);
                if (dirtyMetaFiles != null && string.Equals(Path.GetExtension(tempPath), ".meta", StringComparison.OrdinalIgnoreCase))
                {
                    if (!File.Exists(tempPath) || !File.ReadAllBytes(tempPath).SequenceEqual(File.ReadAllBytes(file.FullName)))
                        dirtyMetaFiles.Add(tempPath);
                }
                file.CopyTo(tempPath, true);
            }

            foreach (var subdir in sourceDir.GetDirectories())
            {
                string tempPath = Path.Combine(destinationDirectory, subdir.Name);
                DirectoryCopy(subdir.FullName, tempPath, dirtyMetaFiles);
            }
        }

        static string ReplaceAfter(string before, string replaceWithThis, string lineToRead)
        {
            int index = -1;
            index = lineToRead.IndexOf(before, StringComparison.Ordinal);
            if (index > -1)
            {
                lineToRead = lineToRead.Substring(0, index + before.Length) + replaceWithThis;
            }
            return lineToRead;
        }
    }
}
