using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Wodsoft.QuicRpc.SourceGenerators
{
    public class QuicRpcClientSyntaxReceiver : ISyntaxReceiver
    {
        public List<TypeDeclarationSyntax> Clients { get; } = new List<TypeDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode.Language == "C#" && syntaxNode is TypeDeclarationSyntax typeDeclarationSyntax)
            {
                if (typeDeclarationSyntax.BaseList != null)
                {
                    foreach (var typeSyntax in typeDeclarationSyntax.BaseList.Types)
                    {
                        if (SyntaxHelper.IsSameFullName(typeSyntax.Type, "Wodsoft.QuicRpc.IQuicRpcClient", false))
                        {
                            Clients.Add(typeDeclarationSyntax);
                            return;
                        }
                    }
                }
            }
        }
    }
}
