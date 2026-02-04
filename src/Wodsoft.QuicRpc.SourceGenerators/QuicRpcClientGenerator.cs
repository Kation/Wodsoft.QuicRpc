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
    public class QuicRpcClientGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is QuicRpcClientSyntaxReceiver receiver)
            {
                foreach (var typeDeclarationSyntax in receiver.Clients)
                {
                    if (typeDeclarationSyntax is not StructDeclarationSyntax)
                    {
                        context.ReportDiagnostic(Diagnostic.Create("QUICRPC007", "QuicRpc", "IQuicRpcClient only allow implementing by structural.", DiagnosticSeverity.Error,
                            DiagnosticSeverity.Error, true, 0, false,
                            location: Location.Create(typeDeclarationSyntax.SyntaxTree, typeDeclarationSyntax.Identifier.Span)));
                        continue;
                    }
                    var model = context.Compilation.GetSemanticModel(typeDeclarationSyntax.SyntaxTree);
                    var structType = model.GetDeclaredSymbol(typeDeclarationSyntax);
                    if (structType.ContainingType != null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create("QUICRPC006", "QuicRpc", "Nested structural do not support implementing IQuicRpcClient.", DiagnosticSeverity.Error,
                            DiagnosticSeverity.Error, true, 0, false,
                            location: Location.Create(typeDeclarationSyntax.SyntaxTree, typeDeclarationSyntax.Identifier.Span)));
                        continue;
                    }
                    bool isPartial = typeDeclarationSyntax.Modifiers.Any(SyntaxKind.PartialKeyword);
                    //没有partial关键字抛错
                    if (!isPartial)
                    {
                        context.ReportDiagnostic(Diagnostic.Create("QUICRPC001", "QuicRpc", "Structural implementing IQuicRpcClient must have partital keyword.", DiagnosticSeverity.Error,
                            DiagnosticSeverity.Error, true, 0, false,
                            location: Location.Create(typeDeclarationSyntax.SyntaxTree, typeDeclarationSyntax.Identifier.Span)));
                        continue;
                    }
                    var functionAttribute = structType.GetAttributes().FirstOrDefault(t => SyntaxHelper.IsSameFullName(t.AttributeClass, "Wodsoft.QuicRpc.QuicRpcFunctionAttribute"));
                    //必须有QuicRpcFunctionAttribute
                    if (functionAttribute == null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create("QUICRPC002", "QuicRpc", "Structural implementing IQuicRpcClient must have a 'QuicRpcFunctionAttribute'.", DiagnosticSeverity.Error,
                            DiagnosticSeverity.Error, true, 0, false,
                            location: Location.Create(typeDeclarationSyntax.SyntaxTree, typeDeclarationSyntax.Identifier.Span)));
                        continue;
                    }
                    var classId = (byte)functionAttribute.ConstructorArguments[0].Value << 8;


                    Dictionary<byte, RpcFunction> functions = new Dictionary<byte, RpcFunction>();

                    foreach (var methodSyntax in structType.GetMembers().OfType<IMethodSymbol>())
                    {
                        if (methodSyntax.MethodKind == MethodKind.Ordinary && methodSyntax.IsPartialDefinition && methodSyntax.DeclaredAccessibility == Accessibility.Public && !methodSyntax.IsStatic)
                        {
                            if (!methodSyntax.GetAttributes().Any(t => SyntaxHelper.IsSameFullName(t.AttributeClass, "Wodsoft.QuicRpc.QuicRpcFunctionAttribute")))
                                continue;

                            if (methodSyntax.Parameters.Length > 2)
                            {
                                context.ReportDiagnostic(Diagnostic.Create("QUICRPC008", "QuicRpc", "QuicRpcClient function do not support more than two parameters.", DiagnosticSeverity.Error,
                                    DiagnosticSeverity.Error, true, 0, false,
                                    location: Location.Create(typeDeclarationSyntax.SyntaxTree, methodSyntax.DeclaringSyntaxReferences[0].Span)));
                                continue;
                            }
                            if (methodSyntax.Parameters.Length == 2 && !SyntaxHelper.IsSameFullName(methodSyntax.Parameters[1].Type, "System.Threading.CancellationToken"))
                            {
                                context.ReportDiagnostic(Diagnostic.Create("QUICRPC009", "QuicRpc", "If a QuicRpcClient function have two parameters, second parameter type must be \"CancellationToken\".", DiagnosticSeverity.Error,
                                    DiagnosticSeverity.Error, true, 0, false,
                                    location: Location.Create(typeDeclarationSyntax.SyntaxTree, methodSyntax.DeclaringSyntaxReferences[0].Span)));
                                continue;
                            }
                            if (!SyntaxHelper.IsSameFullName(methodSyntax.ReturnType, "System.Threading.Tasks.Task"))
                            {
                                context.ReportDiagnostic(Diagnostic.Create("QUICRPC011", "QuicRpc", "QuicRpcClient function return type must be 'Task' or 'Task<>'.", DiagnosticSeverity.Error,
                                    DiagnosticSeverity.Error, true, 0, false,
                                    location: Location.Create(typeDeclarationSyntax.SyntaxTree, methodSyntax.ReturnType.DeclaringSyntaxReferences[0].Span)));
                                continue;
                            }

                            functionAttribute = methodSyntax.GetAttributes().FirstOrDefault(t => SyntaxHelper.IsSameFullName(t.AttributeClass, "Wodsoft.QuicRpc.QuicRpcFunctionAttribute"));
                            bool isStreaming;
                            if (functionAttribute.NamedArguments.Length != 0 && functionAttribute.NamedArguments[0].Value.Value.Equals(true))
                            {
                                if (methodSyntax.Parameters.Length != 0 && !SyntaxHelper.IsSameFullName(methodSyntax.Parameters[0].Type, "System.Threading.CancellationToken"))
                                {
                                    context.ReportDiagnostic(Diagnostic.Create("QUICRPC004", "QuicRpc", "QuicRpc streaming function parameter count must be empty or CancellationToken.", DiagnosticSeverity.Error,
                                        DiagnosticSeverity.Error, true, 0, false,
                                        location: Location.Create(typeDeclarationSyntax.SyntaxTree, methodSyntax.Parameters[0].DeclaringSyntaxReferences[0].Span)));
                                    continue;
                                }
                                else if (methodSyntax.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) != "global::System.Threading.Tasks.Task<global::System.Net.Quic.QuicStream>")
                                {
                                    context.ReportDiagnostic(Diagnostic.Create("QUICRPC004", "QuicRpc", "QuicRpc streaming function return type must be Task<QuicStream>.", DiagnosticSeverity.Error,
                                        DiagnosticSeverity.Error, true, 0, false,
                                        location: Location.Create(typeDeclarationSyntax.SyntaxTree, methodSyntax.ReturnType.DeclaringSyntaxReferences[0].Span)));
                                    continue;
                                }
                                isStreaming = true;
                            }
                            else
                                isStreaming = false;
                            var functionId = (byte)functionAttribute.ConstructorArguments[0].Value;
                            if (functions.ContainsKey(functionId))
                            {
                                context.ReportDiagnostic(Diagnostic.Create("QUICRPC005", "QuicRpc", $"Function id \"{functionId}\" is used by {functions[functionId].Method.Name}.", DiagnosticSeverity.Error,
                                    DiagnosticSeverity.Error, true, 0, false,
                                    location: Location.Create(typeDeclarationSyntax.SyntaxTree, functionAttribute.ApplicationSyntaxReference.Span)));
                                continue;
                            }
                            var parameter = methodSyntax.Parameters.FirstOrDefault();
                            var cancellationParameter = methodSyntax.Parameters.LastOrDefault();
                            if (parameter != null && SyntaxHelper.IsSameFullName(parameter.Type, "System.Threading.CancellationToken"))
                                parameter = null;
                            if (cancellationParameter != null && !SyntaxHelper.IsSameFullName(cancellationParameter.Type, "System.Threading.CancellationToken"))
                                cancellationParameter = null;
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
                        continue;
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
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new QuicRpcClientSyntaxReceiver());
        }

        private struct RpcFunction
        {
            public ITypeSymbol ReturnType;

            public IMethodSymbol Method;

            public IParameterSymbol Parameter;

            public IParameterSymbol CancellationTokenParameter;

            public bool IsStreaming;
        }
    }
}
