using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.Tutorials.Core.Editor
{
    /// <summary>
    /// Runs IET project initialization logic.
    /// </summary>
    [InitializeOnLoad]
    public static class UserStartupCode
    {
        internal static void RunStartupCode(TutorialProjectSettings projectSettings)
        {
            if (projectSettings.InitialScene != null)
                EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(projectSettings.InitialScene));

            TutorialManager.WriteAssetsToTutorialDefaultsFolder();

            // Ensure Editor is in predictable state
            EditorPrefs.SetString("ComponentSearchString", string.Empty);
            Tools.current = Tool.Move;

            if (TutorialEditorUtils.FindAssets<TutorialContainer>().Any())
            {
                var existingWindow = EditorWindowUtils.FindOpenInstance<TutorialWindow>();
                if (existingWindow)
                    existingWindow.Close();
                ShowTutorialWindow();
            }

            // NOTE camera settings can be applied successfully only after potential layout changes
            if (projectSettings.InitialCameraSettings != null && projectSettings.InitialCameraSettings.Enabled)
                projectSettings.InitialCameraSettings.Apply();

            if (projectSettings.WelcomePage)
                TutorialModalWindow.Show(projectSettings.WelcomePage);
        }

        /// <summary>
        /// Shows Tutorials window using the currently specified behaviour.
        /// </summary>
        /// <remarks>
        /// Different behaviors:
        /// 1. If a single root tutorial container (TutorialContainer.ParentContainer is null) that has Project Layout specified exists,
        ///    the window is loaded and shown using the specified project window layout (old behaviour).
        ///    If the project layout does not contain Tutorials window, the window is shown an as a free-floating window.
        /// 2. If no root tutorial containers exist, or a root container's Project Layout is not specified, the window is shown
        ///     by anchoring and docking it next to the Inspector (new behaviour). If the Inspector is not available,
        ///     the window is shown an as a free-floating window.
        /// 3. If there is more than one root tutorial container with different Project Layout setting in the project,
        ///    one asset is chosen randomly to specify the behavior.
        /// 4. If Tutorials window is already created, it is simply brought to the foreground and focused.
        /// </remarks>
        /// <returns>The the created, or aleady existing, window instance.</returns>
        public static TutorialWindow ShowTutorialWindow()
        {
            var rootContainers = TutorialEditorUtils.FindAssets<TutorialContainer>()
                .Where(container => container.ParentContainer is null);
            var defaultContainer = rootContainers.FirstOrDefault();
            var projectLayout = defaultContainer?.ProjectLayout;
            if (rootContainers.Any(container => container.ProjectLayout != projectLayout))
            {
                Debug.LogWarningFormat(
                    "There is more than one TutorialContainers asset with different Project Layout setting in the project. " +
                    "Using asset at path {0} for the window behavior settings.",
                    AssetDatabase.GetAssetPath(defaultContainer)
                );
            }

            TutorialWindow window = null;
            if (!rootContainers.Any() || defaultContainer.ProjectLayout == null)
                window = TutorialWindow.GetOrCreateWindowNextToInspector();
            else if (defaultContainer.ProjectLayout != null)
                window = TutorialWindow.GetOrCreateWindowAndLoadLayout(defaultContainer);

            // If we have more than one root container, we show a selection view. Exactly one (or zero) container
            // is set active immediately without possibility to return to the the selection view.
            if (rootContainers.Count() > 1)
                window.SetContainers(rootContainers);
            else
                window.ActiveContainer = defaultContainer;

            return window;
        }

        internal static readonly string initFileMarkerPath = "InitCodeMarker";
        // Folder so that user can easily create this from the Editor's Project view.
        internal static readonly string dontRunInitCodeMarker = "Assets/DontRunInitCodeMarker";

        static UserStartupCode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || TutorialManager.IsLoadingLayout)
                return;

            // Language change triggers an assembly reload.
            if (LoadPreviousEditorLanguage() != LocalizationDatabaseProxy.currentEditorLanguage)
            {
                SaveCurrentEditorLanguage();
                // There are several smaller and bigger localization issues with if we don't restart
                // the Editor so let's query the user to do so.
                var title = Localization.Tr("Editor Language Change Detected");
                var msg = Localization.Tr("It's recommended to restart the Editor for the language change to be applied fully.");
                var ok = Localization.Tr("Restart");
                var cancel = Localization.Tr("Continue without restarting");
                if (EditorUtility.DisplayDialog(title, msg, ok, cancel))
                    RestartEditor();
            }

            EditorApplication.update += InitRunStartupCode;
        }

        static void InitRunStartupCode()
        {
            if (IsDontRunInitCodeMarkerSet())
                return;

            if (LocalizationDatabaseProxy.enableEditorLocalization && !IsLanguageInitialized())
            {
                // Need to Request a script reload in order overcome Editor Localization issues
                // with static initialization when opening the project for the first time.
                SetLanguageInitialized();
                EditorUtility.RequestScriptReload();
                return;
            }

            // Prepare the layout always. For example, the user might have moved the project around,
            // so we need to ensure the file paths in the layouts are correct.
            PrepareWindowLayouts();

            EditorApplication.update -= InitRunStartupCode;

            if (IsInitialized())
                return;

            SetInitialized();
            RunStartupCode(TutorialProjectSettings.Instance);
        }

        /// <summary>
        /// Has the IET project initialization been performed?
        /// </summary>
        /// <returns></returns>
        static bool IsInitialized() => File.Exists(initFileMarkerPath);

        static bool IsDontRunInitCodeMarkerSet() => Directory.Exists(dontRunInitCodeMarker);

        /// <summary>
        /// Marks the IET project initialization to be done.
        /// </summary>
        static void SetInitialized() => File.CreateText(initFileMarkerPath).Close();

        static bool IsLanguageInitialized() => SessionState.GetBool("EditorLanguageInitialized", false);

        static void SetLanguageInitialized() => SessionState.SetBool("EditorLanguageInitialized", true);

        // Replaces LastProjectPaths in window layouts used in tutorials so that e.g.
        // pre-saved Project window states work correctly.
        internal static void PrepareWindowLayouts()
        {
            AssetDatabase.FindAssets($"t:{typeof(TutorialContainer).FullName}")
                .Select(guid =>
                    AssetDatabase.LoadAssetAtPath<TutorialContainer>(AssetDatabase.GUIDToAssetPath(guid)).ProjectLayoutPath
                )
                .Concat(
                    AssetDatabase.FindAssets($"t:{typeof(Tutorial).FullName}")
                        .Select(guid =>
                            AssetDatabase.LoadAssetAtPath<Tutorial>(AssetDatabase.GUIDToAssetPath(guid)).WindowLayoutPath
                        )
                )
                .Where(StringExt.IsNotNullOrEmpty)
                .Distinct()
                .ToList()
                .ForEach(layoutPath => TutorialManager.PrepareWindowLayout(layoutPath));
        }

        static SystemLanguage LoadPreviousEditorLanguage() =>
            (SystemLanguage)EditorPrefs.GetInt("EditorLanguage", (int)SystemLanguage.English);

        static void SaveCurrentEditorLanguage() =>
            EditorPrefs.SetInt("EditorLanguage", (int)LocalizationDatabaseProxy.currentEditorLanguage);

        /// <summary>
        /// Restart the Editor.
        /// </summary>
        internal static void RestartEditor()
        {
            // In older versions, calling EditorApplication.OpenProject() while having unsaved modifications
            // can cause us to get stuck in a dialog loop. This seems to be fixed in 2020.1 (and newer?).
            // As a workaround, ask for saving before starting to restart the Editor for real. However,
            // we get the dialog twice and it can cause issues if user chooses first "Don't save" and then tries
            // to "Cancel" in the second dialog.
#if !UNITY_2020_1_OR_NEWER
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
#endif
            {
                EditorApplication.OpenProject(".");
            }
        }
    }
}
