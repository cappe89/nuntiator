using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nuntiator.Analyzers.Tests;

public static class AnalyzerTestHelper
{
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        string source,
        DiagnosticAnalyzer analyzer)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new HashSet<MetadataReference>();

        void AddAssemblyAndDependencies(Type type)
        {
            var loc = type.Assembly.Location;
            if (!string.IsNullOrEmpty(loc) && File.Exists(loc))
            {
                references.Add(MetadataReference.CreateFromFile(loc));
            }
        }

        AddAssemblyAndDependencies(typeof(object));
        AddAssemblyAndDependencies(typeof(Console));
        AddAssemblyAndDependencies(typeof(Enumerable));
        AddAssemblyAndDependencies(typeof(ICommand<>));
        AddAssemblyAndDependencies(typeof(Microsoft.Extensions.DependencyInjection.ServiceCollectionExtensions));
        AddAssemblyAndDependencies(typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection));

        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var runtimeDll = Path.Combine(runtimeDir, "System.Runtime.dll");
        if (File.Exists(runtimeDll))
        {
            references.Add(MetadataReference.CreateFromFile(runtimeDll));
        }

        var netstandardDll = Path.Combine(runtimeDir, "netstandard.dll");
        if (File.Exists(netstandardDll))
        {
            references.Add(MetadataReference.CreateFromFile(netstandardDll));
        }

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly_" + Guid.NewGuid().ToString("N"),
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create(analyzer));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}
