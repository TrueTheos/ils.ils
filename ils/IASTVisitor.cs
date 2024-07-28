using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils
{
    public interface IASTVisitor
    {
        void Visit(ASTVariableDeclaration node);
        void Visit(ASTScope node);
        void Visit(ASTFunction node);
        void Visit(ASTFunctionCall node);
        void Visit(ASTReturn node);
        void Visit(ASTAssign node);
        void Visit(ASTIdentifier node);
        void Visit(ASTLiteral node);
        void Visit(ASTArithmeticOperation node);
        void Visit(ASTBreak node);
        void Visit(ASTWhile node);
        void Visit(ASTIf node);
        void Visit(ASTElifPred node);
        void Visit(ASTElsePred node);
        void Visit(ASTCondition node);
        void Visit(ASTArrayDeclaration node);
    }
}
