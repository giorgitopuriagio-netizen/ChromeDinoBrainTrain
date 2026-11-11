using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChromeDino.Editor
{
    public static class ProjectTools
    {
        [MenuItem("Chrome Dino/Open Main Menu")]
        public static void OpenMainMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        }

        [MenuItem("Chrome Dino/Build Windows")]
        public static void BuildWindows()
        {
            string output = Path.GetFullPath("Builds/Windows/ChromeDino.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Training.unity", "Assets/Scenes/AITest.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed: " + report.summary.result);
            Debug.Log("Windows build: " + output);
        }
    }
}
