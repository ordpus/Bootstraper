using System.Reflection;

using Bootstrap;

using BootstrapApi.Logger;

using Microsoft.Extensions.Logging;

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

using MonoMod.Utils;

using ILogger = Microsoft.Extensions.Logging.ILogger;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodBody = Mono.Cecil.Cil.MethodBody;
using MethodImplAttributes = Mono.Cecil.MethodImplAttributes;

namespace BootstrapApi.Patcher;

public class ArrayComparator<T> : IEqualityComparer<T[]> where T : IEquatable<T> {
    public bool Equals(T[]? x, T[]? y) {
        return ReferenceEquals(x, y)
               || (x != null
                   && y != null
                   && x.Length == y.Length
                   && x.AsSpan().SequenceEqual(y.AsSpan()));
    }

    public int GetHashCode(T[]? array) {
        return array == null ? 0 : array.Aggregate(17, HashCode.Combine);
    }
}


public class Patcher {
    private const string ArgModule = "module";
    private const string ArgImportModules = "importModules";

    private static readonly ILogger Logger;
    private static readonly ILoggerProvider LogProvider;

    static Patcher() {
        LogProvider = LoggerProvider.Create("Bootstrap/logs/patcher.log", "Patcher".Length);
        Logger = LogProvider.CreateLogger("Patcher");
    }

    private static int s_patched;

    private const BindingFlags All = BindingFlags.Instance
                                     | BindingFlags.Static
                                     | BindingFlags.Public
                                     | BindingFlags.NonPublic
                                     | BindingFlags.GetField
                                     | BindingFlags.SetField
                                     | BindingFlags.GetProperty
                                     | BindingFlags.SetProperty;


    private readonly IReadOnlyDictionary<string, ModuleDefinition> _modules;

    public static void Patch() {
        if (Interlocked.CompareExchange(ref s_patched, 1, 0) == 1) return;
        Logger.LogInformation("Begin patch");
        try {
            new Patcher().DoAllPatches();
        } catch (Exception e) {
            Logger.LogError(e, "Error patch");
            throw;
        } finally {
            LogProvider.Dispose();
        }
    }

    public Patcher() {
        List<AssemblyDefinition> assemblies =
            AppDomain.CurrentDomain
                     .GetAssemblies()
                     .Where(x => !x.IsDynamic)
                     .ToDictionary(
                         x => x.GetFileData(),
                         x => x,
                         new ArrayComparator<byte>())
                     .Values
                     .Select(x => AssemblyDefinition.ReadAssembly(x.Location))
                     .ToList();
        _modules = assemblies.SelectMany(x => x.Modules)
                             .ToDictionary(x => x.Name);
    }

    private void DoAllPatches() {
        var addFieldModules = DoAddField();
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

    private List<ModuleDefinition> DoAddField() {
        return _modules.Values.SelectMany(x => x.Types)
                       .SelectMany(x => x.Methods)
                       .Where(x => x.HasCustomAttribute(typeof(AddFieldAttribute).FullName!))
                       .SelectMany(x => new AddFieldPatcher(Logger, x, _modules).Patch())
                       .ToList();
    }

    

    private ModuleDefinition? ExecuteFreePatch(MethodInfo method) {
        var attribute = method.GetCustomAttribute<FreePatchAttribute>();
        try {
            return (bool)method.Invoke(
                null,
                method.GetParameters().Select(x => GetPatchParameter(x.Name)).ToArray())
                ? _modules[attribute.Module]
                : null;
        } catch (Exception e) {
            Logger.LogError(e, "Free Patch {id} Error", attribute.ID);
            return null;
        }

        object? GetPatchParameter(string parameter) {
            return parameter switch {
                ArgModule => _modules[attribute.Module],
                ArgImportModules => attribute.ImportModules.Select(x => _modules[x]).ToList(),
                _ => null
            };
        }
    }

    private bool FreePatchMethodValidate(MethodInfo method) {
        return MethodValidate() && MethodParameterValidate() && PatchModuleValidate();

        bool MethodValidate() {
            var attribute = method.GetCustomAttribute<FreePatchAttribute>();
            if (!method.IsStatic) {
                Logger.LogError("Free Patch {id} is not static", attribute.ID);
                return false;
            }

            if (method.ReturnType != typeof(bool)) {
                Logger.LogError("Free Patch {id} return type is not bool", attribute.ID);
                return false;
            }

            return true;
        }

        bool MethodParameterValidate() {
            var parameters = method.GetParameters().ToList();
            var attribute = method.GetCustomAttribute<FreePatchAttribute>();
            if (parameters.Count is > 2 or 0) {
                Logger.LogError(
                    "Free Patch {id} has invalid parameters count {parametersCount}, expected 1 to 2",
                    attribute.ID,
                    parameters.Count);
                return false;
            }

            if (!parameters.Any(x => x.Name == ArgModule && x.ParameterType == typeof(ModuleDefinition))) {
                Logger.LogError("Free Patch {id} does not have parameter '{argModule}'", attribute.ID, ArgModule);
                return false;
            }

            parameters.RemoveAll(x =>
                x.Name == ArgModule
                || (x.Name == ArgImportModules && x.ParameterType == typeof(IEnumerable<ModuleDefinition>)));
            if (parameters.Count != 0) {
                Logger.LogError(
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
            if (_modules.ContainsKey(attribute.Module)) return true;
            Logger.LogWarning("Free Patch {id} not found module {module}", attribute.ID, attribute.Module);
            return false;
        }
    }
}