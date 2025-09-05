using System.Buffers;
using System.Reflection;

using Bootstrap;

using BootstrapApi.Logger;

using Microsoft.Extensions.Logging;

namespace Entrypoint;

internal static class BootstrapEntrypoint {
    internal static void Start() {
        try {
            var managedPath = Environment.GetEnvironmentVariable("DOORSTOP_MANAGED_FOLDER_DIR");
            Assembly.LoadFrom($"{managedPath}/mscorlib.dll");
            using var reader = new BinaryReader(
                new FileStream(BootstrapData.AssemblyDatFile, FileMode.Open, FileAccess.Read, FileShare.None));
            var length = reader.ReadInt32();
            for (int i = 0; i < length; i++) {
                var arrLength = reader.ReadInt32();
                var buffer = new byte[arrLength];
                var actLength = reader.Read(buffer, 0, arrLength);
                if (actLength != arrLength) throw new IndexOutOfRangeException("Reading Length not consistent!");
                var assembly = Assembly.Load(buffer);
                BootstrapLog.DefaultLogger.LogInformation(
                    "Loading Assembly: {}",
                    assembly.FullName);
                File.WriteAllBytes($"Bootstrap/asms/{assembly.GetName().Name}.dll", buffer);
            }

            Assembly.LoadFrom("Bootstrap/core/BootstrapApi.dll");
        } catch (Exception e) {
            BootstrapLog.ErrorLogger.WriteLine(e.ToString());
        } finally {
            if (Directory.Exists(BootstrapData.AssemblyDatFile)) File.Delete(BootstrapData.AssemblyDatFile);
        }
    }
}