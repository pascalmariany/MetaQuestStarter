using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Events;

namespace Unity.Tutorials.Core.Editor
{
    /// <summary>
    /// A generic event for signaling changes in a tutorial container.
    /// Parameters: sender.
    /// </summary>
    [Serializable]
    public class TutorialContainerEvent : UnityEvent<TutorialContainer>
    {
    }

    /// <summary>
    /// A tutorial container is a collection of tutorial content, and is used to access the actual tutorials in the project.
    /// </summary>
    /// <remarks>
    /// A tutorial container can be two things:
    /// 1. Tutorial project (null Parent): a root container which is the entry point for tutorial content in the project.
    /// 2. Tutorial category (non-null Parent): a set of tutorials that are a part of some other container
    /// </remarks>
    public class TutorialContainer : ScriptableObject
    {
        /// <summary>
        /// Raised when any TutorialContainer is modified.
        /// </summary>
        /// <remarks>
        /// Raised before Modified event.
        /// </remarks>
        public static TutorialContainerEvent TutorialContainerModified = new TutorialContainerEvent();

        /// <summary>
        /// Raised when any field of this container is modified.
        /// </summary>
        /// <remarks>
        /// If 'this' container is parented, we consider modifications to 'this' container also to be modifications of the parent.
        /// </remarks>
        public TutorialContainerEvent Modified;

        /// <summary>
        /// By setting another container as a parent, this container becomes a tutorial category in the parent container.
        /// </summary>
        [Tooltip("By setting another container as a parent, this container becomes a tutorial category in the parent container.")]
        public TutorialContainer ParentContainer;

        /// <summary>
        /// This value determines the position of a container / container card within a container, if this container is shown as a card.
        /// </summary>
        [Tooltip("This value determines the position of a container / container card within a container, if this container is shown as a card.")]
        public int OrderInView;

        /// <summary>
        /// Background texture for the card/header.
        /// </summary>
        [FormerlySerializedAs("HeaderBackground")]
        public Texture2D BackgroundImage;

        /// <summary>
        /// Title shown in the card/header.
        /// </summary>
        [Tooltip("Title shown in the card/header.")]
        public LocalizableString Title;

        /// <summary>
        /// Subtitle shown in the container card and header area.
        /// </summary>
        [Tooltip("Subtitle shown in the card/header.")]
        public LocalizableString Subtitle;

        /// <summary>
        /// Used as the tooltip for the container card.
        /// </summary>
        [Tooltip("Used as the tooltip for the card.")]
        public LocalizableString Description;

        /// <summary>
        /// Can be used to override or disable (the default behavior) the default project layout specified by the Tutorial Framework.
        /// </summary>
        [Tooltip("Can be used to override or disable (the default behavior) the default project layout specified by the Tutorial Framework.")]
        public UnityEngine.Object ProjectLayout;

        /// <summary>
        /// Sections (tutorial or link card) of this container.
        /// </summary>
#if UNITY_2020_2_OR_NEWER
        [NonReorderable] // reordering freely would be problematic and is disallowed for now
#endif
        public Section[] Sections = {};

        /// <summary>
        /// Returns the path for the ProjectLayout, relative to the project folder,
        /// or a default tutorial layout path if ProjectLayout not specified.
        /// </summary>
        public string ProjectLayoutPath =>
            ProjectLayout != null ? AssetDatabase.GetAssetPath(ProjectLayout) : k_DefaultLayoutPath;

        // The default layout used when a project is started for the first time, if project layout is used.
        internal static readonly string k_DefaultLayoutPath =
            "Packages/com.unity.learn.iet-framework/Editor/DefaultAssets/DefaultLayout.wlt";

        /// <summary>
        /// A section/card for starting a tutorial or opening a web page.
        /// </summary>
        [Serializable]
        public class Section
        {
            /// <summary>
            /// This value determines the position of a section / section card within a container.
            /// </summary>
            [Tooltip("This value determines the position of a section / section card within a container.")]
            public int OrderInView;

            /// <summary>
            /// Title of the card.
            /// </summary>
            public LocalizableString Heading;

            /// <summary>
            /// Description of the card.
            /// </summary>
            public LocalizableString Text;

            /// <summary>
            /// Used as content type metadata for external references/URLs
            /// </summary>
            [Tooltip("Used as content type metadata for external references/URLs"), FormerlySerializedAs("LinkText")]
            public string Metadata;

            /// <summary>
            /// The URL of this section.
            /// Setting the URL will take precedence and make the card act as a link card instead of a tutorial card
            /// </summary>
            [Tooltip("Setting the URL will take precedence and make the card act as a link card instead of a tutorial card")]
            public string Url;

            /// <summary>
            /// Image for the card.
            /// </summary>
            public Texture2D Image;

            /// <summary>
            /// The tutorial this container contains
            /// </summary>
            public Tutorial Tutorial;

            /// <summary>
            /// Does this represent a tutorial?
            /// </summary>
            public bool IsTutorial => Url.IsNullOrEmpty();

            /// <summary>
            /// The ID of the represented tutorial, if any
            /// </summary>
            public string TutorialId => Tutorial?.LessonId.AsEmptyIfNull();

            /// <summary>
            /// Starts the tutorial of the section
            /// </summary>
            public void StartTutorial()
            {
                TutorialManager.Instance.StartTutorial(Tutorial);
            }

            /// <summary>
            /// Opens the URL Of the section, if any
            /// </summary>
            public void OpenUrl()
            {
                // TODO by making a static OpenUrl(string url) utility function we can easily track rich text hyperlink clicks also
                TutorialEditorUtils.OpenUrl(Url);
                AnalyticsHelper.SendExternalReferenceEvent(Url, Heading.Untranslated, Metadata, Tutorial?.LessonId);
            }

            // TODO Managing tutorials' completion states feels something that Tutorial and TutorialManager classes should be responsible of.

            /// <summary>
            /// Has the tutorial already been completed?
            /// </summary>
            internal bool TutorialCompleted { get; set; }

            internal string SessionStateKey => $"Unity.Tutorials.Core.Editor.lesson{TutorialId}";

            /// <summary>
            /// Loads the state of the section from SessionState.
            /// </summary>
            /// <returns>returns true if the state was found from EditorPrefs</returns>
            internal bool LoadState()
            {
                const string nonexisting = "NONEXISTING";
                var state = SessionState.GetString(SessionStateKey, nonexisting);
                if (state == "")
                {
                    TutorialCompleted = false;
                }
                else if (state == "Finished")
                {
                    TutorialCompleted = true;
                }
                return state != nonexisting;
            }

            /// <summary>
            /// Saves the state of the section from SessionState.
            /// </summary>
            internal void SaveState()
            {
                SessionState.SetString(SessionStateKey, TutorialCompleted ? "Finished" : "");
            }
        }

        void OnValidate()
        {
            Title = POFileUtils.SanitizeString(Title);
            Subtitle = POFileUtils.SanitizeString(Subtitle);
            Description = POFileUtils.SanitizeString(Description);
            foreach (var section in Sections)
            {
                section.Heading = POFileUtils.SanitizeString(section.Heading);
                section.Text = POFileUtils.SanitizeString(section.Text);
            }
            Array.Sort(Sections, (x, y) => x.OrderInView.CompareTo(y.OrderInView));
        }

        /// <summary>
        /// Loads the tutorial project layout
        /// </summary>
        public void LoadTutorialProjectLayout()
        {
            TutorialManager.LoadWindowLayoutWorkingCopy(ProjectLayoutPath);
        }

        /// <summary>
        /// Raises the Modified events for this asset.
        /// </summary>
        public void RaiseModified()
        {
            TutorialContainerModified?.Invoke(this);
            Modified?.Invoke(this);
        }
    }
}
