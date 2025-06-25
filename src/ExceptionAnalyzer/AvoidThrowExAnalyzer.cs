using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ExceptionAnalyzer
{
    /// <summary>
    /// Analyzer EX004: Use <c>throw;</c> instead of <c>throw ex;</c> to preserve the original stack trace.
    ///
    /// When rethrowing an exception using <c>throw ex;</c>, the stack trace is reset, which makes debugging much harder.
    /// The correct way to rethrow an exception is simply using <c>throw;</c>, which maintains the original call stack.
    ///
    /// <para>⚠️ Triggers on:</para>
    /// <code>
    /// catch (Exception ex)
    /// {
    ///     // Stack trace is lost
    ///     throw ex;
    /// }
    /// </code>
    ///
    /// <para>✅ Preferred usage:</para>
    /// <code>
    /// catch (Exception ex)
    /// {
    ///     // Preserves stack trace
    ///     throw;
    /// }
    /// </code>
    ///
    /// <para>Relevant best practice:</para>
    /// https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2200
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AvoidThrowExAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EX004";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Use 'throw;' instead of 'throw ex;'",
            "Use 'throw;' to preserve stack trace.",
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
            if (throwStmt.Expression is not IdentifierNameSyntax ident)
                return;

            // We are only looking for "throw ex;"
            var catchClause = throwStmt.FirstAncestorOrSelf<CatchClauseSyntax>();
            if (catchClause?.Declaration?.Identifier.ValueText != ident.Identifier.ValueText)
                return;

            var diagnostic = Diagnostic.Create(Rule, throwStmt.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
}
