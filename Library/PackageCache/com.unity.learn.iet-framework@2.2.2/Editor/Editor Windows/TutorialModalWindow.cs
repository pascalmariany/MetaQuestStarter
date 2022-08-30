using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

using static Unity.Tutorials.Core.Editor.RichTextParser;
using static Unity.Tutorials.Core.Editor.Localization;

namespace Unity.Tutorials.Core.Editor
{
    /// <summary>
    /// A modal/utility window that can display TutorialWelcomePage as its content.
    /// Optionally utilizes masking for modality.
    /// </summary>
    public class TutorialModalWindow : EditorWindow
    {
        const int k_Width = 700;
        const int k_Height = 500;

        // Never returns null
        static TutorialStyles Styles => TutorialProjectSettings.Instance.TutorialStyle;

        /// <summary>
        /// In order to set the welcome page, use the Show() function instead.
        /// </summary>
        public TutorialWelcomePage WelcomePage
        {
            get => m_WelcomePage;
            private set
            {
                if (m_WelcomePage)
                    m_WelcomePage.Modified.RemoveListener(OnWelcomePageModified);

                m_WelcomePage = value;

                if (m_WelcomePage)
                    m_WelcomePage.Modified.AddListener(OnWelcomePageModified);
            }
        }
        [SerializeField]
        TutorialWelcomePage m_WelcomePage;

        Action m_OnClose;

        /// <summary>
        /// Is the window currently visible.
        /// </summary>
        public static bool Visible { get; private set; }

        /// <summary>
        /// Does the window utilize masking for modality effect.
        /// </summary>
        /// <remarks>
        /// Remember to set prior to calling TryToShow().
        /// </remarks>
        public static bool MaskingEnabled { get; set; } = false;

        // Authoring toolbar is an IMGUIContainer and it will appear the first child element always in authoring mode.
        // so at least 2 elements are required in authoring mode for the window to have actual content.
        bool IsInitialized => rootVisualElement.childCount > (TutorialWindow.k_AuthoringMode ? 1 : 0);

        static bool s_IsBeingModified;
        //string m_PreviousWindowTitle;

        /// <summary>
        /// Shows the window using the provided content.
        /// </summary>
        /// <remarks>
        /// Shown as a utility window, https://docs.unity3d.com/ScriptReference/EditorWindow.ShowUtility.html
        /// </remarks>
        /// <param name="welcomePage">Content to be shown.</param>
        /// <param name="onClose">Optional callback to be called when the window is closed.</param>
        public static void Show(TutorialWelcomePage welcomePage, Action onClose = null)
        {
            Hide();
            var window = CreateInstance<TutorialModalWindow>();
            window.titleContent.text = welcomePage.WindowTitle;
            //window.m_PreviousWindowTitle = welcomePage.WindowTitle;
            window.minSize = window.maxSize = new Vector2(k_Width, k_Height);
            window.m_OnClose = onClose;
            window.WelcomePage = welcomePage;

            window.ShowUtility();
            // NOTE: positioning must be done after Show() in order to work.
            if (!s_IsBeingModified)
                EditorWindowUtils.CenterOnMainWindow(window);

            if (MaskingEnabled)
                window.Mask();
        }

        /// <summary>
        /// Closes the window if it's open
        /// </summary>
        public static void Hide()
        {
            var window = EditorWindowUtils.FindOpenInstance<TutorialModalWindow>();
            if (window)
                window.Close();
        }

        void Initialize()
        {
            var windowAsset = UIElementsUtils.LoadUIAsset<VisualTreeAsset>("WelcomeDialog.uxml");
            var mainContainer = windowAsset.CloneTree().Q("MainContainer");

            if (TutorialWindow.k_AuthoringMode)
                rootVisualElement.Add(new IMGUIContainer(OnGuiToolbar));
            rootVisualElement.Add(mainContainer);
        }

        void OnEnable()
        {
            if (!IsInitialized)
            {
                Initialize();
            }
            Styles.ApplyThemeStyleSheetTo(rootVisualElement);

            if (m_WelcomePage)
                m_WelcomePage.Modified.AddListener(OnWelcomePageModified);
        }

        void OnBecameVisible()
        {
            Visible = true;

            if (!IsInitialized)
                Initialize();

            UpdateContent();
            //Mask();
        }

        // For the teardown callbacks the order of execution is OnBecameInvisible, OnDisable, OnDestroy.
        // NOTE OnBecameInvisible appears to be called never if window was shown as utility window.
        // void OnBecameInvisible() {}

        void OnDisable()
        {
            if (m_WelcomePage)
                m_WelcomePage.Modified.RemoveListener(OnWelcomePageModified);
        }

        void OnDestroy()
        {
            Visible = false;
            s_IsBeingModified = false;
            m_OnClose?.Invoke();
            Unmask();
        }

        void UpdateContent()
        {
            if (!WelcomePage)
            {
                Debug.LogError("null WelcomePage.");
                return;
            }

            var header = rootVisualElement.Q("HeaderMedia");
            header.style.backgroundImage = Background.FromTexture2D(WelcomePage.Image);
            rootVisualElement.Q("HeaderContainer").style.display = WelcomePage.Image != null ? DisplayStyle.Flex : DisplayStyle.None;
            titleContent.text = WelcomePage.WindowTitle;
            rootVisualElement.Q<Label>("Heading").text = WelcomePage.Title;
            RichTextToVisualElements(WelcomePage.Description, rootVisualElement.Q("Description"));

            var buttonContainer = rootVisualElement.Q("ButtonContainer");
            buttonContainer.Clear();
            WelcomePage.Buttons
                .Where(buttonData => buttonData.Text.Value.IsNotNullOrEmpty())
                .Select(buttonData =>
                    new Button(() => buttonData.OnClick?.Invoke())
                    {
                        text = buttonData.Text,
                        tooltip = buttonData.Tooltip
                    })
                .ToList()
                .ForEach(button => buttonContainer.Add(button));
        }

        void OnGuiToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.ExpandWidth(true));

            GUILayout.FlexibleSpace();

            EditorGUI.BeginChangeCheck();
            MaskingEnabled = GUILayout.Toggle(
                MaskingEnabled, TutorialWindow.IconContent("Mask Icon", Tr("Preview Masking")),
                EditorStyles.toolbarButton, GUILayout.Width(TutorialWindow.k_AuthoringButtonWidth)
            );
            if (EditorGUI.EndChangeCheck())
            {
                if (MaskingEnabled)
                    Mask();
                else
                    Unmask();
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
        }

        void Mask()
        {
            var unmaskedViews = new UnmaskedView.MaskData();
            unmaskedViews.AddParentFullyUnmasked(this);
            var highlightedViews = new UnmaskedView.MaskData();

            MaskingManager.Mask(
                unmaskedViews,
                Styles.MaskingColor,
                highlightedViews,
                Styles.HighlightColor,
                Styles.BlockedInteractionColor,
                Styles.HighlightThickness
            );

            MaskingEnabled = true;
        }

        void Unmask()
        {
            MaskingManager.Unmask();
            MaskingEnabled = false;
        }

        void OnWelcomePageModified(TutorialWelcomePage sender)
        {
            s_IsBeingModified = true;

            // TODO try to find a way to author window title for utility window at real-time.
            //if (WelcomePage.WindowTitle != m_PreviousWindowTitle)
            //{
            //    // The way this window is shown currently requires us to recreate the window
            //    // in order to see a change in the title.
            //    Close();
            //    EditorApplication.delayCall += () => TryToShow(WelcomePage, m_OnClose);
            //}
            //else
            {
                UpdateContent();
            }
        }
    }
}
