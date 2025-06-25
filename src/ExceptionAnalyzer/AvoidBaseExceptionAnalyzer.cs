using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace ExceptionAnalyzer
{
    /// <summary>
    /// Analyzer EX002: Avoid throwing base exceptions like <c>System.Exception</c> or <c>System.SystemException</c>.
    ///
    /// Throwing these base exception types is discouraged, as they provide little contextual information.
    /// More specific exception types should be used to convey intent and improve error handling.
    ///
    /// <para>⚠️ Triggers on:</para>
    /// <code>
    /// throw new System.Exception("Something went wrong");
    /// throw new System.SystemException("Unexpected error");
    /// </code>
    ///
    /// <para>✅ Preferred usage:</para>
    /// <code>
    /// throw new InvalidOperationException("Invalid state");
    /// throw new ArgumentNullException(nameof(arg));
    /// </code>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AvoidBaseExceptionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EX002";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Avoid throwing base exceptions",
            "Throwing '{0}' is discouraged. Use a more specific exception type.",
            "Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(AnalyzeThrow, SyntaxKind.ThrowStatement);
        }

        private void AnalyzeThrow(SyntaxNodeAnalysisContext context)
        {
            var throwStmt = (ThrowStatementSyntax)context.Node;
            if (throwStmt.Expression is not ObjectCreationExpressionSyntax objCreation)
                return;

            if (context.SemanticModel.GetSymbolInfo(objCreation.Type).Symbol is not INamedTypeSymbol type)
                return;

            var fullName = type.ToDisplayString();
            if (fullName == "System.Exception" || fullName == "System.SystemException")
            {
                var diagnostic = Diagnostic.Create(Rule, objCreation.GetLocation(), type.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
