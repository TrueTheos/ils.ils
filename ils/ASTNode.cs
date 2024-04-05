using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils
{
    public abstract class ASTNode { }

    public abstract class ASTStatement { }

    public class ASTVariableDeclaration(Token name, Token type, ASTExpression value) : ASTStatement
    {
        public Token name = name;
        public TokenType type = type.tokenType;
        public ASTExpression value = value;
    }

    public abstract class ASTExpression : ASTNode { }

    public enum ScopeType { DEFAULT, IF, LOOP }
    public class ASTScope(List<ASTStatement> statements, ASTScope parentScope, ScopeType scopeType) : ASTStatement
    {
        public List<ASTStatement> statements = statements;
        public ASTScope parentScope = parentScope;
        public ScopeType scopeType = scopeType;

        public List<ASTScope> GetChildScopes()
        {
            return statements.OfType<ASTScope>().ToList();
        }

        public List<T> GetStatementsOfType<T>() where T : ASTStatement
        {
            return statements.OfType<T>().ToList();
        }     
    }
    
    public class ASTAssign(Token identifier, ASTExpression value) : ASTStatement
    {
        public Token identifier = identifier;
        public ASTExpression value = value;
    }

    public class ASTIdentifier(Token token) : ASTExpression
    {
        public string name = token.value;
    }

    public class ASTIntLiteral(string value) : ASTExpression 
    {
        public int value = Int32.Parse(value);
    }

    public class ASTStringLiteral(string value) : ASTExpression
    {
        public int[] value = value.Select(c => int.Parse(c.ToString())).ToArray();
    }

    public class ASTCharLiteral(string value) : ASTExpression
    {
        public int value = value[0];
    }

    public class ASTBoolLiteral(string value) : ASTExpression
    {
        public int value = value == "true" ? 1 : 0;
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

    public class ASTBreak(ASTScope scope) : ASTStatement
    {
        public ASTScope scope = scope;
    }

    public class ASTWhile(ASTCondition cond, ASTScope scope) : ASTStatement
    {
        public ASTCondition cond = cond;
        public ASTScope scope = scope;
    }

    public class ASTIf(ASTCondition cond, ASTScope scope, ASTIfPred pred) : ASTStatement
    {
        public ASTCondition cond = cond;
        public ASTScope scope = scope;
        public ASTIfPred pred = pred;
    }

    public abstract class ASTIfPred : ASTStatement { }

    public class ASTElifPred(ASTCondition cond, ASTScope scope, ASTIfPred pred) : ASTIfPred
    {
        public ASTCondition cond = cond;
        public ASTScope scope = scope;
        public ASTIfPred pred = pred;
    }

    public class ASTElsePred(ASTScope scope) : ASTIfPred
    {
        public ASTScope scope = scope;
    }

    public class ASTCondition : ASTNode
    {
        public ASTExpression leftNode;
        public enum ConditionType { EQUAL, NOT_EQUAL, LESS, LESS_EQUAL, GREATER, GREATER_EQUAL }
        public ConditionType conditionType;

        public ASTExpression rightNode;

        public ASTCondition(ASTExpression leftNode, Token conditionSymbol, ASTExpression rightNode)
        {
            this.leftNode = leftNode;

            switch(conditionSymbol?.tokenType)
            {
                case TokenType.EQUALS: this.conditionType = ConditionType.EQUAL; break;
                case TokenType.NOT_EQUAL: this.conditionType = ConditionType.NOT_EQUAL; break;
                case TokenType.LESS: this.conditionType = ConditionType.LESS; break;
                case TokenType.GREATER: this.conditionType = ConditionType.GREATER; break;
                case TokenType.LESS_EQUAL: this.conditionType = ConditionType.LESS_EQUAL; break;
                case TokenType.GREATER_EQUAL: this.conditionType = ConditionType.GREATER_EQUAL; break;
            }

            this.rightNode = rightNode;
        }
    }

}
