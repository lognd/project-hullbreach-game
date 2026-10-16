using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hullbreach.Editor
{
    // One build entry point for menus, `play build` and CI, so nobody has to
    // touch Build Profiles or remember the scene list.
    // frob:doc docs/reference/hullbreach-editor.md#hullbreachbuild
    public static class HullbreachBuild
    {
        // frob:doc docs/reference/hullbreach-editor.md#hullbreachbuild
        public const string LobbyScene = "Assets/Scenes/LobbyScene.unity";

        // frob:doc docs/reference/hullbreach-editor.md#hullbreachbuild
        public const string GameScene = "Assets/Scenes/MultiplayerGame.unity";

        // Written next to every build; the doctor reads it to tell a stale build.
        // frob:doc docs/reference/hullbreach-editor.md#hullbreachbuild
        public const string StampFile = "hullbreach-build.json";

        // Printed on success and failure so wrapper scripts can grep one line.
        // frob:doc docs/reference/hullbreach-editor.md#hullbreachbuild
        public const string ResultPrefix = "HullbreachBuild:";

        [Serializable]
        sealed class Stamp
        {
            public string target;
            public string kind;
            public string commit;
            public bool development;
            public string unity;
            public string builtUtc;
            public string executable;
            public string backend;
            public string[] scenes;
        }

        // frob:doc docs/reference/hullbreach-editor.md#hullbreachbuild
        [MenuItem("Hullbreach/Build/Client for this computer")]
        public static void BuildClientMenu() => Run(HostTarget(), "client", null, null, development: true, backend: "auto");

        // frob:doc docs/reference/hullbreach-editor.md#hullbreachbuild
        [MenuItem("Hullbreach/Build/Dedicated server for this computer")]
        public static void BuildServerMenu() => Run(HostTarget(), "server", null, null, development: true, backend: "auto");

        // Entry point for -executeMethod Hullbreach.Editor.HullbreachBuild.BuildFromCommandLine.
        // Args: -hullbreachBuildTarget windows|linux|macos, -hullbreachBuildKind client|server,
        // -hullbreachBuildOutput <dir>, -hullbreachBuildCommit <sha>, -hullbreachBuildRelease,
        // -hullbreachBuildBackend auto|il2cpp|mono.
        // frob:doc docs/reference/hullbreach-editor.md#hullbreachbuild
        public static void BuildFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            string target = Value(args, "-hullbreachBuildTarget") ?? HostTarget();
            string kind = Value(args, "-hullbreachBuildKind") ?? "client";
            bool release = args.Any(a => string.Equals(a, "-hullbreachBuildRelease", StringComparison.OrdinalIgnoreCase));
            string backend = Value(args, "-hullbreachBuildBackend") ?? "auto";
            bool ok = Run(target, kind, Value(args, "-hullbreachBuildOutput"), Value(args, "-hullbreachBuildCommit"), !release, backend);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        // The scenes a build of this kind ships, in load order: clients start in
        // the lobby, a server goes straight to the match.
        // frob:doc docs/reference/hullbreach-editor.md#hullbreachbuild
        public static string[] ScenesFor(string kind)
        {
            if (kind == "server") return new[] { GameScene };
            var scenes = new List<string> { LobbyScene, GameScene };
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (s.enabled && !scenes.Contains(s.path)) scenes.Add(s.path);
            return scenes.ToArray();
        }

        static bool Run(string target, string kind, string output, string commit, bool development, string backend)
        {
            if (!TryTarget(target, out BuildTarget buildTarget, out string exe))
                return Fail($"unknown target '{target}' (use windows, linux or macos).");
            if (kind != "client" && kind != "server")
                return Fail($"unknown kind '{kind}' (use client or server).");
            if (backend != "auto" && backend != "il2cpp" && backend != "mono")
                return Fail($"unknown backend '{backend}' (use auto, il2cpp or mono).");

            // A missing module is the most common beginner failure; name the exact Hub module.
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, buildTarget))
                return Fail($"the {ModuleName(target, kind)} module is not installed. {AddModules}");
            string[] variations = Variations(buildTarget);
            if (kind == "server" && !variations.Any(v => v.Contains("server")))
                return Fail($"the {ModuleName(target, kind)} module is not installed. {AddModules}");

            // The project ships IL2CPP; without that module a Mono build still demos fine.
            bool hasIl2cpp = variations.Any(v => v.Contains("il2cpp") && v.Contains(kind == "server" ? "server" : "player"));
            if (backend == "il2cpp" && !hasIl2cpp)
                return Fail($"the IL2CPP part of {ModuleName(target, kind)} is not installed. {AddModules}");
            ScriptingImplementation chosen = backend == "mono" || (backend == "auto" && !hasIl2cpp)
                ? ScriptingImplementation.Mono2x
                : ScriptingImplementation.IL2CPP;
            if (backend == "auto" && !hasIl2cpp)
                Debug.LogWarning($"{ResultPrefix} IL2CPP is not installed for {target}; building with Mono instead (fine for demos).");

            string dir = Path.GetFullPath(output ?? Path.Combine("Builds", $"{target}-{kind}"));
            Directory.CreateDirectory(dir);
            string[] scenes = ScenesFor(kind);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                target = buildTarget,
                targetGroup = BuildTargetGroup.Standalone,
                subtarget = (int)(kind == "server" ? StandaloneBuildSubtarget.Server : StandaloneBuildSubtarget.Player),
                locationPathName = Path.Combine(dir, kind == "server" ? ServerExe(target, exe) : exe),
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            NamedBuildTarget named = kind == "server" ? NamedBuildTarget.Server : NamedBuildTarget.Standalone;
            ScriptingImplementation saved = PlayerSettings.GetScriptingBackend(named);
            Debug.Log($"{ResultPrefix} building {target} {kind} (development={development}, backend={chosen}) into {dir} with scenes: {string.Join(", ", scenes)}");
            BuildReport report;
            try
            {
                // Switch the backend for this build only; the committed setting is restored below.
                PlayerSettings.SetScriptingBackend(named, chosen);
                report = BuildPipeline.BuildPlayer(options);
            }
            catch (BuildFailedException e)
            {
                return Fail($"{e.Message} (is the {ModuleName(target, kind)} module installed?)");
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(named, saved);
            }

            if (report.summary.result != BuildResult.Succeeded)
                return Fail($"{report.summary.result} with {report.summary.totalErrors} error(s); " +
                            $"if the log mentions a missing module, add {ModuleName(target, kind)} in Unity Hub.");

            WriteStamp(dir, target, kind, commit, development, Path.GetFileName(options.locationPathName), chosen, scenes);
            Debug.Log($"{ResultPrefix} OK {target} {kind} -> {options.locationPathName}");
            return true;
        }

        const string AddModules = "Unity Hub -> Installs -> 6000.0.43f1 -> gear -> Add modules.";

        // Installed player variations for a target, e.g. win64_player_development_mono.
        static string[] Variations(BuildTarget target)
        {
            string engine = BuildPipeline.GetPlaybackEngineDirectory(target, BuildOptions.None);
            string dir = string.IsNullOrEmpty(engine) ? null : Path.Combine(engine, "Variations");
            if (dir == null || !Directory.Exists(dir)) return Array.Empty<string>();
            return Directory.GetDirectories(dir).Select(d => Path.GetFileName(d).ToLowerInvariant()).ToArray();
        }

        static void WriteStamp(string dir, string target, string kind, string commit, bool development, string executable,
                               ScriptingImplementation backend, string[] scenes)
        {
            var stamp = new Stamp
            {
                target = target,
                kind = kind,
                commit = commit ?? "",
                development = development,
                unity = Application.unityVersion,
                builtUtc = DateTime.UtcNow.ToString("o"),
                executable = executable,
                backend = backend == ScriptingImplementation.IL2CPP ? "il2cpp" : "mono",
                scenes = scenes,
            };
            File.WriteAllText(Path.Combine(dir, StampFile), JsonUtility.ToJson(stamp, true));
        }

        static bool Fail(string message)
        {
            Debug.LogError($"{ResultPrefix} FAILED {message}");
            return false;
        }

        static string HostTarget() =>
            Application.platform == RuntimePlatform.OSXEditor ? "macos"
            : Application.platform == RuntimePlatform.LinuxEditor ? "linux"
            : "windows";

        static bool TryTarget(string name, out BuildTarget target, out string exe)
        {
            switch (name)
            {
                case "windows": target = BuildTarget.StandaloneWindows64; exe = "Project Hullbreach.exe"; return true;
                case "linux": target = BuildTarget.StandaloneLinux64; exe = "ProjectHullbreach.x86_64"; return true;
                case "macos": target = BuildTarget.StandaloneOSX; exe = "Project Hullbreach.app"; return true;
                default: target = BuildTarget.NoTarget; exe = null; return false;
            }
        }

        static string ServerExe(string target, string exe) =>
            target == "windows" ? "HullbreachServer.exe" : target == "linux" ? "HullbreachServer.x86_64" : exe;

        static string ModuleName(string target, string kind)
        {
            string os = target == "windows" ? "Windows" : target == "linux" ? "Linux" : "Mac";
            return kind == "server" ? $"{os} Dedicated Server Build Support" : $"{os} Build Support";
        }

        static string Value(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
