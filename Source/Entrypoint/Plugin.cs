using System.Reflection;
using System.Runtime.CompilerServices;

using BootstrapApi;

using Entrypoint;

using Serilog;

// ReSharper disable once CheckNamespace
namespace Doorstop;

public static class Entrypoint {
    public static void Start() {
        RuntimeHelpers.RunClassConstructor(typeof(BootstrapData).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(SeriaLogger).TypeHandle);
        Log.Information("[{args}]", Environment.GetCommandLineArgs());
        if (!BootstrapUtility.ShouldIntercept()) {
            Assembly.LoadFrom("Bootstrap/core/BootstrapApi.dll");
            Assembly.LoadFrom("Bootstrap/core/Bootstrap.dll");
            return;
        }
        Log.Information("Is Intercept");
        BootstrapEntrypoint.Start();
    }
}