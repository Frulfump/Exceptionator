using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ExceptionAnalyzer
{
    /// <summary>
    /// Analyzer EX007: Detects try/catch blocks that serve no meaningful purpose,
    /// such as try blocks that only contain a <c>throw</c> or catch blocks that rethrow the exception without any added logic.
    ///
    /// These constructs add noise to the code and should be removed unless a specific handling intention is present.
    ///
    /// <para>⚠️ Triggers on:</para>
    /// <code>
    /// try
    /// {
    ///     throw;
    /// }
    /// catch
    /// {
    ///     throw;
    /// }
    /// </code>
    ///
    /// <para>✅ Preferred usage:</para>
    /// Remove the try/catch block entirely if no additional processing, logging, or recovery is performed.
    ///
    /// <para>Note:</para>
    /// The analyzer does not flag try/catch blocks that include logging, conditional logic, or alternative flow.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PointlessTryCatchAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EX007";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Pointless try/catch block",
            "This try/catch block doesn't add any handling or logic.",
            "CodeQuality",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(AnalyzeTryCatch, SyntaxKind.TryStatement);
        }

        private void AnalyzeTryCatch(SyntaxNodeAnalysisContext context)
        {
            var tryStmt = (TryStatementSyntax)context.Node;

            // Example: try only containing a throw
            if (tryStmt.Block?.Statements.Count == 1 &&
                tryStmt.Block.Statements[0] is ThrowStatementSyntax &&
                tryStmt.Catches.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, tryStmt.GetLocation()));
                return;
            }

            foreach (var catchClause in tryStmt.Catches)
            {
                var block = catchClause.Block;
                if (block != null && block.Statements.Count == 1 && block.Statements[0] is ThrowStatementSyntax)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, catchClause.GetLocation()));
                }
            }
        }
    }
}
