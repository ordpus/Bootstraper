using System.Reflection;
using System.Runtime.CompilerServices;

using Bootstrap;

using BootstrapApi.Logger;

namespace Entrypoint;

[IsExternalInit]
[Serializable]
public record ModifiedAssembly(string name, string sha256, byte[] assembly);

internal static class BootstrapEntrypoint {
    
    internal static void Start() {
        try {
            Assembly.LoadFrom("Bootstrap/core/BootstrapApi.dll");
            if (BootstrapData.Instance == null) return;
            var loaderDomainSetup = new AppDomainSetup {
                PrivateBinPath = "Bootstrap/core;RimWorldWin64_Data/Managed"
            };
            var loaderDomain = AppDomain.CreateDomain("Loader", null, loaderDomainSetup);
            var executor = (LoaderExecutor)loaderDomain.CreateInstanceAndUnwrap(
                typeof(LoaderExecutor).Assembly.FullName,
                typeof(LoaderExecutor).FullName!);
            var result = executor.Execute(executor.PreExecute());
            AppDomain.Unload(loaderDomain);
            BootstrapLog.LogInformation("Unload");
        } catch (Exception e) {
            BootstrapLog.ErrorLogger.WriteLine(e.ToString());
        }
    }
}

internal class LoaderExecutor : MarshalByRefObject {

    public List<ModifiedAssembly> PreExecute() {
        var assembly = Assembly.LoadFile(Path.GetFullPath("Bootstrap/core/Bootstrap.dll"));
        return (List<ModifiedAssembly>)assembly.GetType("Bootstrap.Loader")
                                               .GetMethod(
                                                   "PreStart",
                                                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
                                               .Invoke(null, []);
    }
    
    public List<ModifiedAssembly> Execute(List<ModifiedAssembly> modified) {
        modified.ForEach(x => Assembly.Load(x.assembly));
        var assembly = Assembly.LoadFile(Path.GetFullPath("Bootstrap/core/Bootstrap.dll"));
        return (List<ModifiedAssembly>)assembly.GetType("Bootstrap.Loader")
                                               .GetMethod(
                                                   "Start",
                                                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
                                               .Invoke(null, []);
    }
}