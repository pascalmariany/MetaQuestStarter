using UnityEditor;
using UnityEngine;

namespace Unity.Tutorials.Core.Editor
{
    internal static class HandleUtilityProxy
    {
        internal static GameObject FindSelectionBase(GameObject gameObject)
        {
#if UNITY_2020_2_OR_NEWER
            return HandleUtility.FindSelectionBaseForPicking(gameObject);
#else
            return HandleUtility.FindSelectionBase(gameObject);
#endif
        }
    }
}
