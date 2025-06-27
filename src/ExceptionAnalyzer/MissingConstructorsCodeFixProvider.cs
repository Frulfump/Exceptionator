using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExceptionAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingConstructorsCodeFixProvider)), Shared]
    public class MissingConstructorsCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create("EX021");

        public sealed override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add expected constructors",
                    ct => AddConstructorsAsync(context.Document, diagnostic, ct),
                    nameof(MissingConstructorsCodeFixProvider)),
                diagnostic);
        }

        private async Task<Document> AddConstructorsAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (root == null || semanticModel == null)
                return document;

            var classDecl = root.FindNode(diagnostic.Location.SourceSpan) as ClassDeclarationSyntax;
            if (classDecl == null)
                return document;

            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var constructors = classDecl.Members.OfType<ConstructorDeclarationSyntax>().ToList();

            bool hasMessageCtor = constructors.Any(c =>
                c.ParameterList.Parameters.Count == 1 &&
                c.ParameterList.Parameters[0].Type is PredefinedTypeSyntax pts &&
                pts.Keyword.IsKind(SyntaxKind.StringKeyword));

            bool hasMessageInnerCtor = constructors.Any(c =>
                c.ParameterList.Parameters.Count == 2 &&
                c.ParameterList.Parameters[0].Type is PredefinedTypeSyntax pts1 &&
                pts1.Keyword.IsKind(SyntaxKind.StringKeyword) &&
                c.ParameterList.Parameters[1].Type is IdentifierNameSyntax ins &&
                ins.Identifier.Text == "Exception");

            var newConstructors = new List<MemberDeclarationSyntax>();

            if (!hasMessageCtor)
            {
                newConstructors.Add(SyntaxFactory.ConstructorDeclaration(classDecl.Identifier.Text)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .WithParameterList(SyntaxFactory.ParameterList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("message"))
                                .WithType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword))))))
                    .WithInitializer(SyntaxFactory.ConstructorInitializer(
                        SyntaxKind.BaseConstructorInitializer,
                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(SyntaxFactory.IdentifierName("message"))))))
                    .WithBody(SyntaxFactory.Block()));
            }

            if (!hasMessageInnerCtor)
            {
                newConstructors.Add(SyntaxFactory.ConstructorDeclaration(classDecl.Identifier.Text)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .WithParameterList(SyntaxFactory.ParameterList(
                        SyntaxFactory.SeparatedList(new[]
                        {
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("message"))
                                .WithType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword))),
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("innerException"))
                                .WithType(SyntaxFactory.IdentifierName("Exception"))
                        })))
                    .WithInitializer(SyntaxFactory.ConstructorInitializer(
                        SyntaxKind.BaseConstructorInitializer,
                        SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                        {
                            SyntaxFactory.Argument(SyntaxFactory.IdentifierName("message")),
                            SyntaxFactory.Argument(SyntaxFactory.IdentifierName("innerException"))
                        }))))
                    .WithBody(SyntaxFactory.Block()));
            }

            var newClass = classDecl.AddMembers(newConstructors.ToArray());
            var newRoot = root.ReplaceNode(classDecl, newClass);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
