using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nuntiator.Analyzers;

/// <summary>
/// Roslyn Analyzer that verifies types passed to AddMiddleware, AddOpenMiddleware, or AddOpenBehavior
/// implement ICommandMiddleware&lt;,&gt; or IPipelineBehavior&lt;,&gt;. (NUNT002)
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvalidMiddlewareTypeAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableString Title = "Invalid middleware type";
    private static readonly LocalizableString MessageFormat = "Type '{0}' passed to '{1}' does not implement 'ICommandMiddleware<,>' or 'IPipelineBehavior<,>'";
    private static readonly LocalizableString Description = "Types registered as Nuntiator middleware must implement ICommandMiddleware<TCommand, TResponse> or IPipelineBehavior<TCommand, TResponse>.";
    private const string Category = "Usage";

    public static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.InvalidMiddlewareType,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        var containingType = methodSymbol.ContainingType;
        if (containingType == null || containingType.Name != "NuntiatorConfiguration")
        {
            return;
        }

        var methodName = methodSymbol.Name;
        if (methodName != "AddMiddleware" &&
            methodName != "AddOpenMiddleware" &&
            methodName != "AddOpenBehavior")
        {
            return;
        }

        ITypeSymbol? candidateType = null;
        Location? diagnosticLocation = null;

        // Case 1: Generic AddMiddleware<T>()
        if (methodSymbol.IsGenericMethod && methodSymbol.TypeArguments.Length == 1)
        {
            candidateType = methodSymbol.TypeArguments[0];
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name is GenericNameSyntax genericName &&
                genericName.TypeArgumentList.Arguments.Count > 0)
            {
                diagnosticLocation = genericName.TypeArgumentList.Arguments[0].GetLocation();
            }
            else
            {
                diagnosticLocation = invocation.GetLocation();
            }
        }
        // Case 2: Non-generic AddMiddleware(typeof(T)) / AddOpenMiddleware(typeof(T)) / AddOpenBehavior(typeof(T))
        else if (invocation.ArgumentList.Arguments.Count >= 1)
        {
            var firstArg = invocation.ArgumentList.Arguments[0].Expression;
            if (firstArg is TypeOfExpressionSyntax typeOfExpression)
            {
                var typeInfo = context.SemanticModel.GetTypeInfo(typeOfExpression.Type, context.CancellationToken);
                candidateType = typeInfo.Type ?? context.SemanticModel.GetSymbolInfo(typeOfExpression.Type, context.CancellationToken).Symbol as ITypeSymbol;
                diagnosticLocation = typeOfExpression.GetLocation();
            }
        }

        if (candidateType == null ||
            diagnosticLocation == null ||
            candidateType.TypeKind == TypeKind.Error ||
            candidateType.TypeKind == TypeKind.TypeParameter)
        {
            return;
        }

        if (!ImplementsMiddlewareInterface(candidateType))
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                diagnosticLocation,
                candidateType.ToDisplayString(),
                methodName);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool ImplementsMiddlewareInterface(ITypeSymbol typeSymbol)
    {
        var targetType = typeSymbol.OriginalDefinition;

        if (IsMiddlewareInterface(targetType))
        {
            return true;
        }

        foreach (var iface in targetType.AllInterfaces)
        {
            if (IsMiddlewareInterface(iface))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMiddlewareInterface(ITypeSymbol typeSymbol)
    {
        var originalDef = typeSymbol.OriginalDefinition;
        var name = originalDef.MetadataName;
        var containingNamespace = originalDef.ContainingNamespace?.ToDisplayString();

        if (containingNamespace != "Nuntiator")
        {
            return false;
        }

        return name == "ICommandMiddleware`2" || name == "IPipelineBehavior`2";
    }
}
