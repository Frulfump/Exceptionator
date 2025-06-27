using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace ExceptionAnalyzer
{
    /// <summary>
    /// EX023: Exception class name must end with 'Exception'
    /// Ensures consistency and clarity by requiring exception classes to follow the naming convention of ending with 'Exception'.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ExceptionNameMustEndWithExceptionAnalyzer : CustomExceptionAnalyzerBase
    {
        public const string DiagnosticId = "EX023";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Exception class name must end with 'Exception'",
            "Class '{0}' inherits from System.Exception but does not end with 'Exception'.",
            "Naming",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
        }

        private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;
            var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl);
            if (symbol == null || symbol.TypeKind != TypeKind.Class)
                return;

            if (InheritsFromException(symbol) && !symbol.Name.EndsWith("Exception"))
            {
                var diagnostic = Diagnostic.Create(Rule, classDecl.Identifier.GetLocation(), symbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
