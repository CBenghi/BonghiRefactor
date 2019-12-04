using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
                var sInfo = semanticModel.GetSymbolInfo(identifierNameSyntax, context.CancellationToken);
                try
                {
                    if (sInfo.Symbol == null)
                        return;
                    var action = CodeAction.Create("Create use interface for class", c => CreateUsagesInterfaceAsync(context, sInfo.Symbol, c));
                    context.RegisterRefactoring(action);

                    var ass = sInfo.Symbol.ContainingAssembly;
                    if (ass == null)
                        return;
                    var action2 = CodeAction.Create("Create use interface for assembly", c => CreateUsagesInterfaceForAssemblyAsync(context, ass, c));
                    context.RegisterRefactoring(action2);
                }
                catch (System.Exception ex)
                {
                    Debug.WriteLine("Caught in lib: " + ex.Message);
                }               
            }
            if (node is TypeDeclarationSyntax typeDecl)
            {
                // For any type declaration node, create a code action to reverse the identifier text.
                var action = CodeAction.Create("Copy Use To Clipboard", c => ReverseTypeNameAsync(context.Document, typeDecl, c));
                // var action = CodeAction.Create("Usages To Clipboard", c => DocumentUsagesAsync(context.Document, typeDecl, c));
                context.RegisterRefactoring(action);
            }
        }

        private async Task<Solution> CreateUsagesInterfaceForAssemblyAsync(CodeRefactoringContext context, IAssemblySymbol assembly, CancellationToken c)
        {
            string code = "";
            var compilation = context.Document.Project.GetCompilationAsync().Result;
            var m2 = assembly.GlobalNamespace.GetMembers();
            foreach (var item in m2)
            {
                foreach (var item2 in item.GetMembers())
                {
                    if (item2.Kind == SymbolKind.NamedType)
                    {
                        var fNname = item2.ContainingNamespace + "." + item2.Name;
                        var sb = PrepareInterfaceText(context, fNname, compilation);
                        code += sb.ToString() + "\r\n";
                    }
                }
            }
            if (code != "")
            {
                var newDoc = context.Document.Project.AddAdditionalDocument("ExtractedInterface.cs", code);
                return newDoc.Project.Solution;
            }
            return context.Document.Project.Solution;
        }


        private async Task<Solution> CreateUsagesInterfaceAsync(CodeRefactoringContext context, ISymbol symbol, CancellationToken cancellationToken)
        {
            var originalSolution = context.Document.Project.Solution;
            var qualifiedClassName = $"{symbol.ContainingNamespace}.{symbol.Name}";
            var compilation = context.Document.Project.GetCompilationAsync().Result;
            PrepareInterfaceText(context, qualifiedClassName, compilation);
            return originalSolution;
        }

        private static StringBuilder PrepareInterfaceText(CodeRefactoringContext context, string qualifiedClassName, Compilation compilation)
        {
            StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"// {qualifiedClassName} in {context.Document.Project.Name}");

            

            
            var classRef = compilation.GetTypeByMetadataName(qualifiedClassName);
            // classRef = compilation.GetTypeByMetadataName(symbol);
            
            if (classRef != null)
            {
                sb.AppendLine($"namespace {classRef.ContainingNamespace} {{");
                sb.AppendLine($"interface {classRef.Name}ExtractedInterface");
                sb.AppendLine("{");
                var methodsToSearchFor = classRef.GetMembers();
                foreach (var methodToSearchFor in methodsToSearchFor)
                {
                    var asString = ""; // $"// skipped {methodToSearchFor.ToString()} // standard";
                    var references = SymbolFinder.FindReferencesAsync(methodToSearchFor, context.Document.Project.Solution).Result;
                    var projReferences = references.Where(x => x.Locations.Any(loc => loc.Document.Project == context.Document.Project));
                    var projLocs = projReferences.SelectMany(x => x.Locations.Where(loc => loc.Document.Project == context.Document.Project));
                    if (projLocs.Any())
                    {
                        asString = $"{methodToSearchFor.ToString()} // standard";
                        if (methodToSearchFor is IMethodSymbol met)
                        {
                            if (
                                met.ToString().ToLowerInvariant().EndsWith(".get")
                                ||
                                met.ToString().ToLowerInvariant().EndsWith(".set")
                                )
                                asString = "";
                            else
                            {
                                
                                var jnd = met.Parameters.Select(x=>x.Name).ToArray();
                                var j2 = string.Join(",", jnd);

                                asString = $"{met.ReturnType} {met.ToString()}; // {j2} IMethodSymbol";
                            }
                        }
                        else if (methodToSearchFor is IEventSymbol eventSymbol)
                        {
                            // eventSymbol
                            asString = $"{eventSymbol.Type} {eventSymbol.ToString()} // IEventSymbol";
                        }
                        else if (methodToSearchFor is IPropertySymbol propSymbol)
                        {
                            asString = propSymbol.Type + " " + propSymbol.ToString() + " {get; set;} // IPropertySymbol";
                        }
                        else if (methodToSearchFor is IFieldSymbol fieldSymbol)
                        {
                            // eventSymbol
                            if (fieldSymbol.DeclaredAccessibility != Accessibility.Public)
                            {
                                asString = $"// skipped non public {fieldSymbol.Type} {fieldSymbol.ToString()} //IFieldSymbol";
                            }
                            else
                            {
                                asString = $"{fieldSymbol.Type} {fieldSymbol.ToString()}; //IFieldSymbol";
                            }
                        }
                        else if (methodToSearchFor is INamedTypeSymbol namedTypeSymbol)
                        {
                            // eventSymbol
                            if (namedTypeSymbol.DeclaredAccessibility != Accessibility.Public)
                            {
                                asString = $"// skipped non public enum {namedTypeSymbol.ToString()} //INamedTypeSymbol";
                            }
                            else
                            {
                                asString = $"enum {namedTypeSymbol.ToString()} //INamedTypeSymbol";
                            }
                            // fieldSymbol.acc
                        }
                        if (false)
                        {
                            foreach (var projLoc in projLocs)
                            {
                                var sp = projLoc.Location.GetMappedLineSpan();

                                asString += $"\r\n// loc: {projLoc.Location.SourceSpan} line: {sp.StartLinePosition.Line + 1}, {sp.StartLinePosition.Character + 1}";
                            }
                        }
                        asString = asString.Replace(qualifiedClassName + ".", "");
                    }
                    if (!string.IsNullOrEmpty(asString))
                        sb.AppendLine($"{asString};");
                }
            }
            sb.AppendLine("}}");
            return sb;
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
