using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace ExceptionAnalyzer
{
    /// <summary>
    /// EX017 - Detects catch clauses with a 'when' filter that always evaluates to true.
    ///
    /// This is often redundant and suggests the filter can be removed:
    ///
    /// ❌ Bad:
    /// catch (Exception ex) when (true)
    /// catch (Exception ex) when (1 == 1)
    ///
    /// ✅ Good:
    /// catch (Exception ex)
    /// catch (Exception ex) when (ex is IOException)
    /// catch (Exception ex) when (ShouldLog(ex))
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CatchWhenAlwaysTrueAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EX017";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Catch 'when' clause always true",
            "The 'when' filter always evaluates to true and is redundant.",
            "CodeQuality",
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
            var filter = catchClause.Filter;
            if (filter == null)
                return;

            var constant = context.SemanticModel.GetConstantValue(filter.FilterExpression);
            if (constant.HasValue && constant.Value is bool b && b)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, filter.GetLocation()));
            }
        }
    }
}
