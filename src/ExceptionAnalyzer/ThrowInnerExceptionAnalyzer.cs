using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace ExceptionAnalyzer
{
    /// <summary>
    /// Analyzer EX013: Detects throw statements that throw <c>ex.InnerException</c>
    /// directly from within a catch block.
    ///
    /// Throwing <c>ex.InnerException</c> will lose the current stack trace and may result
    /// in a null reference exception if the inner exception is null. It’s better to either
    /// rethrow the original exception using <c>throw;</c> or wrap the inner exception in a new one.
    ///
    /// <para>⚠️ Triggers on:</para>
    /// <code>
    /// catch (Exception ex)
    /// {
    ///     throw ex.InnerException;
    /// }
    /// </code>
    ///
    /// <para>✅ Preferred usage:</para>
    /// <code>
    /// catch (Exception ex)
    /// {
    ///     throw;
    /// }
    /// </code>
    /// or
    /// <code>
    /// catch (Exception ex)
    /// {
    ///     throw new CustomException("Something went wrong", ex.InnerException);
    /// }
    /// </code>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ThrowInnerExceptionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EX013";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Avoid throwing ex.InnerException",
            "Throwing ex.InnerException directly loses the original stack trace and may cause null reference exceptions.",
            "ExceptionHandling",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeThrowStatement, SyntaxKind.ThrowStatement);
        }

        private void AnalyzeThrowStatement(SyntaxNodeAnalysisContext context)
        {
            var throwStatement = (ThrowStatementSyntax)context.Node;
            if (throwStatement.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Name.Identifier.Text == "InnerException"
                && memberAccess.Expression is IdentifierNameSyntax exIdentifier)
            {
                // Try to verify that exIdentifier refers to a caught exception
                var enclosingCatch = throwStatement.FirstAncestorOrSelf<CatchClauseSyntax>();
                if (enclosingCatch?.Declaration?.Identifier.Text == exIdentifier.Identifier.Text)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, throwStatement.GetLocation()));
                }
            }
        }
    }
}
