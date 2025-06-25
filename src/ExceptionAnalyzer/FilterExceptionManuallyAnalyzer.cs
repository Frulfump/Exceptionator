using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace ExceptionAnalyzer
{
    /// <summary>
    /// Analyzer EX018: Detects when exception type filtering is done using `if (ex is Type)` inside a catch block.
    /// Suggests using a catch with `when` filter or a specific exception type instead.
    ///
    /// ❌ Bad:
    /// catch (Exception ex)
    /// {
    ///     if (ex is IOException) { ... }
    /// }
    ///
    /// ✅ Good:
    /// catch (IOException ex) { ... }
    ///
    /// ✅ Also good:
    /// catch (Exception ex) when (ex is IOException) { ... }
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FilterExceptionManuallyAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EX018";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Filter exceptions using catch when or specific types",
            "Use exception filters or catch specific exception types instead of 'if (ex is ...)'.",
            "ExceptionHandling",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeCatchClause, SyntaxKind.CatchClause);
        }

        private void AnalyzeCatchClause(SyntaxNodeAnalysisContext context)
        {
            var catchClause = (CatchClauseSyntax)context.Node;
            if (catchClause.Block == null || catchClause.Declaration == null || catchClause.Declaration.Type == null)
                return;

            // Already using a when-filter? No problem.
            if (catchClause.Filter != null)
                return;

            var exceptionIdentifier = catchClause.Declaration.Identifier.ValueText;
            if (string.IsNullOrWhiteSpace(exceptionIdentifier))
                return;

            // Look for: if (ex is SomeException)
            var ifs = catchClause.Block.DescendantNodes().OfType<IfStatementSyntax>();
            foreach (var ifStmt in ifs)
            {
                if (ifStmt.Condition is BinaryExpressionSyntax binary &&
                    binary.IsKind(SyntaxKind.IsExpression) &&
                    binary.Left is IdentifierNameSyntax id &&
                    id.Identifier.Text == exceptionIdentifier)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, ifStmt.GetLocation()));
                    return;
                }
            }
        }
    }
}
