using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace ExceptionAnalyzer
{
    /// <summary>
    /// Analyzer EX008: Detects catch blocks that handle <c>ThreadAbortException</c>
    /// without properly rethrowing the exception or calling <c>Thread.ResetAbort()</c>.
    ///
    /// Swallowing <c>ThreadAbortException</c> can lead to infinite loops or thread termination issues,
    /// especially in scenarios involving <c>while</c> loops or long-running tasks.
    ///
    /// <para>⚠️ Triggers on:</para>
    /// <code>
    /// try
    /// {
    ///     while (true)
    ///     {
    ///         // ...
    ///     }
    /// }
    /// catch (ThreadAbortException ex)
    /// {
    ///     // Swallowed without reset or rethrow
    /// }
    /// </code>
    ///
    /// <para>✅ Preferred usage:</para>
    /// <code>
    /// catch (ThreadAbortException ex)
    /// {
    ///     Thread.ResetAbort();
    /// }
    /// </code>
    /// or
    /// <code>
    /// catch (ThreadAbortException)
    /// {
    ///     throw;
    /// }
    /// </code>
    ///
    /// The problem in this analyzer is described here: https://andrewlock.net/creating-an-analyzer-to-detect-infinite-loops-caused-by-threadabortexception/
    /// The post uses a while loop as an example, but the same issue can occur in catch blocks for ThreadAbortException.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ThreadAbortExceptionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EX008";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "ThreadAbortException must not be swallowed",
            "ThreadAbortException should be rethrown or reset using Thread.ResetAbort().",
            "Correctness",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(AnalyzeCatchClause, SyntaxKind.CatchClause);
        }

        private void AnalyzeCatchClause(SyntaxNodeAnalysisContext context)
        {
            var catchClause = (CatchClauseSyntax)context.Node;
            if (catchClause.Declaration == null || catchClause.Block == null)
                return;

            var type = context.SemanticModel.GetTypeInfo(catchClause.Declaration.Type).Type;
            if (type == null || type.ToDisplayString() != "System.Threading.ThreadAbortException")
                return;

            var block = catchClause.Block;

            // Check for throw
            var hasRethrow = block.Statements.OfType<ThrowStatementSyntax>().Any(ts => ts.Expression == null);

            // Check for Thread.ResetAbort() anywhere in block
            var hasReset = block.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation =>
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    return symbol?.ToDisplayString() == "System.Threading.Thread.ResetAbort()";
                });

            if (!hasRethrow && !hasReset)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, catchClause.GetLocation()));
            }
        }
    }
}
