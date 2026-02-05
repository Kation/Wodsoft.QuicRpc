using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wodsoft.QuicRpc.SourceGenerators
{
    [Generator]
    public class QuicRpcClientGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var nodes = context.SyntaxProvider.CreateSyntaxProvider((syntaxNode, cancellationToken) =>
            {
                if (syntaxNode is StructDeclarationSyntax structDeclarationSyntax && structDeclarationSyntax.BaseList != null)
                {
                    foreach (var typeSyntax in structDeclarationSyntax.BaseList.Types)
                    {
                        if (SyntaxHelper.IsSameFullName(typeSyntax.Type, "Wodsoft.QuicRpc.IQuicRpcClient", false))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }, (syntaxContext, cancellationToken) => (syntaxContext.Node, syntaxContext.SemanticModel)).Combine(context.CompilationProvider);
            
            context.RegisterSourceOutput(nodes, (context, values) => BuildCode(context, (StructDeclarationSyntax)values.Left.Node, values.Left.SemanticModel, values.Right));
        }

        private void BuildCode(SourceProductionContext context, StructDeclarationSyntax typeDeclarationSyntax, SemanticModel model, Compilation compilation)
        {
            var structType = model.GetDeclaredSymbol(typeDeclarationSyntax);
            if (structType == null)
                return;
            if (structType.ContainingType != null)
            {
                context.ReportDiagnostic(Diagnostic.Create("QUICRPC006", "QuicRpc", "嵌套的struct不支持实现IQuicRpcClient。", DiagnosticSeverity.Error,
                    DiagnosticSeverity.Error, true, 0, false,
                    location: Location.Create(typeDeclarationSyntax.SyntaxTree, typeDeclarationSyntax.Identifier.Span)));
                return;
            }
            bool isPartial = typeDeclarationSyntax.Modifiers.Any(SyntaxKind.PartialKeyword);
            //没有partial关键字抛错
            if (!isPartial)
            {
                context.ReportDiagnostic(Diagnostic.Create("QUICRPC001", "QuicRpc", "实现IQuicRpcClient的结构体必须有partial关键字。", DiagnosticSeverity.Error,
                    DiagnosticSeverity.Error, true, 0, false,
                    location: Location.Create(typeDeclarationSyntax.SyntaxTree, typeDeclarationSyntax.Identifier.Span)));
                return;
            }
            var functionAttribute = structType.GetAttributes().FirstOrDefault(t => t.AttributeClass != null && SyntaxHelper.IsSameFullName(t.AttributeClass, "Wodsoft.QuicRpc.QuicRpcFunctionAttribute"));
            //必须有QuicRpcFunctionAttribute
            if (functionAttribute == null)
            {
                context.ReportDiagnostic(Diagnostic.Create("QUICRPC002", "QuicRpc", "实现IQuicRpcClient的结构体必须有'QuicRpcFunctionAttribute'特性。", DiagnosticSeverity.Error,
                    DiagnosticSeverity.Error, true, 0, false,
                    location: Location.Create(typeDeclarationSyntax.SyntaxTree, typeDeclarationSyntax.Identifier.Span)));
                return;
            }
            var classId = (byte)functionAttribute.ConstructorArguments[0].Value! << 8;


            Dictionary<byte, RpcFunction> functions = new Dictionary<byte, RpcFunction>();

            foreach (var methodSyntax in structType.GetMembers().OfType<IMethodSymbol>())
            {
                if (methodSyntax.MethodKind == MethodKind.Ordinary && methodSyntax.IsPartialDefinition && methodSyntax.DeclaredAccessibility == Accessibility.Public && !methodSyntax.IsStatic)
                {
                    functionAttribute = methodSyntax.GetAttributes().FirstOrDefault(t => t.AttributeClass != null && SyntaxHelper.IsSameFullName(t.AttributeClass, "Wodsoft.QuicRpc.QuicRpcFunctionAttribute"));
                    if (functionAttribute == null)
                        return;

                    if (methodSyntax.Parameters.Length > 2)
                    {
                        context.ReportDiagnostic(Diagnostic.Create("QUICRPC007", "QuicRpc", "QuicRpcClient服务方法不支持超过两个参数。", DiagnosticSeverity.Error,
                            DiagnosticSeverity.Error, true, 0, false,
                            location: Location.Create(typeDeclarationSyntax.SyntaxTree, methodSyntax.DeclaringSyntaxReferences[0].Span)));
                        continue;
                    }
                    if (methodSyntax.Parameters.Length == 2 && !SyntaxHelper.IsSameFullName(methodSyntax.Parameters[1].Type, "System.Threading.CancellationToken"))
                    {
                        context.ReportDiagnostic(Diagnostic.Create("QUICRPC008", "QuicRpc", "如果QuicRpcClient服务方法有两个参数，则第二个参数类型必须为\"CancellationToken\"。", DiagnosticSeverity.Error,
                            DiagnosticSeverity.Error, true, 0, false,
                            location: Location.Create(typeDeclarationSyntax.SyntaxTree, methodSyntax.DeclaringSyntaxReferences[0].Span)));
                        continue;
                    }
                    if (!SyntaxHelper.IsSameFullName(methodSyntax.ReturnType, "System.Threading.Tasks.Task"))
                    {
                        context.ReportDiagnostic(Diagnostic.Create("QUICRPC009", "QuicRpc", "QuicRpcClient服务方法返回类型必须是'Task'或'Task<>'。", DiagnosticSeverity.Error,
                            DiagnosticSeverity.Error, true, 0, false,
                            location: Location.Create(typeDeclarationSyntax.SyntaxTree, methodSyntax.ReturnType.DeclaringSyntaxReferences[0].Span)));
                        continue;
                    }

                    bool isStreaming;
                    if (functionAttribute.NamedArguments.Length != 0 && functionAttribute.NamedArguments[0].Value.Value!.Equals(true))
                    {
                        if (methodSyntax.Parameters.Length != 0 && !SyntaxHelper.IsSameFullName(methodSyntax.Parameters[0].Type, "System.Threading.CancellationToken"))
                        {
                            context.ReportDiagnostic(Diagnostic.Create("QUICRPC004", "QuicRpc", "QuicRpc流式服务方法的参数数量必须为空或为CancellationToken。", DiagnosticSeverity.Error,
                                DiagnosticSeverity.Error, true, 0, false,
                                location: Location.Create(typeDeclarationSyntax.SyntaxTree, methodSyntax.Parameters[0].DeclaringSyntaxReferences[0].Span)));
                            continue;
                        }
                        else if (methodSyntax.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) != "global::System.Threading.Tasks.Task<global::System.Net.Quic.QuicStream>")
                        {
                            context.ReportDiagnostic(Diagnostic.Create("QUICRPC004", "QuicRpc", "QuicRpc流式服务方法返回类型必须是Task<QuicStream>。", DiagnosticSeverity.Error,
                                DiagnosticSeverity.Error, true, 0, false,
                                location: Location.Create(typeDeclarationSyntax.SyntaxTree, methodSyntax.ReturnType.DeclaringSyntaxReferences[0].Span)));
                            continue;
                        }
                        isStreaming = true;
                    }
                    else
                        isStreaming = false;
                    var functionId = (byte)functionAttribute.ConstructorArguments[0].Value!;
                    if (functions.ContainsKey(functionId))
                    {
                        context.ReportDiagnostic(Diagnostic.Create("QUICRPC005", "QuicRpc", $"服务方法ID\"{functionId}\"已被{functions[functionId].Method.Name}使用。", DiagnosticSeverity.Error,
                            DiagnosticSeverity.Error, true, 0, false,
                            location: Location.Create(typeDeclarationSyntax.SyntaxTree, functionAttribute.ApplicationSyntaxReference!.Span)));
                        continue;
                    }
                    var parameter = methodSyntax.Parameters.FirstOrDefault();
                    var cancellationParameter = methodSyntax.Parameters.LastOrDefault();
                    if (parameter != null)
                    {
                        if (parameter.RefKind != RefKind.None)
                        {
                            context.ReportDiagnostic(Diagnostic.Create("QUICRPC010", "QuicRpc", "服务方法的参数不能带有关键字。", DiagnosticSeverity.Error,
                                DiagnosticSeverity.Error, true, 0, false,
                                location: Location.Create(typeDeclarationSyntax.SyntaxTree, parameter.DeclaringSyntaxReferences[0].Span)));
                            continue;
                        }
                        if (SyntaxHelper.IsSameFullName(parameter.Type, "System.Threading.CancellationToken"))
                            parameter = null;
                    }
                    if (cancellationParameter != null)
                    {
                        if (cancellationParameter.RefKind != RefKind.None)
                        {
                            context.ReportDiagnostic(Diagnostic.Create("QUICRPC010", "QuicRpc", "服务方法的参数不能带有关键字。", DiagnosticSeverity.Error,
                                DiagnosticSeverity.Error, true, 0, false,
                                location: Location.Create(typeDeclarationSyntax.SyntaxTree, cancellationParameter.DeclaringSyntaxReferences[0].Span)));
                            continue;
                        }
                        if (!SyntaxHelper.IsSameFullName(cancellationParameter.Type, "System.Threading.CancellationToken"))
                            cancellationParameter = null;
                    }
                    functions.Add(functionId, new RpcFunction
                    {
                        ReturnType = isStreaming ? null : ((INamedTypeSymbol)methodSyntax.ReturnType).TypeArguments.FirstOrDefault(),
                        Method = methodSyntax,
                        Parameter = parameter,
                        CancellationTokenParameter = cancellationParameter,
                        IsStreaming = isStreaming
                    });
                }
            }

            if (functions.Count == 0)
                return;
            var builder = new StringBuilder();
            builder.AppendLine("// QuicRpc auto generated.");
            builder.AppendLine();
            builder.AppendLine($"namespace {structType.ContainingNamespace}");
            builder.AppendLine("{");
            builder.AppendLine($"    {typeDeclarationSyntax.Modifiers} struct {structType.Name} : global::Wodsoft.QuicRpc.IQuicRpcClient");
            builder.AppendLine("    {");
            builder.AppendLine("        [global::System.ComponentModel.Browsable(false)]");
            builder.AppendLine("        private global::Wodsoft.QuicRpc.QuicRpcService _quicRpcService;");
            builder.AppendLine("        [global::System.ComponentModel.Browsable(false)]");
            builder.AppendLine("        private global::System.Net.Quic.QuicConnection _quicConnection;");
            builder.AppendLine();
            builder.AppendLine("        void global::Wodsoft.QuicRpc.IQuicRpcClient.Bind(global::Wodsoft.QuicRpc.QuicRpcService quicRpcService, global::System.Net.Quic.QuicConnection connection)");
            builder.AppendLine("        {");
            builder.AppendLine("            _quicRpcService = quicRpcService;");
            builder.AppendLine("            _quicConnection = connection;");
            builder.AppendLine("        }");
            foreach (var function in functions)
            {
                builder.AppendLine();
                if (function.Value.Parameter == null)
                {
                    if (function.Value.IsStreaming)
                    {
                        builder.Append($"        public partial async global::System.Threading.Tasks.Task<global::System.Net.Quic.QuicStream> {function.Value.Method.Name}(");
                    }
                    else if (function.Value.ReturnType == null)
                    {
                        builder.Append($"        public partial async global::System.Threading.Tasks.Task {function.Value.Method.Name}(");
                    }
                    else
                    {
                        builder.Append($"        public partial async global::System.Threading.Tasks.Task<{function.Value.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}> {function.Value.Method.Name}(");
                    }
                }
                else
                {
                    if (function.Value.ReturnType == null)
                    {
                        builder.Append($"        public partial async global::System.Threading.Tasks.Task {function.Value.Method.Name}({function.Value.Parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {function.Value.Parameter.Name}");
                    }
                    else
                    {
                        builder.Append($"        public partial async global::System.Threading.Tasks.Task<{function.Value.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}> {function.Value.Method.Name}({function.Value.Parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {function.Value.Parameter.Name}");
                    }
                }
                if (function.Value.CancellationTokenParameter == null)
                    builder.AppendLine(")");
                else
                {
                    if (function.Value.Parameter == null)
                        builder.AppendLine($"global::System.Threading.CancellationToken {function.Value.CancellationTokenParameter.Name})");
                    else
                        builder.AppendLine($", global::System.Threading.CancellationToken {function.Value.CancellationTokenParameter.Name})");
                }
                builder.AppendLine("        {");
                builder.AppendLine("            if (_quicConnection == null)");
                builder.AppendLine("                throw new global::System.InvalidOperationException(\"Client not bind to any connection.\");");
                builder.Append("            var stream = await _quicConnection.OpenOutboundStreamAsync(global::System.Net.Quic.QuicStreamType.Bidirectional");
                if (function.Value.CancellationTokenParameter == null)
                    builder.Append(")");
                else
                {
                    builder.Append($", {function.Value.CancellationTokenParameter.Name})");
                }
                builder.AppendLine(".ConfigureAwait(false);");
                builder.AppendLine("            try");
                builder.AppendLine("            {");
                if (function.Value.Parameter == null)
                {
                    if (function.Value.IsStreaming)
                    {
                        builder.Append($"                return await _quicRpcService.InvokeStreamingFunctionAsync(stream, {classId + function.Key}");
                    }
                    else if (function.Value.ReturnType == null)
                    {
                        builder.Append($"                await _quicRpcService.InvokeFunctionAsync(stream, {classId + function.Key}");
                    }
                    else
                    {
                        builder.Append($"                return await _quicRpcService.InvokeFunctionAsync<{function.Value.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>(stream, {classId + function.Key}");
                    }
                }
                else
                {
                    if (function.Value.ReturnType == null)
                    {
                        builder.Append($"                await _quicRpcService.InvokeFunctionAsync<{function.Value.Parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>(stream, {classId + function.Key}, {function.Value.Parameter.Name}");
                    }
                    else
                    {
                        builder.Append($"                return await _quicRpcService.InvokeFunctionAsync<{function.Value.Parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}, {function.Value.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>(stream, {classId + function.Key}, {function.Value.Parameter.Name}");
                    }
                }
                if (function.Value.CancellationTokenParameter == null)
                    builder.Append(")");
                else
                {
                    builder.Append($", {function.Value.CancellationTokenParameter.Name})");
                }
                builder.AppendLine(".ConfigureAwait(false);");
                builder.AppendLine("            }");
                builder.AppendLine("            finally");
                builder.AppendLine("            {");
                builder.AppendLine("                await stream.DisposeAsync().ConfigureAwait(false);");
                builder.AppendLine("            }");
                builder.AppendLine("        }");
            }
            builder.AppendLine("    }");
            builder.AppendLine("}");
            var filename = $"{structType.ContainingNamespace}.{structType.Name}.g.cs";
            context.AddSource(filename, builder.ToString());
        }

        private struct RpcFunction
        {
            public ITypeSymbol? ReturnType;

            public IMethodSymbol Method;

            public IParameterSymbol? Parameter;

            public IParameterSymbol? CancellationTokenParameter;

            public bool IsStreaming;
        }
    }
}
