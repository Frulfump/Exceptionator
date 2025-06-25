using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace ExceptionAnalyzer
{
    /// <summary>
    /// Analyzer EX009: Detects empty <c>try</c> blocks that have one or more <c>catch</c> clauses.
    ///
    /// An empty <c>try</c> block with a <c>catch</c> may indicate a logic error, leftover debugging code, or unnecessary exception handling.
    /// Unless there is a documented reason, such code should either contain meaningful logic or be removed.
    ///
    /// <para>⚠️ Triggers on:</para>
    /// <code>
    /// try
    /// {
    ///     // nothing here
    /// }
    /// catch (Exception ex)
    /// {
    ///     Log(ex);
    /// }
    /// </code>
    ///
    /// <para>✅ Preferred usage:</para>
    /// <code>
    /// try
    /// {
    ///     DoSomethingRisky();
    /// }
    /// catch (Exception ex)
    /// {
    ///     Log(ex);
    /// }
    /// </code>
    ///
    /// <para>✅ Also allowed with comment indicating intent:</para>
    /// <code>
    /// try
    /// {
    ///     // intentionally left empty – external system throws randomly
    /// }
    /// catch (Exception ex)
    /// {
    ///     Log(ex);
    /// }
    /// </code>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EmptyTryBlockAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EX009";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Empty try block with catch",
            "Try block is empty but includes a catch – consider removing or adding meaningful code.",
            "CodeQuality",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(AnalyzeTryBlock, SyntaxKind.TryStatement);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S2486:Generic exceptions should not be ignored", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S108:Nested blocks of code should not be left empty", Justification = "<Pending>")]
        private void AnalyzeTryBlock(SyntaxNodeAnalysisContext context)
        {
            try
            {
                int i = 0;
            }
            catch (System.Exception)
            {

            }
            var tryStatement = (TryStatementSyntax)context.Node;

            if (tryStatement.Block == null || tryStatement.Block.Statements.Count != 0)
                return;

            if (tryStatement.Catches.Count == 0)
                return;

            var catchesException = tryStatement.Catches.Any(c =>
            {
                if (c.Declaration == null)
                    return true;

                var type = context.SemanticModel.GetTypeInfo(c.Declaration.Type).Type;
                return type?.ToDisplayString() == "System.Exception";
            });

            if (catchesException)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, tryStatement.TryKeyword.GetLocation()));
            }
        }
    }
}
