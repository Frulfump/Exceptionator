using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace ExceptionAnalyzer
{
    /// <summary>
    /// Analyzer EX014: Detects logging of only <c>ex.Message</c> instead of the full exception object.
    ///
    /// Logging just the <c>Message</c> property on an exception often hides the stack trace and other useful context,
    /// making debugging and monitoring harder. This analyzer flags logging statements that include <c>ex.Message</c>
    /// but omit passing the full exception object.
    ///
    /// <para>⚠️ Triggers on:</para>
    /// <code>
    /// catch (Exception ex)
    /// {
    ///     logger.LogError("Something failed: " + ex.Message);
    /// }
    /// </code>
    ///
    /// <para>✅ Preferred usage:</para>
    /// <code>
    /// catch (Exception ex)
    /// {
    ///     logger.LogError(ex, "Something failed");
    /// }
    /// </code>
    ///
    /// <para>Note:</para>
    /// This analyzer only triggers if <c>ex.Message</c> is logged without also including <c>ex</c> as a separate argument.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class LogOnlyExceptionMessageAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EX014";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Avoid logging only ex.Message",
            "Log the entire exception to preserve stack trace and context.",
            "ExceptionHandling",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeCatchBlock, SyntaxKind.CatchClause);
        }

        private void AnalyzeCatchBlock(SyntaxNodeAnalysisContext context)
        {
            var catchClause = (CatchClauseSyntax)context.Node;
            if (catchClause.Declaration is not { Identifier.Text: var exceptionIdentifier })
                return;

            var block = catchClause.Block;
            if (block == null)
                return;

            var invocationExpressions = block.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocationExpressions)
            {
                var arguments = invocation.ArgumentList.Arguments;

                var hasExMessage = arguments.Any(arg =>
                    arg.Expression is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Expression is IdentifierNameSyntax id &&
                    id.Identifier.Text == exceptionIdentifier &&
                    memberAccess.Name.Identifier.Text == "Message");

                var hasFullException = arguments.Any(arg =>
                    arg.Expression is IdentifierNameSyntax id &&
                    id.Identifier.Text == exceptionIdentifier);

                if (hasExMessage && !hasFullException)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
                }
            }
        }
    }
}
