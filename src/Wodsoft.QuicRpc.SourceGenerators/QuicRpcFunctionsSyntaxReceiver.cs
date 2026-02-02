using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace Wodsoft.QuicRpc.SourceGenerators
{
    public class QuicRpcFunctionsSyntaxReceiver : ISyntaxReceiver
    {
        public List<ClassDeclarationSyntax> Functions { get; } = new List<ClassDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode.Language == "C#" && syntaxNode is ClassDeclarationSyntax classDeclarationSyntax && classDeclarationSyntax.BaseList != null)
            {
                foreach (var typeSyntax in classDeclarationSyntax.BaseList.Types)
                {
                    if (SyntaxHelper.IsSameFullName(typeSyntax.Type, "Wodsoft.QuicRpc.QuicRpcFunctions", false))
                    {
                        Functions.Add(classDeclarationSyntax);
                        return;
                    }
                }
            }
        }
    }
}
