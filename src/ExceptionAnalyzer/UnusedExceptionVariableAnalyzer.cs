using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace ExceptionAnalyzer
{
    /// <summary>
    /// Analyzer EX005: Identifies unused exception variables in catch blocks.
    ///
    /// Catching an exception with a named variable (e.g. `catch (Exception ex)`) without ever using
    /// that variable leads to clutter and reduces code clarity. If the variable is not used,
    /// it should be omitted (i.e. `catch (Exception)` or simply `catch` if the type isn't important).
    ///
    /// <para>⚠️ Triggers on:</para>
    /// <code>
    /// try
    /// {
    ///     DoSomething();
    /// }
    /// catch (Exception ex)
    /// {
    ///     LogError(); // 'ex' is never used
    /// }
    /// </code>
    ///
    /// <para>✅ Preferred usage:</para>
    /// <code>
    /// try
    /// {
    ///     DoSomething();
    /// }
    /// catch (Exception)
    /// {
    ///     LogError();
    /// }
    /// </code>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnusedExceptionVariableAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EX005";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Exception variable is unused",
            "The caught exception '{0}' is never used.",
            "Usage",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(AnalyzeCatch, SyntaxKind.CatchClause);
        }

        private void AnalyzeCatch(SyntaxNodeAnalysisContext context)
        {
            var catchClause = (CatchClauseSyntax)context.Node;
            var identifier = catchClause.Declaration?.Identifier;
            if (identifier == null || !identifier.HasValue || identifier.Value.ValueText == "")
                return;

            var block = catchClause.Block;
            if (block == null)
                return;

            // Is the variable used?
            var used = block.DescendantTokens()
                .Any(t => t.IsKind(SyntaxKind.IdentifierToken) && t.ValueText == identifier.Value.ValueText);

            if (!used)
            {
                var diagnostic = Diagnostic.Create(Rule, identifier.Value.GetLocation(), identifier.Value.ValueText);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
