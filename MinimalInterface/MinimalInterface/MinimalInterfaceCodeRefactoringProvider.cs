using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace MinimalInterface
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(MinimalInterfaceCodeRefactoringProvider)), Shared]
    internal class MinimalInterfaceCodeRefactoringProvider : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            // TODO: Replace the following code with your own analysis, generating a CodeAction for each refactoring to offer

            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // Find the node at the selection.
            var node = root.FindNode(context.Span);

            if (node is IdentifierNameSyntax identifierNameSyntax)
            {
                var semanticModel = await context.Document.GetSemanticModelAsync();
                var typeInfo = semanticModel.GetTypeInfo(identifierNameSyntax);
                var nameSpace = ((INamedTypeSymbol)typeInfo.Type).ContainingNamespace;
                var nameSpaceName = nameSpace.Name;
            }
            if (node is TypeDeclarationSyntax typeDecl)
            {
                // For any type declaration node, create a code action to reverse the identifier text.
                var action = CodeAction.Create("Copy Use To Clipboard", c => ReverseTypeNameAsync(context.Document, typeDecl, c));
                // var action = CodeAction.Create("Usages To Clipboard", c => DocumentUsagesAsync(context.Document, typeDecl, c));
                context.RegisterRefactoring(action);
            }
        }

        private async Task<Solution> DocumentUsagesAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            // Produce a reversed version of the type declaration's identifier token.
            var identifierToken = typeDecl.Identifier;
            var newName = new string(identifierToken.Text.ToCharArray().Reverse().ToArray());
            var sb = new System.Text.StringBuilder();


            // Get the symbol representing the type to be renamed.
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

            if (typeSymbol == null)
            {
                Debug.WriteLine("Class not found");
                return document.Project.Solution;
            }

            Microsoft.CodeAnalysis.Editing.SyntaxGenerator synGen = Microsoft.CodeAnalysis.Editing.SyntaxGenerator.GetGenerator(document);
            if (synGen == null)
            {
                Debug.WriteLine("Could not build generator.");
                return document.Project.Solution;
            }

            var fullClassName = typeSymbol.ContainingNamespace + "." + typeSymbol.Name;

            // Produce a new solution that has all references to that type renamed, including the declaration.
            var originalSolution = document.Project.Solution;
            var optionSet = originalSolution.Workspace.Options;
            // var newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, typeSymbol, newName, optionSet, cancellationToken).ConfigureAwait(false);


            foreach (var proj in originalSolution.Projects)
            {
                sb.AppendLine($"=== {proj.Name}");
                if (document.Project == proj)
                {
                    sb.AppendLine($" skipped");
                    continue;
                }
                var compilation = proj.GetCompilationAsync().Result;
                // Look for a reference to the Class in the Assembly
                var classRef = compilation.GetTypeByMetadataName(fullClassName);

                if (classRef != null)
                {
                    var methodsToSearchFor = classRef.GetMembers();
                    foreach (var methodToSearchFor in methodsToSearchFor)
                    {
                        var references = SymbolFinder.FindReferencesAsync(methodToSearchFor, originalSolution).Result;
                        foreach (var loc in references.SelectMany(x => x.Locations))
                        {
                            if (loc.Location.ToString().Contains(proj.Name))
                            {
                                // SymbolDisplay.ToDisplayString();

                                // var dec = SymbolFinder.FindSourceDefinitionAsync(methodToSearchFor, originalSolution, cancellationToken).Result;
                                //var mDec = dec as IMethodSymbol;
                                //if (mDec != null)
                                //{
                                //    SymbolDisplayParameterOptions v = (SymbolDisplayParameterOptions)64 - 1;
                                //    SymbolDisplayGenericsOptions gen = (SymbolDisplayGenericsOptions)8 - 1;
                                //    SymbolDisplayFormat f = SymbolDisplayFormat.FullyQualifiedFormat.AddParameterOptions(v).AddGenericsOptions(gen);

                                //    var s = SymbolDisplay.ToDisplayString(mDec, f);
                                //}

                                //var ll = dec.Locations.FirstOrDefault();
                                //var tt = ll.SourceTree.GetText();
                                //var src = tt.GetSubText(ll.SourceSpan);




                                var asString = methodToSearchFor.ToString();
                                var met = methodToSearchFor as IMethodSymbol;
                                if (met != null)
                                {
                                    var parameters = met.Parameters.Select(p => synGen.ParameterDeclaration(
                                        p.Name,
                                        type: synGen.TypeExpression(p.Type),
                                        initializer: p.HasExplicitDefaultValue ? synGen.LiteralExpression(p.ExplicitDefaultValue) : null,
                                        refKind: p.RefKind));

                                    // asString = SymbolDisplay.ToDisplayString(methodToSearchFor, s_debuggerDisplayFormat);
                                    // Debug.WriteLine(asString);
                                    asString = met.ReturnType + " " + methodToSearchFor.ToString();
                                    //Debug.WriteLine($"{asString};");
                                    //break;
                                }
                                asString = asString.Replace(fullClassName + ".", "");
                                if (asString.Contains("StoreMode"))
                                {

                                }
                                var asEvent = methodToSearchFor;
                                if (methodToSearchFor is IEventSymbol eventSymbol)
                                {
                                    // eventSymbol
                                    asString = eventSymbol.Type + " " + eventSymbol.ToString();
                                }
                                else if (methodToSearchFor is IFieldSymbol fieldSymbol)
                                {
                                    // eventSymbol
                                    if (fieldSymbol.DeclaredAccessibility != Accessibility.Public)
                                        break;
                                    asString = fieldSymbol.Type + " " + fieldSymbol.ToString();
                                    // fieldSymbol.acc
                                }
                                else if (methodToSearchFor is INamedTypeSymbol namedTypeSymbol)
                                {
                                    // eventSymbol
                                    if (namedTypeSymbol.DeclaredAccessibility != Accessibility.Public)
                                        break;
                                    asString = "enum" + " " + namedTypeSymbol.ToString();
                                    // fieldSymbol.acc
                                }

                                sb.AppendLine($"{asString};");
                                break;
                            }
                        }
                    }
                }
            }
            Debug.WriteLine(sb.ToString());
            // Return the new solution with the now-uppercase type name.
            return originalSolution;
        }


        private async Task<Solution> ReverseTypeNameAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            // Produce a reversed version of the type declaration's identifier token.
            var identifierToken = typeDecl.Identifier;
            var newName = new string(identifierToken.Text.ToCharArray().Reverse().ToArray());
            var sb = new System.Text.StringBuilder();


            // Get the symbol representing the type to be renamed.
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

            if (typeSymbol == null)
            {
                Debug.WriteLine("Class not found");
                return document.Project.Solution;
            }

            Microsoft.CodeAnalysis.Editing.SyntaxGenerator synGen = Microsoft.CodeAnalysis.Editing.SyntaxGenerator.GetGenerator(document);
            if (synGen == null)
            {
                Debug.WriteLine("Could not build generator.");
                return document.Project.Solution;
            }

            var fullClassName = typeSymbol.ContainingNamespace + "." + typeSymbol.Name;
                
            // Produce a new solution that has all references to that type renamed, including the declaration.
            var originalSolution = document.Project.Solution;
            var optionSet = originalSolution.Workspace.Options;
            // var newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, typeSymbol, newName, optionSet, cancellationToken).ConfigureAwait(false);
            

            foreach (var proj in originalSolution.Projects)
            {
                sb.AppendLine($"=== {proj.Name}");
                if (document.Project == proj)
                {
                    sb.AppendLine($" skipped");
                    continue;
                }
                var compilation = proj.GetCompilationAsync().Result;
                // Look for a reference to the Class in the Assembly
                var classRef = compilation.GetTypeByMetadataName(fullClassName);

                if (classRef != null)
                {
                    var methodsToSearchFor = classRef.GetMembers();
                    foreach (var methodToSearchFor in methodsToSearchFor)
                    {
                        var references = SymbolFinder.FindReferencesAsync(methodToSearchFor, originalSolution).Result;
                        foreach (var loc in references.SelectMany(x=>x.Locations))
                        {
                            if (loc.Location.ToString().Contains(proj.Name))
                            {
                                // SymbolDisplay.ToDisplayString();

                                // var dec = SymbolFinder.FindSourceDefinitionAsync(methodToSearchFor, originalSolution, cancellationToken).Result;
                                //var mDec = dec as IMethodSymbol;
                                //if (mDec != null)
                                //{
                                //    SymbolDisplayParameterOptions v = (SymbolDisplayParameterOptions)64 - 1;
                                //    SymbolDisplayGenericsOptions gen = (SymbolDisplayGenericsOptions)8 - 1;
                                //    SymbolDisplayFormat f = SymbolDisplayFormat.FullyQualifiedFormat.AddParameterOptions(v).AddGenericsOptions(gen);

                                //    var s = SymbolDisplay.ToDisplayString(mDec, f);
                                //}

                                //var ll = dec.Locations.FirstOrDefault();
                                //var tt = ll.SourceTree.GetText();
                                //var src = tt.GetSubText(ll.SourceSpan);
                                



                                var asString = methodToSearchFor.ToString();
                                var met = methodToSearchFor as IMethodSymbol;
                                if (met != null)
                                {
                                    var parameters = met.Parameters.Select(p => synGen.ParameterDeclaration(
                                        p.Name,
                                        type: synGen.TypeExpression(p.Type),
                                        initializer: p.HasExplicitDefaultValue ? synGen.LiteralExpression(p.ExplicitDefaultValue) : null,
                                        refKind: p.RefKind));

                                    // asString = SymbolDisplay.ToDisplayString(methodToSearchFor, s_debuggerDisplayFormat);
                                    // Debug.WriteLine(asString);
                                    asString = met.ReturnType + " " + methodToSearchFor.ToString();
                                    //Debug.WriteLine($"{asString};");
                                    //break;
                                }
                                asString = asString.Replace(fullClassName + ".", "");
                                if (asString.Contains("StoreMode"))
                                {

                                }
                                var asEvent = methodToSearchFor ;
                                if (methodToSearchFor is IEventSymbol eventSymbol)
                                {
                                    // eventSymbol
                                    asString = eventSymbol.Type + " " + eventSymbol.ToString();
                                }
                                else if (methodToSearchFor is IFieldSymbol fieldSymbol)
                                {
                                    // eventSymbol
                                    if (fieldSymbol.DeclaredAccessibility != Accessibility.Public)
                                        break;
                                    asString = fieldSymbol.Type + " " + fieldSymbol.ToString();
                                    // fieldSymbol.acc
                                }
                                else if (methodToSearchFor is INamedTypeSymbol namedTypeSymbol)
                                {
                                    // eventSymbol
                                    if (namedTypeSymbol.DeclaredAccessibility != Accessibility.Public)
                                        break;
                                    asString = "enum" + " " + namedTypeSymbol.ToString();
                                    // fieldSymbol.acc
                                }

                                sb.AppendLine($"{asString};");
                                break;
                            }                           
                        }
                    }
                }
            }
            Debug.WriteLine(sb.ToString());
            // Return the new solution with the now-uppercase type name.
            return originalSolution;
        }
    }
}
