using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nuntiator.Analyzers;

/// <summary>
/// Roslyn Analyzer that detects duplicate command handlers registered for the same command type in a compilation. (NUNT003)
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateCommandHandlerAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableString Title = "Duplicate command handler detected";
    private static readonly LocalizableString MessageFormat = "Multiple handlers found for command '{0}': '{1}'. Nuntiator expects a single handler per command.";
    private static readonly LocalizableString Description = "Nuntiator automatic dependency injection registers all discovered handlers. Having multiple handlers for the same command can lead to non-deterministic dispatch.";
    private const string Category = "Design";

    public static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.DuplicateCommandHandler,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        customTags: new[] { "CompilationEnd" });

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var handlerInterface2 = startContext.Compilation.GetTypeByMetadataName("Nuntiator.ICommandHandler`2");
            var handlerInterface1 = startContext.Compilation.GetTypeByMetadataName("Nuntiator.ICommandHandler`1");

            if (handlerInterface2 == null && handlerInterface1 == null)
            {
                return;
            }

            var handlersByCommand = new ConcurrentDictionary<ITypeSymbol, ConcurrentBag<(INamedTypeSymbol HandlerType, Location Location)>>(SymbolEqualityComparer.Default);

            startContext.RegisterSymbolAction(symbolContext =>
            {
                if (symbolContext.Symbol is not INamedTypeSymbol namedTypeSymbol)
                {
                    return;
                }

                if (namedTypeSymbol.TypeKind != TypeKind.Class ||
                    namedTypeSymbol.IsAbstract ||
                    namedTypeSymbol.IsGenericType)
                {
                    return;
                }

                foreach (var iface in namedTypeSymbol.AllInterfaces)
                {
                    if (iface.IsGenericType)
                    {
                        var originalDef = iface.OriginalDefinition;
                        if (SymbolEqualityComparer.Default.Equals(originalDef, handlerInterface2) ||
                            SymbolEqualityComparer.Default.Equals(originalDef, handlerInterface1))
                        {
                            var commandType = iface.TypeArguments[0];
                            var location = namedTypeSymbol.Locations.FirstOrDefault() ?? Location.None;
                            var bag = handlersByCommand.GetOrAdd(commandType, _ => new ConcurrentBag<(INamedTypeSymbol, Location)>());
                            bag.Add((namedTypeSymbol, location));
                        }
                    }
                }
            }, SymbolKind.NamedType);

            startContext.RegisterCompilationEndAction(endContext =>
            {
                foreach (var entry in handlersByCommand)
                {
                    var commandType = entry.Key;
                    var handlers = entry.Value.ToList();

                    var distinctHandlers = handlers
                        .GroupBy(h => h.HandlerType, SymbolEqualityComparer.Default)
                        .Select(g => g.First())
                        .ToList();

                    if (distinctHandlers.Count > 1)
                    {
                        var allHandlerNames = string.Join(", ", distinctHandlers.Select(h => h.HandlerType.Name).OrderBy(n => n));

                        foreach (var handler in distinctHandlers)
                        {
                            var diagnostic = Diagnostic.Create(
                                Rule,
                                handler.Location,
                                commandType.ToDisplayString(),
                                allHandlerNames);

                            endContext.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            });
        });
    }
}
