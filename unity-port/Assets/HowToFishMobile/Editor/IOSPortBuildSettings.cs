#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace HowToFish.Mobile.Editor
{
    [InitializeOnLoad]
    internal static class IOSPortBuildSettings
    {
        static IOSPortBuildSettings()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS) return;
            Apply();
        }

        [MenuItem("How to Fish/iOS/Apply Port Settings")]
        public static void Apply()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.iOS.targetOSVersionString = "16.0";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, 1);
            Debug.Log("[HowToFish.Mobile] Applied iOS landscape + ARM64/IL2CPP settings.");
        }
    }
}
#endif
