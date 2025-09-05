using System.Collections;

namespace Bootstrap;

public static class BootstrapData {
    public const string AssemblyDatFile = "Bootstrap/data/AssemblyData.bin";
    public static readonly Dictionary<string, string> InitEnvs = [];

    static BootstrapData() {
        var envs = Environment.GetEnvironmentVariables();
        foreach (DictionaryEntry entry in envs) {
            if (entry.Key is string key && !key.StartsWith("DOORSTOP"))
                InitEnvs[key] = (string)entry.Value;
        }
    }
}