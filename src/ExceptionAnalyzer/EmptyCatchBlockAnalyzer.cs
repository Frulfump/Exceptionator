using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace ExceptionAnalyzer
{
    /// <summary>
    /// Analyzer EX011: Detects empty <c>catch</c> blocks without any statements or comments.
    ///
    /// Swallowing exceptions silently makes debugging and maintenance difficult. An empty <c>catch</c> block may hide errors unless there's a clear reason for ignoring them.
    /// This analyzer raises a warning if a <c>catch</c> block is completely empty and lacks any explanatory comment.
    ///
    /// <para>⚠️ Triggers on:</para>
    /// <code>
    /// try
    /// {
    ///     DoSomething();
    /// }
    /// catch (Exception)
    /// {
    ///     // Nothing here
    /// }
    /// </code>
    ///
    /// <para>✅ Preferred usage:</para>
    /// <code>
    /// catch (Exception ex)
    /// {
    ///     LogError(ex);
    /// }
    ///
    /// catch (Exception)
    /// {
    ///     // Intentionally ignored because XYZ
    /// }
    /// </code>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EmptyCatchBlockAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EX011";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Empty catch block",
            "Empty catch block – consider rethrowing, logging or documenting why it's empty.",
            "ExceptionHandling",
            DiagnosticSeverity.Warning,
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

            // Check if catch block exists
            var block = catchClause.Block;
            if (block == null)
                return;

            // If there are statements, do not warn
            if (block.Statements.Count > 0)
                return;

            // If there's any comment (trivia), we assume it's intentional
            var hasComment = block.DescendantTrivia()
                .Any(trivia =>
                    trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                    trivia.IsKind(SyntaxKind.MultiLineCommentTrivia));

            if (!hasComment)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, catchClause.GetLocation()));
            }
        }
    }
}
