using System;
using UnityEditor;

namespace Unity.Tutorials.Core.Editor
{
    internal static class HostViewProxy
    {
        public static event Action actualViewChanged;

        static HostViewProxy()
        {
            HostView.actualViewChanged += OnActualViewChanged;
        }

        static void OnActualViewChanged(HostView hostView)
        {
            actualViewChanged?.Invoke();
        }
    }
}
