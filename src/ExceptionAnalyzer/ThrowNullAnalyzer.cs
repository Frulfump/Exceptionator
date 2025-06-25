using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace ExceptionAnalyzer
{
    /// <summary>
    /// EX016: Detects and warns against the use of <c>throw null;</c>, which results in a <see cref="System.NullReferenceException"/>.
    ///
    /// ❌ Triggers on:
    /// <code>
    /// throw null;
    /// </code>
    ///
    /// ✅ Allowed examples:
    /// <code>
    /// throw new InvalidOperationException();
    /// throw; // re-throws the current exception
    /// </code>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ThrowNullAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EX016";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Don't throw null",
            "Avoid throwing null – use a specific exception type instead.",
            "ExceptionHandling",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(AnalyzeThrowStatement, SyntaxKind.ThrowStatement);
        }

        private void AnalyzeThrowStatement(SyntaxNodeAnalysisContext context)
        {
            var throwStmt = (ThrowStatementSyntax)context.Node;

            if (throwStmt.Expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.NullLiteralExpression))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, throwStmt.GetLocation()));
            }
        }
    }
}
