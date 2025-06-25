using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace ExceptionAnalyzer
{
    /// <summary>
    /// Analyzer EX010: Warns when <c>Task.WaitAll</c> is used without catching <c>AggregateException</c>.
    ///
    /// Using <c>Task.WaitAll</c> without handling <c>AggregateException</c> may result in unobserved exceptions and loss of error details.
    /// It is generally safer to use <c>await Task.WhenAll</c> in asynchronous code or explicitly catch <c>AggregateException</c> in synchronous code.
    ///
    /// <para>⚠️ Triggers on:</para>
    /// <code>
    /// try
    /// {
    ///     Task.WaitAll(task1, task2);
    /// }
    /// catch (Exception ex)
    /// {
    ///     Console.WriteLine(ex); // Doesn't catch AggregateException properly
    /// }
    /// </code>
    ///
    /// <para>✅ Preferred usage (option 1 - async):</para>
    /// <code>
    /// await Task.WhenAll(task1, task2);
    /// </code>
    ///
    /// <para>✅ Preferred usage (option 2 - sync):</para>
    /// <code>
    /// try
    /// {
    ///     Task.WaitAll(task1, task2);
    /// }
    /// catch (AggregateException ex)
    /// {
    ///     foreach (var inner in ex.InnerExceptions)
    ///         Console.WriteLine(inner);
    /// }
    /// </code>
    ///
    /// <para>See also:</para>
    /// https://www.code4it.dev/csharptips/task-whenall-vs-task-waitall-error-handling/
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AvoidTaskWaitAllAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EX010";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Task.WaitAll should be wrapped with AggregateException catch",
            "Task.WaitAll should be used with caution – catch AggregateException or use await Task.WhenAll for proper exception handling.",
            "ExceptionHandling",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            var symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (symbol == null || symbol.Name != "WaitAll")
                return;

            if (symbol.ContainingType.ToDisplayString() != "System.Threading.Tasks.Task")
                return;

            var tryStatement = invocation.FirstAncestorOrSelf<TryStatementSyntax>();
            if (tryStatement == null || tryStatement.Catches.Count == 0)
                return;

            var catchesAggregate = tryStatement.Catches.Any(c =>
            {
                // catch { } – catches all exceptions including AggregateException
                if (c.Declaration == null)
                    return true;

                var type = context.SemanticModel.GetTypeInfo(c.Declaration.Type).Type;
                return type?.ToDisplayString() == "System.AggregateException";
            });

            if (!catchesAggregate)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
            }
        }
    }
}
