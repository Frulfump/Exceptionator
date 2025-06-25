using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace ExceptionAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnusedExceptionVariableCodeFixProvider)), Shared]
    public class UnusedExceptionVariableCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Remove unused exception variable";

        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(UnusedExceptionVariableAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics[0];
            var node = root?.FindNode(diagnostic.Location.SourceSpan)?.Parent?.Parent as CatchClauseSyntax;

            if (node == null || node.Declaration == null)
                return;

            context.RegisterCodeFix(
                Microsoft.CodeAnalysis.CodeActions.CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => RemoveExceptionVariableAsync(context.Document, node, c),
                    equivalenceKey: Title),
                diagnostic);
        }

        private async Task<Document> RemoveExceptionVariableAsync(Document document, CatchClauseSyntax catchClause, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken);

            // Behold typen, fjern variabelnavn
            var newDeclaration = catchClause.Declaration!.WithIdentifier(SyntaxFactory.MissingToken(SyntaxKind.IdentifierToken));
            var newCatch = catchClause.WithDeclaration(newDeclaration);

            editor.ReplaceNode(catchClause, newCatch);
            return editor.GetChangedDocument();
        }
    }
}
