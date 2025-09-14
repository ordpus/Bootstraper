using System.Reflection;
using System.Runtime.CompilerServices;

using BootstrapApi;

using Mono.Cecil;

using Serilog;

namespace Bootstrap.Patcher;


public class Patcher {
    private const string ArgModule = "module";
    private const string ArgImportModules = "importModules";

    private static int s_patched;

    private const BindingFlags All = BindingFlags.Instance
                                     | BindingFlags.Static
                                     | BindingFlags.Public
                                     | BindingFlags.NonPublic
                                     | BindingFlags.GetField
                                     | BindingFlags.SetField
                                     | BindingFlags.GetProperty
                                     | BindingFlags.SetProperty;
    public static void Patch() {
        if (Interlocked.CompareExchange(ref s_patched, 1, 0) == 1) return;
        Log.Logger.Information("Begin patch");
        try {
            new Patcher().DoAllPatches();
        } catch (Exception e) {
            Log.Logger.Error(e, "Error patch");
            throw;
        }
    }

    private void DoAllPatches() {
        RuntimeHelpers.RunClassConstructor(typeof(AddFieldPatcher).TypeHandle);
        var addFieldModules = new AddFieldPatcher(BootstrapPluginManager.AddFieldPlugins).Patch();
        var freePatchModules = DoFreePatch();
        var result = freePatchModules.Concat(addFieldModules)
                                     .Distinct()
                                     .Select(x => x.Assembly)
                                     .Distinct()
                                     .Select(x => x.GetRawBytes())
                                     .ToList();
        using var writer = new BinaryWriter(
            new FileStream(
                BootstrapData.AssemblyDatFile,
                FileMode.OpenOrCreate,
                FileAccess.Write));
        writer.Write(result.Count);
        foreach (var bytes in result) {
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        writer.Flush();
    }

    private List<ModuleDefinition> DoFreePatch() {
        return AppDomain.CurrentDomain
                        .GetAssemblies()
                        .SelectMany(x => x.GetTypes())
                        .SelectMany(x => x.GetMethods(All))
                        .Where(x => Attribute.IsDefined(x, typeof(FreePatchAttribute), true))
                        .Where(FreePatchMethodValidate)
                        .Select(ExecuteFreePatch)
                        .Where(x => x != null)
                        .Cast<ModuleDefinition>()
                        .ToList();
    }

    

    private ModuleDefinition? ExecuteFreePatch(MethodInfo method) {
        var attribute = method.GetCustomAttribute<FreePatchAttribute>();
        try {
            return (bool)method.Invoke(
                null,
                method.GetParameters().Select(x => GetPatchParameter(x.Name)).ToArray())
                ? AssemblySet.Modules[attribute.Module]
                : null;
        } catch (Exception e) {
            Log.Logger.Error(e, "Free Patch {id} Error", attribute.ID);
            return null;
        }

        object? GetPatchParameter(string parameter) {
            return parameter switch {
                ArgModule => AssemblySet.Modules[attribute.Module],
                ArgImportModules => attribute.ImportModules.Select(x => AssemblySet.Modules[x]).ToList(),
                _ => null
            };
        }
    }

    private bool FreePatchMethodValidate(MethodInfo method) {
        return MethodValidate() && MethodParameterValidate() && PatchModuleValidate();

        bool MethodValidate() {
            var attribute = method.GetCustomAttribute<FreePatchAttribute>();
            if (!method.IsStatic) {
                Log.Logger.Error("Free Patch {id} is not static", attribute.ID);
                return false;
            }

            if (method.ReturnType != typeof(bool)) {
                Log.Logger.Error("Free Patch {id} return type is not bool", attribute.ID);
                return false;
            }

            return true;
        }

        bool MethodParameterValidate() {
            var parameters = method.GetParameters().ToList();
            var attribute = method.GetCustomAttribute<FreePatchAttribute>();
            if (parameters.Count is > 2 or 0) {
                Log.Logger.Error(
                    "Free Patch {id} has invalid parameters count {parametersCount}, expected 1 to 2",
                    attribute.ID,
                    parameters.Count);
                return false;
            }

            if (!parameters.Any(x => x.Name == ArgModule && x.ParameterType == typeof(ModuleDefinition))) {
                Log.Logger.Error("Free Patch {id} does not have parameter '{argModule}'", attribute.ID, ArgModule);
                return false;
            }

            parameters.RemoveAll(x =>
                x.Name == ArgModule
                || (x.Name == ArgImportModules && x.ParameterType == typeof(IEnumerable<ModuleDefinition>)));
            if (parameters.Count != 0) {
                Log.Logger.Error(
                    "Free Patch {id} has invalid parameters [{parameters}], expected optional: {type} {argImportModules}",
                    attribute.ID,
                    parameters.Select(x => x.Name),
                    typeof(IEnumerable<ModuleDefinition>).Name,
                    ArgImportModules);
                return false;
            }

            return true;
        }

        bool PatchModuleValidate() {
            var attribute = method.GetCustomAttribute<FreePatchAttribute>();
            if (AssemblySet.Modules.ContainsKey(attribute.Module)) return true;
            Log.Logger.Warning("Free Patch {id} not found module {module}", attribute.ID, attribute.Module);
            return false;
        }
    }
}