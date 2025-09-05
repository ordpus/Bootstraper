#nullable enable
using Microsoft.Extensions.Logging;

using BootstrapApi.Logger;

using FieldAttributes = Mono.Cecil.FieldAttributes;

using System.Reflection;
using System.Linq;

using Mono.Cecil;

using BootstrapApi;

using Bootstrap;

using System;
using System.Diagnostics;
using System.Threading;

using BootstrapApi.Patcher;

using RimWorld;

using UnityEngine;

using Verse;
using Verse.Sound;

namespace RimWorldBootstrap;

// ReSharper disable once UnusedType.Global
// ReSharper disable once ClassNeverInstantiated.Global
public class RimworldBootstrapMod : Mod {
    public static readonly BindingFlags all = BindingFlags.Instance
                                              | BindingFlags.Static
                                              | BindingFlags.Public
                                              | BindingFlags.NonPublic
                                              | BindingFlags.GetField
                                              | BindingFlags.SetField
                                              | BindingFlags.GetProperty
                                              | BindingFlags.SetProperty;


    internal const string Arg = "bootstrap";

    public RimworldBootstrapMod(ModContentPack content) : base(content) {
        if (ShouldReload()) {
            Patcher.Patch();
            PopupWindow("bootstrap");
            // CommandlineUtil.Restart("bootstrap");
        }

        if (ShouldPopupWindow()) PopupWindow("test");
    }

    private static void PopupWindow(string phase) {
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
        return !GenCommandLine.TryGetCommandLineArg(Arg, out _);
    }
}

public class UIRoot_RimworldBootstrap(string phase) : UIRoot {
    public override void UIRootOnGUI() {
        base.UIRootOnGUI();
        if (Widgets.ButtonText(new Rect(500, 500, 500, Text.LineHeight * 2), "OK", true, false))
            CommandlineUtil.Restart(phase);
    }
}

internal static class CommandlineUtil {
    private static int s_starting;

    public static void Restart(string phase) {
        if (Interlocked.CompareExchange(ref s_starting, 1, 0) == 1) return;
        try {
            var commandLineArgs = Environment.GetCommandLineArgs();
            var fileName = commandLineArgs[0];
            var arguments = $"{string.Join(" ", commandLineArgs[1..])} -{RimworldBootstrapMod.Arg}={phase}";
            var pid = Process.GetCurrentProcess().Id;
            var info = UnityData.platform == RuntimePlatform.WindowsPlayer
                ? GetPowershellInfo(fileName, arguments, pid)
                : GetBashInfo(fileName, arguments, pid);
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

    private static ProcessStartInfo GetPowershellInfo(string fileName, string arguments, int pid) {
        var execute = $"& '{fileName}' {arguments}";
        var monitor = $"while (Get-Process -Id {pid} -ErrorAction SilentlyContinue) {{ Start-Sleep -Seconds 0.5 }}";
        var script = $"-ExecutionPolicy Bypass -Command \" {monitor} ; {execute}\"";
        return new ProcessStartInfo { FileName = "powershell.exe", Arguments = script, UseShellExecute = false };
    }

    private static ProcessStartInfo GetBashInfo(string fileName, string arguments, int pid) {
        var execute = $"\"{fileName}\" {arguments}";
        var monitor = $"while kill -0 {pid} 2>/dev/null; do sleep 0.5; done";
        var script = $"-c '{monitor}; {execute}";
        return new ProcessStartInfo { FileName = "bash", Arguments = script, UseShellExecute = false };
    }
}

internal class Test2 {
    public int a;
}

internal static class Test {
    [FreePatch("test", "Assembly-CSharp.dll", [])]
    internal static bool FreePatch(ModuleDefinition module) {
        Log.Message("Patching FreePatch");
        var type = module.GetType("RimWorld.Ability");
        type.Fields.Add(new FieldDefinition("myField", FieldAttributes.Public, module.TypeSystem.Boolean));
        Log.Message("Free Patch Complete");
        return true;
    }

    [AddField] [DefaultValueInjector("Inject2")] internal static extern ref double test(this Def instance);
    [AddField] [DefaultValueInjector("Inject")] internal static extern ref double test2(this Def instance);

    internal static double Inject() {
        return 1.0;
    }

    internal static double Inject2(Def def) {
        var b = def.label;
        return b?.Length ?? 12;
    }
}