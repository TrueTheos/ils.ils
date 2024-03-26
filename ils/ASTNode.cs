using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils
{
    public abstract class ASTNode { }

    public abstract class ASTStatement { }

    public class ASTVariableDeclaration : ASTStatement
    {
        public string name;
        public TokenType type;
        public ASTExpression value;

        public ASTVariableDeclaration(Token name, Token type, ASTExpression value)
        {
            this.name = name.value;
            this.type = type.tokenType;
            this.value = value;
        }

        private bool CheckTypeAndValue()
        {
            return true;
        }
    }

    public abstract class ASTExpression : ASTNode { }
    
    public class ASTIdentifier : ASTExpression
    {
        public string name;

        public ASTIdentifier(Token token)
        {
            this.name = token.value;
        }
    }
    
    public class ASTIntLiteral : ASTExpression 
    {
        public int value;

        public ASTIntLiteral(string value)
        {
            this.value = Int32.Parse(value);
        }
    }

    public class ASTArithmeticOperation : ASTExpression
    {
        public ASTExpression leftNode;
        public ASTExpression rightNode;
        public Token operation;

        public ASTArithmeticOperation(ASTExpression leftNode, ASTExpression rightNode, Token operation)
        {
            this.leftNode = leftNode;
            this.rightNode = rightNode;
            this.operation = operation;
        }
    }
}
