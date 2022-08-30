using UnityEditor;

namespace Unity.Tutorials.Core.Editor
{
    internal static class HomeWindowProxy
    {
        public static void ShowTutorials()
        {
            HomeWindow.Show(HomeWindow.HomeMode.Tutorial);
        }
    }
}
