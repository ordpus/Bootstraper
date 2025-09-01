using Entrypoint;

// ReSharper disable once CheckNamespace
namespace Doorstop;

public static class Entrypoint {
    
    public static void Start() {
        File.WriteAllText("Bootstrap/logs/clean.lock", "");
        BootstrapEntrypoint.Start();
    }
}