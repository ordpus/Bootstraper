using System.Reflection;

using BootstrapApi;

using Serilog;

namespace Entrypoint;

internal static class BootstrapEntrypoint {
    internal static void Start() {
        try {
            using var reader = new BinaryReader(
                new FileStream(BootstrapData.AssemblyDatFile, FileMode.Open, FileAccess.Read, FileShare.None));
            var length = reader.ReadInt32();
            for (int i = 0; i < length; i++) {
                var arrLength = reader.ReadInt32();
                var buffer = new byte[arrLength];
                var actLength = reader.Read(buffer, 0, arrLength);
                if (actLength != arrLength) throw new IndexOutOfRangeException("Reading Length not consistent!");
                var assembly = Assembly.Load(buffer);
                Log.Information(
                    "Loading Assembly: {}",
                    assembly.FullName);
                File.WriteAllBytes($"Bootstrap/asms/{assembly.GetName().Name}.dll", buffer);
            }

            Assembly.LoadFrom("Bootstrap/core/BootstrapApi.dll");
            Assembly.LoadFrom("Bootstrap/core/Bootstrap.dll");
        } catch (Exception e) {
            Log.Error(e, "");
        } finally {
            if (Directory.Exists(BootstrapData.AssemblyDatFile)) File.Delete(BootstrapData.AssemblyDatFile);
        }
    }
}