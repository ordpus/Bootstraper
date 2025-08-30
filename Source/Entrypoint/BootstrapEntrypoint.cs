using System.Reflection;
using System.Runtime.CompilerServices;

using BootstrapApi.Logger;

using Microsoft.Extensions.Logging;

namespace Entrypoint;

[IsExternalInit]
[Serializable]
public record ModifiedAssembly(string sha256, byte[] assembly);

internal static class BootstrapEntrypoint {
    internal static void Start() {
        try {
            File.WriteAllLines("Bootstrap/bootstrap1.log", ["Start!"]);
            Assembly.LoadFrom("Bootstrap/core/BootstrapApi.dll");
            var loaderDomainSetup = new AppDomainSetup {
                PrivateBinPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Bootstrap/core")
            };
            var loaderDomain = AppDomain.CreateDomain("Loader", null, loaderDomainSetup);
            var executor = (LoaderExecutor)loaderDomain.CreateInstanceAndUnwrap(
                typeof(LoaderExecutor).Assembly.FullName,
                typeof(LoaderExecutor).FullName!);
            var result = executor.Execute();
            BootstrapLog.Logger.LogInformation("sha256: {}", result[0].sha256);
        } catch (Exception e) {
            BootstrapLog.Logger.LogError(e, "123");
        }
    }
}

internal class LoaderExecutor : MarshalByRefObject {
    public List<ModifiedAssembly> Execute() {
        var assembly = Assembly.LoadFrom("Bootstrap/core/Bootstrap.dll");
        return (List<ModifiedAssembly>)assembly.GetType("Bootstrap.Loader")
                                               .GetMethod(
                                                   "Start",
                                                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
                                               .Invoke(null, []);
    }
}