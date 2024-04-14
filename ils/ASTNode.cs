using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ils.IRGenerator;

namespace ils
{
    public abstract class ASTStatement { }

    public class ASTVariableDeclaration(Token name, Token type, ASTExpression value) : ASTStatement
    {
        public Token name = name;
        public TokenType type = type.tokenType;
        public ASTExpression value = value;
    }

    public abstract class ASTExpression : ASTStatement { }

    public enum ScopeType { DEFAULT, IF, LOOP, FUNCTION }
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
            List<T> r = statements.OfType<T>().ToList();
            return r;
        }     
    }
    
    public class ASTFunction : ASTStatement
    {
        public Token identifier;
        public List<ASTVariableDeclaration> parameters;
        public Token returnType;
        public ASTScope scope;
        public ASTReturn returnNode;

        public ASTFunction(Token identifier, List<ASTVariableDeclaration> parameters, Token returnType, ASTScope scope, ASTReturn returnNode)
        {
            this.identifier = identifier;
            this.parameters = parameters;
            this.returnType = returnType;
            this.scope = scope;
            this.returnNode = returnNode;
        }
    }

    public class ASTFunctionCall : ASTExpression
    {
        public Token identifier;
        public List<ASTExpression> arguemnts;
        public ASTFunctionCall(Token identifier, List<ASTExpression> arguments)
        {
            this.identifier = identifier;
            this.arguemnts = arguments;
        }
    }

    public class ASTReturn : ASTExpression
    {
        public ASTExpression value;

        public  ASTReturn(ASTExpression value) 
        {
            this.value = value;
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

    public abstract class ASTLiteral : ASTExpression
    {
        public string value;
        public VariableType variableType;
    }

    public class ASTIntLiteral : ASTLiteral
    {
        public ASTIntLiteral(string value) { this.value = value; variableType = VariableType.INT; }
    }

    public class ASTStringLiteral : ASTLiteral
    {
        public ASTStringLiteral(string value) { this.value = value; variableType = VariableType.STRING; }
    }

    public class ASTCharLiteral : ASTLiteral
    {
        public ASTCharLiteral(string value) { this.value = ((int)value[0]).ToString(); variableType = VariableType.CHAR; }
    }

    public class ASTBoolLiteral : ASTLiteral
    {
        public ASTBoolLiteral(string value) { this.value = value == "true" ? "1" : "0"; variableType = VariableType.BOOL; }
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
            switch (operation.tokenType)
            {
                case TokenType.PLUS:
                    this.operation = ArithmeticOpType.ADD;
                    break;
                case TokenType.MINUS:
                    this.operation = ArithmeticOpType.SUB;
                    break;
                case TokenType.SLASH:
                    this.operation = ArithmeticOpType.DIV;
                    break;
                case TokenType.STAR:
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

    public class ASTCondition : ASTStatement
    {
        public ASTExpression leftNode;
        public enum ConditionType { EQUAL, NOT_EQUAL, LESS, LESS_EQUAL, GREATER, GREATER_EQUAL, NONE }
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
