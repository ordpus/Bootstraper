using System.Reflection;

using UnityEngine;

using BootstrapApi.Logger;

using Entrypoint;

using Verse;

namespace Bootstrap;

internal static class Loader {

    internal static List<ModifiedAssembly> PreStart() {
        var managedPath = Path.GetFullPath("RimWorldWin64_Data/Managed");
        var assemblyModifier = new GameAssemblyModifier(managedPath);
        var bytes = assemblyModifier.Modify();
        return [new ModifiedAssembly("Assembly-CSharp", "null", bytes)];
    }
    
    internal static List<ModifiedAssembly> Start() {
        var managedPath = Path.GetFullPath("RimWorldWin64_Data/Managed");
        AppDomain.CurrentDomain.DomainUnload += (_, _) => BootstrapLog.Dispose();
        Directory.GetFiles(managedPath, "*.dll").ToList().ForEach(x => Assembly.LoadFile(x));
        BootstrapLog.LogInformation("Has type: {}", typeof(Root_Entry));
        // BootstrapLog.LogInformation($"Mods: {ModsConfig.ActiveModsInLoadOrder.Select(x => x.Name)}");
        return [];
    }
}