using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Tests;
using Assert = UnityEngine.Assertions.Assert;

namespace UnityEditor.XR.OpenXR.Tests
{
    internal class OpenXRRuntimeSelectorTests : OpenXRInputTestsBase
    {
        [Test]
        public void NoAvailableRuntimesTest()
        {
            List<OpenXRRuntimeSelector.RuntimeDetector> detectorList = OpenXRRuntimeSelector.GenerateRuntimeDetectorList();
            Assert.IsTrue(detectorList.Count > 0);
            Assert.IsTrue(detectorList[0] is OpenXRRuntimeSelector.SystemDefault, "First choice should always be SystemDefault");
            Assert.IsTrue(detectorList[detectorList.Count - 1] is OpenXRRuntimeSelector.OtherRuntime, "Last choice should always be Other");
        }

        [Test]
        public void BuiltInRuntimesAsAvailableRuntimesTest()
        {
            // Simulate what happens if the AvailableRuntimes registry key is empty.
            // WindowsMR, SteamVR, and Oculus should all be added to the list.
            Dictionary<string, int> runtimePathToValue = new Dictionary<string, int>();
            List<OpenXRRuntimeSelector.RuntimeDetector> detectorList = OpenXRRuntimeSelector.GenerateRuntimeDetectorList(runtimePathToValue);
            Assert.IsTrue(detectorList.Count > 0);
            Assert.IsTrue(detectorList[0] is OpenXRRuntimeSelector.SystemDefault);
            Assert.IsTrue(detectorList[detectorList.Count - 1] is OpenXRRuntimeSelector.OtherRuntime);

            bool foundWindowsMRDetector = false;
            bool foundSteamVRDetector = false;
            bool foundOculusDetector = false;
            foreach (var detector in detectorList)
            {
                foundWindowsMRDetector |= detector is OpenXRRuntimeSelector.WindowsMRDetector;
                foundSteamVRDetector |= detector is OpenXRRuntimeSelector.SteamVRDetector;
                foundOculusDetector |= detector is OpenXRRuntimeSelector.OculusDetector;
            }

            Assert.IsTrue(foundWindowsMRDetector);
            Assert.IsTrue(foundSteamVRDetector);
            Assert.IsTrue(foundOculusDetector);
        }

        [Test]
        public void DiscoveredAvailableRuntimesTest()
        {
            OpenXRRuntimeSelector.WindowsMRDetector windowsMRDetector = new OpenXRRuntimeSelector.WindowsMRDetector();
            OpenXRRuntimeSelector.SteamVRDetector steamVRDetector = new OpenXRRuntimeSelector.SteamVRDetector();
            OpenXRRuntimeSelector.OculusDetector oculusDetector = new OpenXRRuntimeSelector.OculusDetector();
            string enabledRuntime = "enabledRuntime";
            string disabledRuntime = "disabledRuntime";

            Dictionary<string, int> runtimePathToValue = new Dictionary<string, int>()
            {
                { windowsMRDetector.jsonPath, 0},
                { steamVRDetector.jsonPath, 1},
                { enabledRuntime, 0 },
                { disabledRuntime, 1 }
            };

            List<OpenXRRuntimeSelector.RuntimeDetector> detectorList = OpenXRRuntimeSelector.GenerateRuntimeDetectorList(runtimePathToValue);
            Assert.IsTrue(detectorList.Count > 0);
            Assert.IsTrue(detectorList[0] is OpenXRRuntimeSelector.SystemDefault);
            Assert.IsTrue(detectorList[detectorList.Count - 1] is OpenXRRuntimeSelector.OtherRuntime);

            HashSet<string> detectedJsons = new HashSet<string>();
            foreach (var detector in detectorList)
            {
                detectedJsons.Add(detector.jsonPath);
            }

            Assert.IsTrue(detectedJsons.Contains(windowsMRDetector.jsonPath));
            Assert.IsFalse(detectedJsons.Contains(steamVRDetector.jsonPath));
            Assert.IsTrue(detectedJsons.Contains(oculusDetector.jsonPath));
            Assert.IsTrue(detectedJsons.Contains(enabledRuntime));
            Assert.IsFalse(detectedJsons.Contains(disabledRuntime));
        }
    }
}