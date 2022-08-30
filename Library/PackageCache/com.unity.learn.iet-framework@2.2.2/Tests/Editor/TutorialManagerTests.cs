using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Unity.Tutorials.Core.Editor.Tests
{
    public class TutorialManagerTests : TestBase
    {
        class TestWindow1 : EditorWindow
        {
        }

        class TestWindow2 : EditorWindow
        {
        }

        [SerializeField]
        string m_TempFolderPath;
        [SerializeField]
        string m_TutorialLayoutPath;
        [SerializeField]
        Tutorial m_Tutorial;
        [SerializeField]
        string m_TutorialScenePath;

        [SetUp]
        public void SetUp()
        {
            if (EditorApplication.isPlaying)
                return;

            var tempFolderGUID = AssetDatabase.CreateFolder("Assets", "Temp");
            m_TempFolderPath = AssetDatabase.GUIDToAssetPath(tempFolderGUID);

            // Make sure we start afresh each time
            var tempScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(tempScene, $"{m_TempFolderPath}/TempScene.unity");

            m_Tutorial = ScriptableObject.CreateInstance<Tutorial>();
            // we don't have unit tests nor intergation tests for progress tracking currently
            m_Tutorial.ProgressTrackingEnabled = false; 
            AssetDatabase.CreateAsset(m_Tutorial, m_TempFolderPath + "/Tutorial.asset");
            var page = ScriptableObject.CreateInstance<TutorialPage>();
            AssetDatabase.CreateAsset(page, m_TempFolderPath + "/Page.asset");
            m_Tutorial.m_Pages = new Tutorial.TutorialPageCollection(new[] { page });
            AssetDatabase.Refresh();

            // TODO layout biz disabled completely for now in order to be able to run some tests
            //SetupLayout(m_Tutorial);
            SetupScene(m_Tutorial);
        }

        void SetupLayout(Tutorial tutorial)
        {
            // Ensure tutorial window is not open
            foreach (var window in Resources.FindObjectsOfTypeAll<TutorialWindow>())
            {
                window.Close();
            }

            Assert.That(Resources.FindObjectsOfTypeAll<TestWindow1>(), Is.Empty, "TestWindow1 is present");

            // Save current layout and use it as the tutorial layout
            // TODO Cannot seem to save the layout when ran in Yamato. Let's use the default layout instead.
            //m_TutorialLayoutPath = m_TempFolderPath + "/TutorialLayout.dwlt";
            m_TutorialLayoutPath = TutorialContainer.k_DefaultLayoutPath;
            //WindowLayout.SaveWindowLayout(m_TutorialLayoutPath);
            var tutorialLayout = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(m_TutorialLayoutPath);
            tutorial.WindowLayout = tutorialLayout;
            TutorialManager.PrepareWindowLayout(tutorial.WindowLayoutPath);

            // Open TestWindow1
            EditorWindow.GetWindow<TestWindow1>().titleContent.text = "TestWindow1";

            Assert.That(Resources.FindObjectsOfTypeAll<TestWindow1>(), Is.Not.Empty, "TestWindow1 is not present");
        }

        void SetupScene(Tutorial tutorial)
        {
            // For some reason Yamato runs fail to load an existing scenes so create one on the fly.
            var tutorialScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            m_TutorialScenePath = m_TempFolderPath + "/TutorialScene.unity";
            EditorSceneManager.SaveScene(tutorialScene, m_TutorialScenePath);
            EditorSceneManager.CloseScene(tutorialScene, true);

            tutorial.Scenes = new[] { AssetDatabase.LoadAssetAtPath<SceneAsset>(m_TutorialScenePath) };

            Assert.That(tutorial.Scenes[0], Is.Not.Null, $"Could not load tutorial scene '{m_TutorialScenePath}'.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Ensure test windows are closed
            foreach (var window in Resources.FindObjectsOfTypeAll<TestWindow1>())
            {
                window.Close();
            }

            foreach (var window in Resources.FindObjectsOfTypeAll<TestWindow2>())
            {
                window.Close();
            }

            // Getting NRE from coroutines if closing the window so don't do it for now.
            //foreach (var window in Resources.FindObjectsOfTypeAll<TutorialWindow>())
            //{
            //    window.Close();
            //}

            // Wait for delayed scene restore before we delete the Temp folder
            //yield return new WaitForDelayCall();
            yield return new WaitForDelayCall();

            // Make sure to nuke any old state TutorialManager could have
            Object.DestroyImmediate(TutorialManager.Instance);

            // Deletion of TutorialManager above will take care all of these:
            // Delete original layout to avoid triggering the layout restore again when TutorialWindow is closed
            // File.Delete(TutorialManager.k_OriginalLayoutPath);
            // Load tutorial layout reverting to layout before test run (with the exception of a potential closed TutorialWindow)
            // EditorUtility.LoadWindowLayout(m_TutorialLayoutPath);

            AssetDatabase.DeleteAsset(m_TempFolderPath);
        }

#if UNITY_EDITOR_LINUX
        [Ignore("TODO: fails on Ubuntu for some odd reason")]
#endif
        [Test]
        public void StartTutorial_TheStartedTutorialIsTheActiveTutorial()
        {
            TutorialManager.Instance.StartTutorial(m_Tutorial);
            Assert.AreEqual(m_Tutorial, TutorialManager.Instance.ActiveTutorial);
        }

        [Test]
        public void StartTutorial_LoadsTutorialScene()
        {
            TutorialManager.Instance.StartTutorial(m_Tutorial);
            Assert.AreEqual(m_TutorialScenePath, SceneManager.GetSceneAt(0).path);
        }

#if UNITY_EDITOR_LINUX && UNITY_2019
        [Ignore("TODO: disabled due to having StartTutorial_ActiveTutorialIsNullAfterTutorialWindowIsClosed disabled")]
#endif
        [Test]
        public void StartTutorial_CreatesTutorialWindow()
        {
            Assert.IsEmpty(Resources.FindObjectsOfTypeAll<TutorialWindow>());

            TutorialManager.Instance.StartTutorial(m_Tutorial);

            Assert.IsNotEmpty(Resources.FindObjectsOfTypeAll<TutorialWindow>());
        }

#if UNITY_EDITOR_LINUX && UNITY_2019
        [Ignore("TODO: fails (crashes?) often on Ubuntu 2019 for some odd reason")]
#endif
        [Test]
        public void StartTutorial_ActiveTutorialIsNullAfterTutorialWindowIsClosed()
        {
            TutorialManager.Instance.StartTutorial(m_Tutorial);

            foreach (var window in Resources.FindObjectsOfTypeAll<TutorialWindow>())
            {
                window.Close();
            }

            Assert.IsNull(TutorialManager.Instance.ActiveTutorial);
        }

        [Ignore("TODO: disabled due to weird issues with layout loading")]
        [UnityTest]
        [TestCase(false, TestName = "StartTutorial_WhenTutorialWindowIsNotOpen_OriginalLayoutIsRestoredWhenTutorialIsCompleted", ExpectedResult = null)]
        [TestCase(true, TestName = "StartTutorial_WhenTutorialWindowIsOpen_OriginalLayoutIsRestoredWhenTutorialIsCompleted", ExpectedResult = null)]
        public IEnumerator StartTutorial_OriginalLayoutIsRestoredWhenTutorialIsCompleted(bool tutorialWindowOpen)
        {
            if (tutorialWindowOpen)
                EditorWindow.GetWindow<TutorialWindow>();

            TutorialManager.Instance.StartTutorial(m_Tutorial);
            yield return new WaitForDelayCall();

            // Complete tutorial
            m_Tutorial.CurrentPage.ValidateCriteria();
            m_Tutorial.TryGoToNextPage();
            yield return new WaitForDelayCall();

            // Assert that original layout is restored (i.e. TestWindow1 should exist)
            Assert.That(Resources.FindObjectsOfTypeAll<TestWindow1>(), Is.Not.Empty, "TestWindow1 is not present");
        }

        [Ignore("TODO: disabled due to weird issues with layout loading")]
        [UnityTest]
        public IEnumerator RestartTutorial_RestoresTutorialLayout()
        {
            TutorialManager.Instance.StartTutorial(m_Tutorial);

            // Open TestWindow2
            EditorWindow.GetWindow<TestWindow2>().titleContent.text = "TestWindow2";

            // Complete tutorial
            TutorialManager.Instance.ResetTutorial();
            yield return new WaitForDelayCall();

            // Assert that tutorial layout is restored (i.e. TestWindow2 should not longer be present)
            Assert.That(Resources.FindObjectsOfTypeAll<TestWindow2>(), Is.Empty, "TestWindow2 is present");

            // Assert that original layout was not restored (i.e. TestWindow1 should not be present)
            Assert.That(Resources.FindObjectsOfTypeAll<TestWindow1>(), Is.Empty, "TestWindow1 is present");
        }

        [UnityTest]
        public IEnumerator StartTutorial_WhenInPlayMode_ExitsPlayMode()
        {
            yield return new EnterPlayMode();

            TutorialManager.Instance.StartTutorial(m_Tutorial);

            yield return new WaitForDelayCall();

            Assert.That(EditorApplication.isPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator ResetTutorial_WhenInPlayMode_ExitsPlayMode()
        {
            TutorialManager.Instance.StartTutorial(m_Tutorial);

            yield return new EnterPlayMode();

            TutorialManager.Instance.ResetTutorial();

            yield return new WaitForDelayCall();

            Assert.That(EditorApplication.isPlaying, Is.False);
        }

#if !UNITY_EDITOR_WIN
        [Ignore("TODO: stability issues on Linux/maxOS Yamato runs")]
#endif
        [UnityTest]
        public IEnumerator StartTutorial_OriginalSceneStateIsRestoredWhenTutorialIsCompleted()
        {
            // Open some new scenes
            var scene0Path = m_TempFolderPath + "/Scene0.unity";
            var scene0 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            EditorSceneManager.SaveScene(scene0, scene0Path);
            var scene1 = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            var scene1Path = m_TempFolderPath + "/Scene1.unity";
            EditorSceneManager.SaveScene(scene1, scene1Path);
            var scene2 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var scene2Path = m_TempFolderPath + "/Scene2.unity";
            EditorSceneManager.SaveScene(scene2, scene2Path);
            var scene3 = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            var scene3Path = m_TempFolderPath + "/Scene3.unity";
            EditorSceneManager.SaveScene(scene3, scene3Path);

            // Set the last scene to be active
            SceneManager.SetActiveScene(scene3);

            // Unload scene 2 and 3
            EditorSceneManager.CloseScene(scene1, false);
            EditorSceneManager.CloseScene(scene2, false);

            int originalSceneCount = SceneManager.sceneCount;

            TutorialManager.Instance.StartTutorial(m_Tutorial);
            // Complete tutorial
            m_Tutorial.CurrentPage.ValidateCriteria();
            m_Tutorial.TryGoToNextPage();
            yield return new WaitForDelayCall();
            // We don't currently have explicit exit procedure for a tutorial so it's simplest to close the tutorial window
            // to achieve exit behavior, which includes restoring the original scenes...
            // ...except we cannot do that right now, getting NRE from coroutines if closing the window.
            //foreach (var window in Resources.FindObjectsOfTypeAll<TutorialWindow>())
            //{
            //    window.Close();
            //}
            TutorialManager.Instance.RestoreOriginalState();

            // NOTE It seems two of these are required in order to wait enough for the scene restoration.
            yield return new WaitForDelayCall();
            yield return new WaitForDelayCall();

            // Assert that we're back at original scene state
            Assert.That(SceneManager.sceneCount, Is.EqualTo(originalSceneCount));
            Assert.That(SceneManager.GetSceneAt(0).path, Is.EqualTo(scene0Path));
            Assert.That(SceneManager.GetSceneAt(1).path, Is.EqualTo(scene1Path));
            Assert.That(SceneManager.GetSceneAt(2).path, Is.EqualTo(scene2Path));
            Assert.That(SceneManager.GetSceneAt(3).path, Is.EqualTo(scene3Path));
            Assert.That(SceneManager.GetSceneAt(0).isLoaded, Is.True);
            Assert.That(SceneManager.GetSceneAt(1).isLoaded, Is.False);
            Assert.That(SceneManager.GetSceneAt(2).isLoaded, Is.False);
            Assert.That(SceneManager.GetSceneAt(3).isLoaded, Is.True);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(scene3Path));
        }
    }
}
