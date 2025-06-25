using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace ExceptionAnalyzer
{
    /// <summary>
    /// Analyzer EX003: Detects rethrown exceptions inside a catch block that fail to include the caught exception as an inner exception.
    ///
    /// When wrapping an exception, it’s best practice to include the original exception as the <c>innerException</c> argument
    /// to preserve the original call stack and diagnostic context.
    ///
    /// <para>⚠️ Triggers on:</para>
    /// <code>
    /// catch (Exception ex)
    /// {
    ///     throw new CustomException("Something went wrong");
    /// }
    /// </code>
    ///
    /// <para>✅ Preferred usage:</para>
    /// <code>
    /// catch (Exception ex)
    /// {
    ///     throw new CustomException("Something went wrong", ex);
    /// }
    /// </code>
    ///
    /// <para>Note:</para>
    /// This analyzer matches the identifier used in the catch clause and ensures it’s passed as an argument
    /// to the new exception constructor.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MissingInnerExceptionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EX003";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Missing inner exception",
            "New exception should include the caught exception as inner exception.",
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
            var objCreation = throwStmt.Expression as ObjectCreationExpressionSyntax;
            if (objCreation == null)
                return;

            // Are we in a catch-block with an exception-symbol?
            var catchClause = throwStmt.FirstAncestorOrSelf<CatchClauseSyntax>();
            if (catchClause?.Declaration?.Identifier.ValueText is not string caughtVar)
                return;

            // If inner exception is already used as an argument, everything is ok
            if (objCreation.ArgumentList?.Arguments.Any(arg =>
                arg.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == caughtVar) == true)
            {
                return;
            }

            var diagnostic = Diagnostic.Create(Rule, objCreation.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
}
