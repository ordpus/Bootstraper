using System.Text;

using Microsoft.CodeAnalysis;

namespace BootstrapApiCodeGenerator;

[Generator]
public class DefaultValueDirectAttributeCodeGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var compilation = context.CompilationProvider;
        context.RegisterSourceOutput(compilation, (x, _) => GenerateCode(x));
    }

    private static void GenerateCode(
        in SourceProductionContext context) {
//         var types = new List<string> {
//             "Bool",
//             "Byte",
//             "Short",
//             "Int",
//             "Long",
//             "SByte",
//             "UShort",
//             "UInt",
//             "ULong",
//             "Char",
//             "Float",
//             "Double",
//             "String"
//         };
//         var template = "public DefaultValueDirectAttribute({0} value) {{}}";
//         var result = new List<string>();
//         foreach (var value in types) {
//             result.Add(string.Format(template, value.ToLower()));
//         }
//         result.Add(string.Format(template, "Type"));
//
//         context.AddSource(
//             "DefaultValueDirectAttribute.g.cs",
//             $$"""
//               namespace BootstrapApi;
//               public partial class DefaultValueDirectAttribute {
//                 {{string.Join("\n\n", result)}}
//               }
//               """);
    }
}