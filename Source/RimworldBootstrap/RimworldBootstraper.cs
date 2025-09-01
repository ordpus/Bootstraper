#nullable enable

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

using Bootstrap;

using RimWorld;

using UnityEngine;

using Verse;
using Verse.Sound;

namespace RimworldBootstrap;

public class RimworldBootstrapMod : Mod {
    public RimworldBootstrapMod(ModContentPack content) : base(content) {
        if (!ShouldRestart()) return;

        GetAllData().WriteData();
        PopupWindow();
    }

    private static UnityDataWrapper GetUnityData() {
        return new UnityDataWrapper(UnityData.dataPath, UnityData.platform.ToString(), UnityData.persistentDataPath);
    }

    private static Dictionary<string, string> GetDllIdentifiers() {
        return Directory.GetFiles(Path.Combine(UnityData.dataPath, "Managed"), "*.dll")
                        .ToDictionary(x => x, x => Sha256(File.ReadAllBytes(x)));
    }

    private static BootstrapData GetAllData() {
        return new BootstrapData(GetUnityData(), new ReadOnlyDictionary<string, string>(GetDllIdentifiers()));
    }

    private static string Sha256(byte[] bytes) {
        using var converter = SHA256.Create();
        return converter.ComputeHash(bytes).ToHexString();
    }

    private static bool ShouldRestart() {
        Log.Message("StringEquals: " + Equals(BootstrapData.Instance.ToStringSafe(), GetAllData().ToStringSafe()));
        Log.Message("Equals: " + Equals(GetAllData(), BootstrapData.Instance));
        return !Equals(BootstrapData.Instance, GetAllData());
    }

    private static void PopupWindow() {
        LongEventHandler.ClearQueuedEvents();
        LongEventHandler.toExecuteWhenFinished.Clear();

        LanguageDatabase.InitAllMetadata();

        KeyPrefs.data = new KeyPrefsData();
        foreach (var f in typeof(KeyBindingDefOf).GetFields()) f.SetValue(null, new KeyBindingDef());
        Current.Root.soundRoot = new SoundRoot();
        Current.Root.uiRoot = new UIRoot_RimworldBootstrap();
    }
}

public class UIRoot_RimworldBootstrap : UIRoot {
    public override void UIRootOnGUI() {
        base.UIRootOnGUI();
        if (Widgets.ButtonText(new Rect(500, 500, 500, Text.LineHeight), "OK", true, false)) GenCommandLine.Restart();
    }
}