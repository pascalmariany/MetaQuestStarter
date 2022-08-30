using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Tutorials.Core.Editor
{
    using UnityObject = UnityEngine.Object;

    static class UIElementsUtils
    {
        internal const string k_UIAssetPath = "Packages/com.unity.learn.iet-framework/Editor/UI";

        /// <summary>
        /// Sets up a button.
        /// </summary>
        /// <param name="onClick">The method that will be called when the button is clicked.</param>
        /// <param name="text">The text for the butto, if any.</param>
        /// <param name="tooltip">The tooltip for the button, if any.</param>
        public static void SetupButton(Button button, Action onClick, string text = null, string tooltip = null)
        {
            button.clickable = new Clickable(() => onClick.Invoke());
            if (text != null)
                button.text = text;
            if (tooltip != null)
                button.tooltip = tooltip;
        }

        /// <summary>
        /// Hides a visual element.
        /// </summary>
        /// <param name="element">The element to hide</param>
        public static void Hide(VisualElement element) { SetVisible(element, false); }

        /// <summary>
        /// Shows a visual element.
        /// </summary>
        /// <param name="element">The element to show</param>
        public static void Show(VisualElement element) { SetVisible(element, true); }

        /// <summary>
        /// Sets visibility of a visual element.
        /// </summary>
        /// <param name="element">The element to show</param>
        /// <param name="visible">the wanted visibility.</param>
        public static void SetVisible(VisualElement element, bool visible)
        {
            if (element == null)
                return; // TODO hides programming errors silently, preferably remove
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Loads an asset from the common UI resource folder.
        /// </summary>
        /// <typeparam name="T">type fo the file to load</typeparam>
        /// <param name="filename">name of the file</param>
        /// <returns>A reference to the loaded file</returns>
        internal static T LoadUIAsset<T>(string filename) where T : UnityObject =>
            AssetDatabase.LoadAssetAtPath<T>($"{k_UIAssetPath}/{filename}");
    }
}
