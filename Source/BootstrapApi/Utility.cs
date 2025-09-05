using System.Reflection;

using BootstrapApi.Logger;

using Microsoft.Extensions.Logging;

using Mono.Cecil;
using Mono.Cecil.Cil;

using MonoMod.Utils;

namespace Bootstrap;

public static class Utility {
    private static readonly char[] HexChars = "0123456789abcdef".ToCharArray();
    private static readonly char[] UpperHexChars = "0123456789ABCDEF".ToCharArray();

    public static string ToHexString(this byte[]? bytes, bool upperCase = false) {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length == 0) return string.Empty;
        var chars = upperCase ? UpperHexChars : HexChars;
        var result = new char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++) {
            var b = bytes[i];
            result[i * 2] = chars[b >> 4];
            result[i * 2 + 1] = chars[b & 0x0F];
        }

        return new string(result);
    }

    public static byte[] GetFileData(this Assembly asm) {
        return asm.IsDynamic ? [] : File.ReadAllBytes(asm.Location);
    }

    public static byte[] GetRawBytes(this AssemblyDefinition asm) {
        using var stream = new MemoryStream();
        asm.Write(stream);
        return stream.ToArray();
    }

    public static T? Get<T>(this CustomAttribute attribute, string name) {
        return (T)attribute.Fields
                           .FirstOrDefault(x => x.Name == name).Argument.Value;
    }

    public static T? GetConstructor<T>(this CustomAttribute attribute, int index) {
        var result = attribute.ConstructorArguments[index].Value;
        if (result is CustomAttributeArgument argument) return (T)argument.Value;
        return (T)result;
    }
    
    internal static void InsertBefore(
        MethodDefinition method, Func<Instruction, bool> predicate, List<Instruction> toInsert) {
        var instructions = method.Body.Instructions;
        var il = method.Body.GetILProcessor();
        var toInsertNew = toInsert.ToList();
        var first = toInsertNew[0];
        toInsertNew.RemoveAt(0);
        var i = 0;
        while (i < instructions.Count) {
            var elem = instructions[i++];
            if (!predicate(elem)) continue;
            var (opcode, operand) = (elem.OpCode, elem.Operand);
            (elem.OpCode, elem.Operand) = (first.OpCode, elem.Operand);
            var finalToInsert = toInsertNew.Concat([il.Create(opcode, operand)]).ToList();
            instructions.InsertRange(i, finalToInsert);
            i += finalToInsert.Count;
        }
    }
}