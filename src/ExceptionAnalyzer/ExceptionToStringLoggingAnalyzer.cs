﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace ExceptionAnalyzer
{
    /// <summary>
    /// EX015: Avoid logging ex.ToString()
    /// Recommends logging the exception directly rather than calling ToString.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ExceptionToStringLoggingAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EX015";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Avoid logging ex.ToString()",
            "Log the exception object directly instead of using ex.ToString() to preserve stack trace and structure.",
            "ExceptionHandling",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var arguments = invocation.ArgumentList?.Arguments;
            if (arguments == null || arguments.Value.Count == 0)
                return;

            foreach (var arg in arguments)
            {
                // Check if argument is a binary expression: "something" + ex.ToString()
                if (arg.Expression is BinaryExpressionSyntax binary &&
                    (binary.Right is InvocationExpressionSyntax rightInvocation &&
                     rightInvocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                     memberAccess.Name.Identifier.Text == "ToString" &&
                     memberAccess.Expression is IdentifierNameSyntax id))
                {
                    ReportIfException(context, id, arg);
                }

                // Also catch: $"something {ex.ToString()}" or $"something {ex}"
                if (arg.Expression is InterpolatedStringExpressionSyntax interpolated)
                {
                    foreach (var content in interpolated.Contents.OfType<InterpolationSyntax>())
                    {
                        if (content.Expression is InvocationExpressionSyntax interpInvocation &&
                            interpInvocation.Expression is MemberAccessExpressionSyntax ma &&
                            ma.Name.Identifier.Text == "ToString" &&
                            ma.Expression is IdentifierNameSyntax interpId)
                        {
                            ReportIfException(context, interpId, arg);
                        }
                        else if (content.Expression is IdentifierNameSyntax interpSimpleId)
                        {
                            ReportIfException(context, interpSimpleId, arg);
                        }
                    }
                }
            }
        }

        private void ReportIfException(SyntaxNodeAnalysisContext context, IdentifierNameSyntax id, ArgumentSyntax arg)
        {
            var symbol = context.SemanticModel.GetSymbolInfo(id).Symbol;
            if (symbol is ILocalSymbol localSymbol &&
                localSymbol.Type.ToDisplayString() == "System.Exception")
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, arg.GetLocation()));
            }
        }
    }
}
