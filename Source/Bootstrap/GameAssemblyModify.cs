using System.Reflection;

using BootstrapApi.Logger;

using Microsoft.Extensions.Logging;

using Mono.Cecil;
using Mono.Cecil.Cil;

using MonoMod.Utils;

namespace Bootstrap;

internal class GameAssemblyModifier {

    private readonly AssemblyDefinition _assembly;
    private readonly ModuleDefinition _module;

    internal GameAssemblyModifier(string managedPath) {
        _assembly = AssemblyDefinition.ReadAssembly(Path.Combine(managedPath, "Assembly-CSharp.dll"));
        _module = _assembly.MainModule;
    }

    internal byte[] Modify() {
        ModifyLogType();
        var result = new MemoryStream();
        _assembly.Write(result);
        return result.ToArray();
    }

    private void ModifyLogType() {
        var type = _module.GetType("Verse.Log");
        var voidMethods = type.Methods.Where(x => x.ReturnType == _module.TypeSystem.Void).ToList();
        foreach (var method in voidMethods) {
            ClearBody(method);
            method.Body.Instructions.Add(method.Body.GetILProcessor().Create(OpCodes.Ret));
        }

        foreach (var method in voidMethods.Where(x => x.Parameters.Count == 1)
                                          .Where(x => x.Parameters[0].ParameterType == _module.TypeSystem.String
                                                      || x.Parameters[0].ParameterType == _module.TypeSystem.Object)) {
            CommonLog(method, method.Name);
        }

        foreach (var method in voidMethods.Where(x => x.Name.EndsWith("Once"))) {
            CommonLog(method, method.Name[..^4]);
        }

        return;

        void CommonLog(MethodDefinition method, string methodName) {
            ClearBody(method);
            var il = method.Body.GetILProcessor();
            method.Body.Instructions.AddRange(
            [
                il.Create(OpCodes.Ldarg_0),
                il.Create(OpCodes.Call, typeof(ModifiedLog).GetMethod(methodName)!),
                il.Create(OpCodes.Ret)
            ]);
        }
    }

    private void ModifyUnityData() {
        
        
    }

    private static void ClearBody(MethodDefinition method) {
        method.Body.Instructions.Clear();
        method.Body.Variables.Clear();
        method.Body.ExceptionHandlers.Clear();
        method.Body.MaxStackSize = 0;
    }
}

internal static class ModifiedLog {
    private static readonly ILogger Logger = BootstrapLog.CreateLogger("Game");

    // ReSharper disable once UnusedMember.Global
    public static void Message(string text) {
        Logger.LogInformation("{}", text);
    }
    // ReSharper disable once UnusedMember.Global
    public static void Warning(string text) {
        Logger.LogWarning("{}", text);
    }
    
    // ReSharper disable once UnusedMember.Global
    public static void Error(string text) {
        Logger.LogError("{}", text);
    }
}