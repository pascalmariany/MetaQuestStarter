using UnityEngine;
using UnityEditor;

namespace Unity.Tutorials.Core.Editor
{
    /// <summary>
    /// Proxy class for accessing UnityEditor.LocalizationDatabase.
    /// </summary>
    internal class LocalizationDatabaseProxy
    {
        /// <summary>
        /// Is Editor Localization enabled.
        /// </summary>
        public static bool enableEditorLocalization
        {
            get => LocalizationDatabase.enableEditorLocalization;
            set => LocalizationDatabase.enableEditorLocalization = value;
        }

        /// <summary>
        /// Returns the current Editor language.
        /// </summary>
        public static SystemLanguage currentEditorLanguage =>
            LocalizationDatabase.currentEditorLanguage;

        /// <summary>
        /// Returns available Editor languages.
        /// </summary>
        /// <returns></returns>
        public static SystemLanguage[] GetAvailableEditorLanguages() =>
            LocalizationDatabase.GetAvailableEditorLanguages();
    }
}
