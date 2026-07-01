using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Project.Editor
{
    public static class BuildPlatforms
    {
        [Serializable]
        private sealed class Metrics
        {
            public string target;
            public string result;
            public double seconds;
            public ulong bytes;
            public int warnings;
            public int errors;
            public Step[] steps;
        }

        [Serializable]
        private sealed class Step
        {
            public string name;
            public double seconds;
        }

        public static void Build()
        {
            string[] args = Environment.GetCommandLineArgs();
            string output = Value(args, "-buildOutput");
            string reportPath = Value(args, "-buildReport");
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();

            if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(reportPath) || scenes.Length == 0)
                throw new ArgumentException("Build output, report, and scenes are required");

            Directory.CreateDirectory(Path.GetDirectoryName(output));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));

            BuildOptions options = BuildOptions.StrictMode | BuildOptions.DetailedBuildReport;
            if (args.Contains("-profileBuild"))
                options |= BuildOptions.Development;

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = EditorUserBuildSettings.activeBuildTarget,
                options = options
            });

            BuildSummary summary = report.summary;
            File.WriteAllText(reportPath, JsonUtility.ToJson(new Metrics
            {
                target = summary.platform.ToString(),
                result = summary.result.ToString(),
                seconds = summary.totalTime.TotalSeconds,
                bytes = summary.totalSize,
                warnings = summary.totalWarnings,
                errors = summary.totalErrors,
                steps = report.steps.Select(step => new Step
                {
                    name = step.name,
                    seconds = step.duration.TotalSeconds
                }).ToArray()
            }, true), new UTF8Encoding(false));

            if (summary.result == BuildResult.Succeeded && summary.platform == BuildTarget.WebGL)
                File.AppendAllText(Path.Combine(output, "TemplateData", "style.css"), "\n@media (max-width: 960px), (max-height: 578px) { #unity-container.unity-desktop { width: 100vw } .unity-desktop #unity-canvas { width: 100%; height: auto; aspect-ratio: 16 / 9 } .unity-desktop #unity-footer { display: none } }\n", new UTF8Encoding(false));

            if (summary.result != BuildResult.Succeeded)
                throw new Exception(summary.result.ToString());
        }

        private static string Value(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
