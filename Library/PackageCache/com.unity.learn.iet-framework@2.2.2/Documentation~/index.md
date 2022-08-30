# About Tutorial Framework

![](images/hero.png)

This package is used to display interactive in-Editor tutorials (IET) in tutorial projects and project templates.

## Installation

For Unity 2021.2 and newer, simply search for "Tutorial Framework" in the Package Manager. For older Unity versions, this package is not currently discoverable,
and you need to add the following line to the `dependencies` list of `Packages/manifest.json`:  
`"com.unity.learn.iet-framework": "2.2.1`

Make sure to update to the latest version of the package.

## Requirements

This version of Tutorial Framework is compatible with the following versions of the Unity Editor:

* 2019.4 and newer (LTS versions recommended)

## Known issues
- A benign "BuildStartedCriterion must be instantiated using the ScriptableObject.CreateInstance method..." warning in the Console when making a build.
- **Window Title** of a `TutorialWelcomePage` asset cannot be edited at real-time; reopen the asset in the welcome dialog in order to see the change in the window title.
- Windows & Unity 2019.4 (and older): A benign "No texture data available to upload" error in the Console when switching target platform while having a tutorial page with video open.
- The save dialog does not appear if Play mode is active when exiting the tutorial.
- `SceneViewCameraMovedCriterion` has no ability to distinguish different types of camera movements.
- A misconfiguration of masking settings is likely to occur when upgrading a tutorial project between major Unity versions due to changes in the Unity Engine/Editor:
  - When upgrading to 2020.1 and newer, certain UI elements, for example, `SceneView` and `Toolbar`, wre moved from `UnityEngine` assembly to `UnityEngine.CoreModule`.
    This issue can addressed by following the instructions [here](framework-documentation.md#assembly-differences-between-unity-versions).
  - When upgrading to 2021.1 and newer, the implementation of many common UI elements has changed from IMGUI to UI Toolkit.
    This issue can addressed by following the instructions [here](framework-documentation.md#ui-implementation-differences-between-unity-versions).

# Using Tutorial Framework
To actually develop any tutorials, the Tutorial Authoring Tools package is needed. Install it by adding the following line to `Packages/manifest.json`:  
`"com.unity.learn.iet-framework.authoring": "1.0.0"`  
Make sure to update to the latest version of the package. After the installation, refer to the Tutorial Authoring Tools' documentation for more information.
