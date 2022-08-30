using UnityEditor;
using UnityEngine;

namespace Unity.Tutorials.Core.Editor
{
    [CustomEditor(typeof(TutorialContainer))]
    class TutorialContainerEditor : UnityEditor.Editor
    {
        readonly string[] k_PropertiesToHide =
        {
            "m_Script",
            nameof(TutorialContainer.Modified)  // this is not not something tutorial authors should subscribe to typically
        };

        TutorialContainer Target => (TutorialContainer)target;

        void OnEnable()
        {
            Undo.postprocessModifications += OnPostprocessModifications;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        void OnDisable()
        {
            Undo.postprocessModifications -= OnPostprocessModifications;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        void OnUndoRedoPerformed()
        {
            Target.RaiseModified();
        }

        UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] modifications)
        {
            Target.RaiseModified();
            // If this container is parented, we consider modifications to 'this'
            // container also to be modifications of the parent.
            if (Target.ParentContainer != null)
                Target.ParentContainer.RaiseModified();
            return modifications;
        }

        public override void OnInspectorGUI()
        {
            TutorialProjectSettings.DrawDefaultAssetRestoreWarning();

            if (GUILayout.Button(Localization.Tr(MenuItems.ShowTutorials)))
            {
                // Make sure we will display 'this' container in the window.
                var window = Target.ProjectLayout != null
                    ? TutorialWindow.GetOrCreateWindowAndLoadLayout(Target)
                    : TutorialWindow.GetOrCreateWindowNextToInspector();

                window.ActiveContainer = Target;
            }

            EditorGUILayout.Space(10);

            if (SerializedTypeDrawer.UseDefaultEditors)
            {
                base.OnInspectorGUI();
            }
            else
            {
                serializedObject.Update();
                DrawPropertiesExcluding(serializedObject, k_PropertiesToHide);
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
