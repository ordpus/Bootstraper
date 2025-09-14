using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using Mono.Cecil;
using Mono.Cecil.Cil;

using MonoMod.Utils;

using Serilog;

namespace BootstrapApi;

public static class BootstrapUtility {
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

    public static void InsertBefore(
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

    public static string ToSHA256Hex(this byte[] bytes) {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(bytes).ToHexString();
    }

    public static void RollLogFile(string logFile, int maxCount = 5) {
        if (!File.Exists(logFile)) return;

        var logFilePath = Path.GetDirectoryName(logFile)!;
        var logFileName = Path.GetFileNameWithoutExtension(logFile)!;
        var logFileExt = Path.GetExtension(logFile)!.Substring(1);
        var timestamp = DateTime.Now.ToString(TimestampPattern());
        File.Move(logFile, Path.Combine(logFilePath, $"{logFileName}-{timestamp}.{logFileExt}"));
        Directory.EnumerateFiles(logFilePath)
                 .Where(x => Path.GetFileName(x).StartsWith(logFileName))
                 .Where(x => Regex.IsMatch(
                     Path.GetFileName(x),
                     $"^{logFileName}-[0-9]{{{TimestampPattern().Length}}}\\.{logFileExt}$"))
                 .OrderByDescending(x =>
                     long.Parse(Regex.Match(x, $"(?<={logFileName}-)[0-9]{{{TimestampPattern().Length}}}").Value))
                 .Skip(maxCount)
                 .ToList()
                 .ForEach(File.Delete);
        return;

        static string TimestampPattern() {
            return "yyyyMMddHHmmss";
        }
    }

    public static bool TryGetCommandLineArg(string key, out string value) {
        var result = Environment.GetCommandLineArgs()
                                .Where(x => x.StartsWith($"-{key}="))
                                .Select(x => x.Split('='))
                                .Where(x => x.Length == 2)
                                .Select(x => x[1])
                                .FirstOrDefault();
        value = result!;
        return result != null;
    }

    public static bool ShouldIntercept() {
        return TryGetCommandLineArg("bootstrap", out var bootstrap) && bootstrap == "bootstrap";
    }

    public static string FullNameWithoutGeneric(this TypeReference type) {
        var fullName = type.FullName;
        var genericMarkerIndex = fullName.IndexOf('<');
        return genericMarkerIndex != -1 ? fullName.Substring(0, genericMarkerIndex) : fullName;
    }

    public static void Bfs<T>(
        T root, Func<T, IEnumerable<T>> getChildren, Func<T, bool> action, IEqualityComparer<T>? comparer = null) =>
        Bfs([root], getChildren, action);

    public static void Bfs<T>(
        IEnumerable<T> roots, Func<T, IEnumerable<T>> getChildren, Func<T, bool> action,
        IEqualityComparer<T>? comparer = null) {
        if (roots == null) throw new ArgumentNullException(nameof(roots));
        if (getChildren == null) throw new ArgumentNullException(nameof(getChildren));
        if (action == null) throw new ArgumentNullException(nameof(action));
        roots = roots.ToList();
        if (roots.Any(x => x == null)) throw new ArgumentException("roots contains null");
        var queue = new Queue<T>(roots);
        var visited = new HashSet<T>(comparer);
        while (queue.Count > 0) {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;
            if (action(current)) return;
            foreach (var child in getChildren(current) ?? []) queue.Enqueue(child);
        }
    }
}