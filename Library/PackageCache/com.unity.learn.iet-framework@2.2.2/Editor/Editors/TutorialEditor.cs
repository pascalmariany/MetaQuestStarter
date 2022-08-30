using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Unity.Tutorials.Core.Editor
{
    using static Localization;

    [CustomEditor(typeof(Tutorial))]
    class TutorialEditor : UnityEditor.Editor
    {
        static class Contents
        {
            public static GUIContent autoCompletion = new GUIContent(Tr("Auto Completion"));
            public static GUIContent startAutoCompletion = new GUIContent(Tr("Start Auto Completion"));
            public static GUIContent stopAutoCompletion = new GUIContent(Tr("Stop Auto Completion"));
        }

        static readonly string[] k_PropsToIgnore = { "m_Script", nameof(Tutorial.m_LessonId) };
        static readonly string[] k_PropsToIgnoreNoScene = k_PropsToIgnore
            .Concat(new[] { nameof(Tutorial.m_SceneManagementBehavior) })
            .ToArray();

        private const string k_PagesPropertyPath = "m_Pages.m_Items";

        private static readonly Regex s_MatchPagesPropertyPath =
            new Regex(
                string.Format("^({0}\\.Array\\.size)|(^({0}\\.Array\\.data\\[\\d+\\]))", Regex.Escape(k_PagesPropertyPath))
            );

        Tutorial Target => (Tutorial)target;

        [NonSerialized]
        private string m_WarningMessage;

        protected virtual void OnEnable()
        {
            if (serializedObject.FindProperty(k_PagesPropertyPath) == null)
            {
                m_WarningMessage = string.Format(
                    Tr("Unable to locate property path {0} on this object. Automatic masking updates will not work."),
                    k_PagesPropertyPath
                );
            }

            Undo.postprocessModifications += OnPostprocessModifications;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        protected virtual void OnDisable()
        {
            Undo.postprocessModifications -= OnPostprocessModifications;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        private void OnUndoRedoPerformed()
        {
            if (Target != null)
            {
                Target.RaiseModified();
            }
        }

        private UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] modifications)
        {
            Target.RaiseModified();

            var pagesChanged = false;

            foreach (var modification in modifications)
            {
                if (modification.currentValue.target != target)
                    continue;

                var propertyPath = modification.currentValue.propertyPath;
                if (s_MatchPagesPropertyPath.IsMatch(propertyPath))
                {
                    pagesChanged = true;
                    break;
                }
            }

            if (pagesChanged)
                Target.RaiseModified();

            return modifications;
        }

        public override void OnInspectorGUI()
        {
            TutorialProjectSettings.DrawDefaultAssetRestoreWarning();

            if (!string.IsNullOrEmpty(m_WarningMessage))
                EditorGUILayout.HelpBox(m_WarningMessage, MessageType.Warning);

            if (SerializedTypeDrawer.UseDefaultEditors)
            {
                base.OnInspectorGUI();
            }
            else
            {
                serializedObject.Update();

                // Scene management options are visible only when we have no scenes specified.
                DrawPropertiesExcluding(serializedObject, Target.HasScenes() ? k_PropsToIgnoreNoScene : k_PropsToIgnore);

                serializedObject.ApplyModifiedProperties();
            }

            // Auto completion
            GUILayout.Label(Contents.autoCompletion, EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(Target.IsCompleted))
            {
                if (GUILayout.Button(Target.IsAutoCompleting ? Contents.stopAutoCompletion : Contents.startAutoCompletion))
                {
                    if (Target.IsAutoCompleting)
                        Target.StopAutoCompletion();
                    else
                        Target.StartAutoCompletion();
                }
            }
        }
    }
}
