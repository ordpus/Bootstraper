#nullable enable

using System.Reflection;

using BootstrapApi;

using Bootstrap;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using Bootstrap.Patcher;

using RimWorld;
using RimWorld.Planet;

using UnityEngine;

using Verse;
using Verse.Sound;

namespace RimWorldBootstrap;

// ReSharper disable once ClassNeverInstantiated.Global
public class RimWorldBootstrapMod : Mod {
    internal const string Arg = "bootstrap";
    internal const string RestartArg = "bootstrap_restart_time";

    public RimWorldBootstrapMod(ModContentPack content) : base(content) {
        if (!AppDomain.CurrentDomain.GetAssemblies().Any(x => x.GetName().Name.Equals("BootstrapApi.dll"))) {
            Assembly.LoadFrom(Path.Combine(Content.RootDir, "Bootstrap", "core", "BootstrapApi.dll"));
            Assembly.LoadFrom(Path.Combine(Content.RootDir, "Bootstrap", "core", "Bootstrap.dll"));
        }

        if (CopyFolder())
            CommandlineUtil.Restart(content.RootDir, "copy");
        else if (ShouldReload()) {
            Patch();
            CommandlineUtil.Restart(content.RootDir, "bootstrap");
        }

        if (ShouldPopupWindow()) PopupWindow(null);
    }

    private static void Patch() {
        AssemblySet.Reset();
        BootstrapPluginManager.Register(
            new PostInitInfo(
                typeof(ThingWithComps),
                typeof(ThingComp),
                nameof(ThingWithComps.InitializeComps),
                new DefaultPostInitProvider(
                    $"{typeof(ThingWithComps).FullName}:{nameof(ThingWithComps.GetComp)}")));
        BootstrapPluginManager.Register(
            new PostInitInfo(
                typeof(HediffWithComps),
                typeof(HediffComp),
                nameof(HediffWithComps.InitializeComps),
                new DefaultPostInitProvider($"{typeof(HediffWithComps).FullName}:{nameof(HediffWithComps.GetComp)}")));
        Patcher.Patch();
    }

    private static void PopupWindow(string? phase) {
        LongEventHandler.ClearQueuedEvents();
        LongEventHandler.toExecuteWhenFinished.Clear();

        LanguageDatabase.InitAllMetadata();

        KeyPrefs.data = new KeyPrefsData();
        foreach (var f in typeof(KeyBindingDefOf).GetFields()) f.SetValue(null, new KeyBindingDef());
        Current.Root.soundRoot = new SoundRoot();
        Current.Root.uiRoot = new UIRoot_RimworldBootstrap(phase);
    }

    private static bool ShouldPopupWindow() {
        return GenCommandLine.TryGetCommandLineArg(Arg, out var phase) && phase == "error";
    }

    private static bool ShouldReload() {
        return !GenCommandLine.TryGetCommandLineArg(Arg, out var phase) || phase == "copy";
    }

    private bool CopyFolder() {
        if (!Directory.Exists("Bootstrap")
            || !Directory.GetFiles(Path.Combine(Content.RootDir, "Doorstop")).Select(Path.GetFileName).All(File.Exists))
            return true;

        Directory.CreateDirectory(Path.Combine("Bootstrap", "asms"));
        Directory.CreateDirectory(Path.Combine("Bootstrap", "core"));
        Directory.CreateDirectory(Path.Combine("Bootstrap", "data"));
        Directory.CreateDirectory(Path.Combine("Bootstrap", "logs"));
        var dst = Directory.GetFiles(Path.Combine("Bootstrap", "core"));
        var src = Directory.GetFiles(Path.Combine(Content.RootDir, "Bootstrap", "core"));
        return !src.Select(Path.GetFileName).SequenceEqual(dst.Select(Path.GetFileName))
               || src.Any(srcFile => File.ReadAllBytes(srcFile).ToSHA256Hex()
                                     != File.ReadAllBytes(
                                                Path.Combine(
                                                    "Bootstrap",
                                                    "core",
                                                    Path.GetFileName(srcFile)))
                                            .ToSHA256Hex())
               || !Directory.GetFiles(Path.Combine(Content.RootDir, "Doorstop")).Select(Path.GetFileName)
                            .All(File.Exists)
               || Directory.GetFiles(Path.Combine(Content.RootDir, "Doorstop")).Any(srcFile =>
                   File.ReadAllBytes(srcFile).ToSHA256Hex()
                   != File.ReadAllBytes(Path.GetFileName(srcFile)).ToSHA256Hex());
    }
}

public class UIRoot_RimworldBootstrap(string? phase) : UIRoot {
    public override void UIRootOnGUI() {
        base.UIRootOnGUI();
        Widgets.Label(
            new Rect(500, 500, 500, Text.LineHeight * 2),
            "RimWorldBootstrap.Error".Translate());
        if (Widgets.ButtonText(new Rect(500, 500 + Text.LineHeight * 2, 100, Text.LineHeight * 2), "OK", true, false))
            Environment.Exit(1);
    }
}

internal static class CommandlineUtil {
    private static int s_starting;

    public static void Restart(string rootDir, string? phase) {
        if (Interlocked.CompareExchange(ref s_starting, 1, 0) == 1) return;
        try {
            var commandLineArgs = Environment.GetCommandLineArgs();
            var fileName = commandLineArgs[0];
            var bootstrapRestartTime =
                GenCommandLine.TryGetCommandLineArg(RimWorldBootstrapMod.RestartArg, out var time)
                    ? int.Parse(time)
                    : 0;
            if (bootstrapRestartTime > 5) phase = "error";
            var arguments =
                $"{string.Join(" ", commandLineArgs[1..].Where(x => !x.StartsWith("-bootstrap=")).Where(x => !x.StartsWith("-bootstrap_restart_time=")))} {(phase == null ? "" : $"-{RimWorldBootstrapMod.Arg}={phase}")} -{RimWorldBootstrapMod.RestartArg}={bootstrapRestartTime}";
            var pid = Process.GetCurrentProcess().Id;
            var info = UnityData.platform == RuntimePlatform.WindowsPlayer
                ? GetPowershellInfo(rootDir, fileName, arguments, pid)
                : GetBashInfo(rootDir, fileName, arguments, pid);
            info.Environment.Clear();
            info.Environment.AddRange(BootstrapData.InitEnvs);

            var process = new Process { StartInfo = info };
            if (!process.Start()) {
                Log.Message("Cannot start process");
            }


            Root.Shutdown();
            Thread.Sleep(1000);
            Environment.Exit(0);
        } catch (Exception ex) {
            Log.Error("Error restarting: " + ex);
        } finally {
            Environment.Exit(0);
        }
    }

    private static ProcessStartInfo GetPowershellInfo(string rootDir, string fileName, string arguments, int pid) {
        var execute =
            $"Copy-Item -Path '{Path.Combine(rootDir, "Bootstrap")}' -Destination '.' -Recurse -Force ; "
            + $"Copy-Item -Path '{Path.Combine(rootDir, "Doorstop", "*")}' -Destination '.' -Recurse -Force ;"
            + $"& '{fileName}' {arguments}";
        var monitor = $"while (Get-Process -Id {pid} -ErrorAction SilentlyContinue) {{ Start-Sleep -Seconds 0.5 }}";
        var script = $"-ExecutionPolicy Bypass -Command \" {monitor} ; {execute}\"";
        return new ProcessStartInfo { FileName = "powershell.exe", Arguments = script, UseShellExecute = false };
    }

    private static ProcessStartInfo GetBashInfo(string rootDir, string fileName, string arguments, int pid) {
        var execute = $"cp -r \"{Path.Combine(rootDir, "Bootstrap", "*")}\" Bootstrap && "
                      + $"cp -r \"{Path.Combine(rootDir, "Doorstop", "*")}\" . && "
                      + $"cp \"{Path.Combine(rootDir, "Doorstop", ".*")}\" . &&"
                      + $"\"{fileName}\" {arguments}";
        var monitor = $"while kill -0 {pid} 2>/dev/null; do sleep 0.5; done";
        var script = $"-c '{monitor}; {execute}";
        return new ProcessStartInfo { FileName = "bash", Arguments = script, UseShellExecute = false };
    }
}