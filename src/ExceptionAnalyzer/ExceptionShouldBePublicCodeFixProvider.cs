using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExceptionAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExceptionShouldBePublicCodeFixProvider)), Shared]
    public class ExceptionShouldBePublicCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Make exception class public";

        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(ExceptionShouldBePublicAnalyzer.DiagnosticId);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null)
                return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var declaration = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (declaration == null)
                return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => MakePublicAsync(context.Document, declaration, c),
                    equivalenceKey: Title),
                diagnostic);
        }

        private static async Task<Document> MakePublicAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
        {
            var modifiers = classDecl.Modifiers;

            // Remove existing accessibility modifiers
            var cleanedModifiers = new SyntaxTokenList(modifiers
                .Where(m => !m.IsKind(SyntaxKind.InternalKeyword)
                         && !m.IsKind(SyntaxKind.PrivateKeyword)
                         && !m.IsKind(SyntaxKind.ProtectedKeyword)
                         && !m.IsKind(SyntaxKind.PublicKeyword)));

            // Add 'public' at the start (with appropriate spacing)
            var publicToken = SyntaxFactory.Token(SyntaxKind.PublicKeyword)
                .WithTrailingTrivia(SyntaxFactory.Whitespace(" "));

            var newModifiers = cleanedModifiers.Insert(0, publicToken);

            var newClassDecl = classDecl.WithModifiers(newModifiers).WithAdditionalAnnotations(Formatter.Annotation);

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = root!.ReplaceNode(classDecl, newClassDecl);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
