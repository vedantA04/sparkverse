using System;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public static class Build
    {
        public static void PerformBuild()
        {
            var (exeName, buildTarget) = Application.platform switch
            {
                RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor =>
                    ("SparkVerse.exe", BuildTarget.StandaloneWindows),
                RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor =>
                    ("SparkVerse", BuildTarget.StandaloneLinux64),
                RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor =>
                    ("SparkVerse", BuildTarget.StandaloneOSX),
                _ => throw new ArgumentException($"Unknown platform '{Application.platform}'")
            };

            BuildPipeline.BuildPlayer(
                new[]
                {
                    "Assets/Scenes/StartScene.unity",
                    "Assets/Scenes/SimulationScene.unity"
                },
                "build/" + exeName,
                buildTarget,
                BuildOptions.None);
        }
    }
}
