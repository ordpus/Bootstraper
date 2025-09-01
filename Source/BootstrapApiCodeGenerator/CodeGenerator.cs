using Microsoft.CodeAnalysis;

namespace BootstrapCodeGenerator;

[Generator]
public class LoggerMethodGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var compilation = context.CompilationProvider;
        context.RegisterSourceOutput(compilation, (x, y) => GenerateCode(x, y));
    }

    private static void GenerateCode(
        in SourceProductionContext context, Compilation compilation) {
        var baseLoggerType =
            compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.LoggerExtensions")
            ?? throw new InvalidOperationException();
        var methods = baseLoggerType.GetMembers().Where(x => x is IMethodSymbol).Cast<IMethodSymbol>()
                                    .Where(x => x.IsStatic)
                                    .Where(x => x.MethodKind is not (MethodKind.PropertyGet
                                        or MethodKind.PropertySet
                                        or MethodKind.EventAdd
                                        or MethodKind.EventRemove
                                        or MethodKind.StaticConstructor))
                                    .Where(x => x.Parameters.First().Name.Equals("logger"))
                                    .Where(x => x.DeclaredAccessibility == Accessibility.Public)
                                    .Select(GenerateMethod);
        context.AddSource(
            "LoggerMethodGenerator.g.cs",
            $$"""
              #nullable enable
              using Microsoft.Extensions.Logging;
              namespace BootstrapApi.Logger;
              public partial class BootstrapLog {
              {{string.Join("\n\n", methods)}}
              }
              """);
    }

    private static string GenerateParameters(IEnumerable<IParameterSymbol> parameters) {
        return string.Join(
            ", ",
            parameters.Skip(1).Select(y => $"{(y.IsParams ? "params " : "")}{y.Type} {y.Name}"));
    }

    private static string GenerateMethod(IMethodSymbol method) {
        return $$"""
                 public static {{method.ReturnType}} {{method.Name}}({{GenerateParameters(method.Parameters)}}) {
                    {{(method.ReturnsVoid ? "" : "return ")}}DefaultLogger.{{method.Name}}({{string.Join(", ", method.Parameters.Select(y => y.Name).Skip(1))}});
                 }
                 """;
    }
}