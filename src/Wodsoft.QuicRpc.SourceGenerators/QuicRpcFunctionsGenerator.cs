using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace Wodsoft.QuicRpc.SourceGenerators
{
    [Generator(LanguageNames.CSharp)]
    public class QuicRpcFunctionsGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var nodes = context.SyntaxProvider.CreateSyntaxProvider((syntaxNode, cancellationToken) =>
            {
                if (syntaxNode is ClassDeclarationSyntax classDeclarationSyntax && classDeclarationSyntax.BaseList != null)
                {
                    foreach (var typeSyntax in classDeclarationSyntax.BaseList.Types)
                    {
                        if (SyntaxHelper.IsSameFullName(typeSyntax.Type, "Wodsoft.QuicRpc.QuicRpcFunctions", false))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }, (syntaxContext, cancellationToken) => (syntaxContext.Node, syntaxContext.SemanticModel)).Combine(context.CompilationProvider);
            context.RegisterSourceOutput(nodes, (context, values) => BuildCode(context, (ClassDeclarationSyntax)values.Left.Node, values.Left.SemanticModel, values.Right));
        }

        private void BuildCode(SourceProductionContext context, ClassDeclarationSyntax classSyntax, SemanticModel model, Compilation compilation)
        {
            var classType = model.GetDeclaredSymbol(classSyntax);
            if (classType == null)
                return;
            var baseType = classType.BaseType!;
            while (true)
            {
                if (SyntaxHelper.IsSameFullName(baseType, "Wodsoft.QuicRpc.QuicRpcFunctions"))
                    break;
                if (baseType.BaseType == null)
                    continue;
                baseType = baseType.BaseType;
            }
            if (classType.ContainingType != null)
            {
                context.ReportDiagnostic(Diagnostic.Create("QUICRPC006", "QuicRpc", "Nested class do not support inherit QuicRpcFunctions.", DiagnosticSeverity.Error,
                    DiagnosticSeverity.Error, true, 0, false,
                    location: Location.Create(classSyntax.SyntaxTree, classSyntax.Identifier.Span)));
                return;
            }
            //检查方法合法性
            foreach (var methodSyntax in classSyntax.Members.OfType<MethodDeclarationSyntax>())
            {
                if (methodSyntax.Modifiers.Any(SyntaxKind.PublicKeyword))
                {
                    if (!methodSyntax.AttributeLists.Any(t => SyntaxHelper.IsSameFullName(t, "Wodsoft.QuicRpc.QuicRpcFunctionAttribute", model)))
                        continue;
                    var returnType = model.GetTypeInfo(methodSyntax.ReturnType);
                    if (!SyntaxHelper.IsSameFullName(methodSyntax.ReturnType, "System.Threading.Tasks.ValueTask", model))
                    {
                        context.ReportDiagnostic(Diagnostic.Create("QUICRPC003", "QuicRpc", "QuicRpc function return type must be 'ValueTask' or 'ValueTask<>'.", DiagnosticSeverity.Error,
                            DiagnosticSeverity.Error, true, 0, false,
                            location: Location.Create(methodSyntax.SyntaxTree, methodSyntax.ReturnType.Span)));
                        continue;
                    }
                    if (methodSyntax.ParameterList.Parameters.Count > 1)
                    {
                        context.ReportDiagnostic(Diagnostic.Create("QUICRPC004", "QuicRpc", "QuicRpc function parameter count must be zero or one.", DiagnosticSeverity.Error,
                            DiagnosticSeverity.Error, true, 0, false,
                            location: Location.Create(methodSyntax.SyntaxTree, methodSyntax.ParameterList.Span)));
                        continue;
                    }
                }
            }

            //如果是抽象类则不生成Bind方法
            if (classSyntax.Modifiers.Any(SyntaxKind.AbstractKeyword))
                return;
            bool isPartial = classSyntax.Modifiers.Any(SyntaxKind.PartialKeyword);
            //没有partial关键字抛错
            if (!isPartial)
            {
                context.ReportDiagnostic(Diagnostic.Create("QUICRPC001", "QuicRpc", "Class inherited QuicRpcFunctions must have partital keyword or be an abstract class.", DiagnosticSeverity.Error,
                    DiagnosticSeverity.Error, true, 0, false,
                    location: Location.Create(classSyntax.SyntaxTree, classSyntax.Identifier.Span)));
                return;
            }
            var functionAttribute = classType.GetAttributes().FirstOrDefault(t => t.AttributeClass != null && SyntaxHelper.IsSameFullName(t.AttributeClass, "Wodsoft.QuicRpc.QuicRpcFunctionAttribute"));
            //必须有QuicRpcFunctionAttribute
            if (functionAttribute == null)
            {
                context.ReportDiagnostic(Diagnostic.Create("QUICRPC002", "QuicRpc", "Class inherited QuicRpcFunctions or its subclass must have a 'QuicRpcFunctionAttribute'.", DiagnosticSeverity.Error,
                    DiagnosticSeverity.Error, true, 0, false,
                    location: Location.Create(classSyntax.SyntaxTree, classSyntax.Identifier.Span)));
                return;
            }
            var classId = (byte)functionAttribute.ConstructorArguments[0].Value! << 8;

            Dictionary<byte, RpcFunction> functions = new Dictionary<byte, RpcFunction>();

            foreach (var methodSyntax in classType.GetMembers().OfType<IMethodSymbol>())
            {
                if (methodSyntax.MethodKind == MethodKind.Ordinary && methodSyntax.DeclaredAccessibility == Accessibility.Public && !methodSyntax.IsStatic)
                {
                    functionAttribute = methodSyntax.GetAttributes().FirstOrDefault(t => t.AttributeClass != null && SyntaxHelper.IsSameFullName(t.AttributeClass, "Wodsoft.QuicRpc.QuicRpcFunctionAttribute"));
                    if (functionAttribute == null)
                        continue;
                    var functionId = (byte)functionAttribute.ConstructorArguments[0].Value!;
                    if (functions.ContainsKey(functionId))
                    {
                        if (methodSyntax.DeclaringSyntaxReferences[0].SyntaxTree == classSyntax.SyntaxTree)
                        {
                            context.ReportDiagnostic(Diagnostic.Create("QUICRPC005", "QuicRpc", $"Function id \"{functionId}\" is used by {functions[functionId].Method.Name}.", DiagnosticSeverity.Error,
                                DiagnosticSeverity.Error, true, 0, false,
                                location: Location.Create(classSyntax.SyntaxTree, functionAttribute.ApplicationSyntaxReference!.Span)));
                        }
                        else
                        {
                            context.ReportDiagnostic(Diagnostic.Create("QUICRPC005", "QuicRpc", $"Function \"{methodSyntax.Name}\" from base type \"{methodSyntax.ContainingType}\" using a custom id \"{functionId}\" is used by {functions[functionId].Method.Name}.", DiagnosticSeverity.Error,
                                DiagnosticSeverity.Error, true, 0, false,
                                location: Location.Create(classSyntax.SyntaxTree, functionAttribute.ApplicationSyntaxReference!.Span)));
                        }
                        continue;
                    }
                    bool isStreaming;
                    if (functionAttribute.NamedArguments.Length != 0 && functionAttribute.NamedArguments[0].Value.Value!.Equals(true))
                    {
                        if (methodSyntax.Parameters.Length != 0)
                        {
                            context.ReportDiagnostic(Diagnostic.Create("QUICRPC004", "QuicRpc", "QuicRpc streaming function parameter count must be zero.", DiagnosticSeverity.Error,
                                DiagnosticSeverity.Error, true, 0, false,
                                location: Location.Create(classSyntax.SyntaxTree, functionAttribute.ApplicationSyntaxReference!.Span)));
                            continue;
                        }
                        else if (methodSyntax.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) != "global::System.Threading.Tasks.ValueTask")
                        {
                            context.ReportDiagnostic(Diagnostic.Create("QUICRPC004", "QuicRpc", "QuicRpc streaming function return type must be ValueTask.", DiagnosticSeverity.Error,
                                DiagnosticSeverity.Error, true, 0, false,
                                location: Location.Create(classSyntax.SyntaxTree, functionAttribute.ApplicationSyntaxReference!.Span)));
                            continue;
                        }
                        isStreaming = true;
                    }
                    else
                        isStreaming = false;
                    functions.Add(functionId, new RpcFunction
                    {
                        ReturnType = ((INamedTypeSymbol)methodSyntax.ReturnType).TypeArguments.FirstOrDefault(),
                        Method = methodSyntax,
                        Parameter = methodSyntax.Parameters.FirstOrDefault(),
                        IsStreaming = isStreaming
                    });

                }
            }

            if (functions.Count == 0)
                return;
            var builder = new StringBuilder();
            builder.AppendLine("// QuicRpc auto generated.");
            builder.AppendLine();
            builder.AppendLine($"namespace {classType.ContainingNamespace}");
            builder.AppendLine("{");
            builder.AppendLine($"    {classSyntax.Modifiers} class {classType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} : global::Wodsoft.QuicRpc.IQuicRpcFunctions");
            builder.AppendLine("    {");
            builder.AppendLine("        void global::Wodsoft.QuicRpc.IQuicRpcFunctions.Bind<TContext>(global::Wodsoft.QuicRpc.QuicRpcService<TContext> service)");
            builder.AppendLine("        {");
            foreach (var function in functions)
            {
                if (function.Value.Parameter == null)
                {
                    if (function.Value.IsStreaming)
                    {
                        builder.AppendLine($"            service.RegisterStreamingFunction({classId + function.Key}, context =>");
                    }
                    else if (function.Value.ReturnType == null)
                    {
                        builder.AppendLine($"            service.RegisterFunction({classId + function.Key}, context =>");
                    }
                    else
                    {
                        builder.AppendLine($"            service.RegisterFunction<{function.Value.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>({classId + function.Key}, context =>");
                    }
                }
                else
                {
                    if (function.Value.ReturnType == null)
                    {
                        builder.AppendLine($"            service.RegisterFunction<{function.Value.Parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>({classId + function.Key}, (context, request) =>");
                    }
                    else
                    {
                        builder.AppendLine($"            service.RegisterFunction<{function.Value.Parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}, {function.Value.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>({classId + function.Key}, (context, request) =>");
                    }
                }
                builder.AppendLine("            {");
                builder.AppendLine("                SetContext(context);");
                if (function.Value.Parameter == null)
                    builder.AppendLine($"                return {function.Value.Method.Name}();");
                else
                    builder.AppendLine($"                return {function.Value.Method.Name}(request);");
                builder.AppendLine("            });");
            }
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            var filename = $"{classType.ContainingNamespace}.{classType.Name}.g.cs";
            context.AddSource(filename, builder.ToString());
        }

        private struct RpcFunction
        {
            public ITypeSymbol? ReturnType;

            public IMethodSymbol Method;

            public IParameterSymbol? Parameter;

            public bool IsStreaming;
        }
    }
}
