using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds the Quest APK. Works from the menu or headless:
// Unity.exe -batchmode -quit -projectPath <project> -buildTarget Android -executeMethod QuestBuild.Build
public static class QuestBuild
{
    public const string OutputDirectory = "Build";
    public const string ApkName = "ConcertPrototype.apk";

    [MenuItem("Build/Build Quest APK")]
    public static void Build()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Fail("No scenes enabled in Build Settings");
            return;
        }

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        Directory.CreateDirectory(OutputDirectory);
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(OutputDirectory, ApkName),
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Fail($"Build {report.summary.result} with {report.summary.totalErrors} error(s)");
            return;
        }

        // The install script reads this to know which app to launch
        File.WriteAllText(Path.Combine(OutputDirectory, "package.txt"), PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android));
        Debug.Log($"Quest build succeeded: {options.locationPathName} ({report.summary.totalSize / (1024 * 1024)} MB)");
    }

    private static void Fail(string message)
    {
        Debug.LogError("Quest build failed: " + message);
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(1);
        }
    }
}
