using System.Reflection;
using System.Runtime.CompilerServices;

using Bootstrap;

using BootstrapApi.Logger;

using Entrypoint;

using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Doorstop;

public static class Entrypoint {
    public static void Start() {
        RuntimeHelpers.RunClassConstructor(typeof(BootstrapData).TypeHandle);
        BootstrapLog.DefaultLogger.LogInformation("[{}]", Environment.GetCommandLineArgs());
        if (!ShouldIntercept()) {
            Assembly.LoadFrom("Bootstrap/core/BootstrapApi.dll");
            return;
        }
        BootstrapLog.DefaultLogger.LogInformation("Is Intercept");
        BootstrapEntrypoint.Start();
    }

    private static bool ShouldIntercept() {
        return Environment.GetCommandLineArgs()
                          .Where(x => x.StartsWith("-bootstrap="))
                          .Select(x => x.Split('='))
                          .Where(x => x.Length == 2)
                          .Any(x => x[1] == "bootstrap");
    }
}