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
        public Token name;
        public TokenType type;
        public ASTExpression value;

        public ASTVariableDeclaration(Token name, Token type, ASTExpression value)
        {
            this.name = name;
            this.type = type.tokenType;
            this.value = value;
        }

        private bool CheckTypeAndValue()
        {
            return true;
        }
    }

    public abstract class ASTExpression : ASTNode { }

    public class ASTScope : ASTStatement
    {
        public List<ASTStatement> statements;
        public ASTScope parentScope;

        public ASTScope(List<ASTStatement> statements, ASTScope parentScope)
        {
            this.statements = statements;
            this.parentScope = parentScope;
        }

        public List<ASTScope> GetChildScopes()
        {
            return statements.OfType<ASTScope>().ToList();
        }

        public List<T> GetStatementsOfType<T>() where T : ASTStatement
        {
            return statements.OfType<T>().ToList();
        }
    }
    
    public class ASTAssign : ASTStatement
    {
        public Token identifier;
        public ASTExpression value;

        public ASTAssign(Token identifier, ASTExpression value) 
        {
            this.identifier = identifier;
            this.value = value;
        }
    }

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

    public class ASTStringLiteral : ASTExpression
    {
        public string value;

        public ASTStringLiteral(string value)
        {
            this.value = value;
        }
    }

    public class ASTCharLiteral : ASTExpression
    {
        public char value;

        public ASTCharLiteral(string value)
        {
            this.value = value[0];
        }
    }

    public class ASTBoolLiteral : ASTExpression
    {
        public bool value;

        public ASTBoolLiteral(string value)
        {
            this.value = value == "true" ? true : false;
        }
    }

    public class ASTArithmeticOperation : ASTExpression
    {
        public ASTExpression leftNode;
        public ASTExpression rightNode;
        public ArithmeticOpType operation;

        public ASTArithmeticOperation(ASTExpression leftNode, ASTExpression rightNode, Token operation)
        {
            this.leftNode = leftNode;
            this.rightNode = rightNode;
            switch (operation.value)
            {
                case "+":
                    this.operation = ArithmeticOpType.ADD;
                    break;
                case "-":
                    this.operation = ArithmeticOpType.SUB;
                    break;
                case "/":
                    this.operation = ArithmeticOpType.DIV;
                    break;
                case "*":
                    this.operation = ArithmeticOpType.MUL;
                    break;
            }
        }
    }
}
