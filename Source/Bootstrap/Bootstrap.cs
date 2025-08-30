using Entrypoint;

namespace Bootstrap;

internal static class Loader {
    internal static List<ModifiedAssembly> Start() {
        return [new ModifiedAssembly(sha256: "12345", null)];
    }
}