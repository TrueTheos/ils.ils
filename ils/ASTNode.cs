using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ils.IRGenerator;
using Type = ils.TypeSystem.Type;

namespace ils
{
    public abstract record ASTNode;

    public abstract record ASTStatement : ASTNode;

    public record ASTVariableDeclaration : ASTStatement
    {
        public Token Name { get; init; }
        public Type Type { get; init; }
        public ASTExpression Value { get; init; }

        public ASTVariableDeclaration(Token name, Type type, ASTExpression value)
            => (Name, Type, Value) = (name, type, value);
    }

    public abstract record ASTExpression : ASTStatement;

    public enum ScopeType { DEFAULT, IF, ELIF, ELSE, LOOP, FUNCTION }
    public record ASTScope : ASTStatement
    {
        public List<ASTStatement> Statements { get; set; }
        public ASTScope ParentScope { get; init; }
        public ScopeType ScopeType { get; init; }

        public ASTScope(List<ASTStatement> statements, ASTScope parentScope, ScopeType scopeType)
            => (Statements, ParentScope, ScopeType) = (statements, parentScope, scopeType);

        public IEnumerable<ASTScope> GetChildScopes()
            => Statements.OfType<ASTScope>();

        public IEnumerable<T> GetStatementsOfType<T>() where T : ASTStatement
            => Statements.OfType<T>();
    }

    public record ASTFunction : ASTStatement
    {
        public Token Identifier { get; init; }
        public List<ASTVariableDeclaration> Parameters { get; init; }
        public Type ReturnType { get; init; }
        public ASTScope Scope { get; init; }
        public ASTReturn ReturnNode { get; init; }

        public ASTFunction(Token identifier, List<ASTVariableDeclaration> parameters, Type returnType, ASTScope scope, ASTReturn returnNode)
            => (Identifier, Parameters, ReturnType, Scope, ReturnNode) = (identifier, parameters, returnType, scope, returnNode);
    }


    public record ASTFunctionCall : ASTExpression
    {
        public Token Identifier { get; init; }
        public List<ASTExpression> Arguments { get; init; }

        public ASTFunctionCall(Token identifier, List<ASTExpression> arguments)
            => (Identifier, Arguments) = (identifier, arguments);
    }

    public record ASTReturn : ASTExpression
    {
        public ASTExpression Value { get; init; }

        public ASTReturn(ASTExpression value) => Value = value;
    }


    public record ASTAssign(Token identifier, ASTExpression value) : ASTStatement
    {
        public Token identifier = identifier;
        public ASTExpression value = value;
    }

    public record ASTIdentifier(Token token) : ASTExpression
    {
        public string name = token.Value;
    }

    public abstract record ASTLiteral : ASTExpression
    {
        public string value;
        public Type variableType;
    }

    public record ASTIntLiteral : ASTLiteral
    {
        public ASTIntLiteral(string value) { this.value = value; this.variableType = TypeSystem.Types[DataType.INT]; }
    }

    public record ASTStringLiteral : ASTLiteral
    {
        public ASTStringLiteral(string value) { this.value = value; this.variableType = TypeSystem.Types[DataType.STRING]; ; }
    }

    public record ASTCharLiteral : ASTLiteral
    {
        public ASTCharLiteral(string value) { this.value = ((int)value[0]).ToString(); this.variableType = TypeSystem.Types[DataType.CHAR]; ; }
    }

    public record ASTBoolLiteral : ASTLiteral
    {
        public ASTBoolLiteral(string value) { this.value = value == "true" ? "1" : "0"; this.variableType = TypeSystem.Types[DataType.BOOL]; ; }
    }

    public record ASTArrayConstructor : ASTExpression
    {
        public List<ASTExpression> values;
        public TypeSystem.Type type;
        public ASTArrayConstructor(List<ASTExpression> vals, TypeSystem.Type type) { this.values = vals; this.type = type; }
    }

    public record ASTArrayIndex : ASTStatement
    {
        public ASTExpression index;
        public Token identifier;
        public ASTArrayIndex(ASTExpression index, Token identifier) { this.index = index; this.identifier = identifier; }
    }


    public record ASTArithmeticOperation : ASTExpression
    {
        public ASTExpression LeftNode { get; init; }
        public ASTExpression RightNode { get; init; }
        public ArithmeticOpType Operation { get; init; }

       public ASTArithmeticOperation(ASTExpression leftNode, ASTExpression rightNode, Token operation)
        {
            LeftNode = leftNode;
            RightNode = rightNode;
            Operation = operation.TokenType switch
            {
                TokenType.PLUS => ArithmeticOpType.ADD,
                TokenType.MINUS => ArithmeticOpType.SUB,
                TokenType.SLASH => ArithmeticOpType.DIV,
                TokenType.STAR => ArithmeticOpType.MUL,
                TokenType.PERCENT => ArithmeticOpType.MOD,
                _ => throw new ArgumentException("Invalid arithmetic operation token", nameof(operation))
            };
        }
    }

    public record ASTBreak : ASTStatement
    {
        public ASTScope scope;
        public ASTBreak(ASTScope scope) { this.scope = scope; }
    }

    public record ASTWhile(ASTCondition cond, ASTScope scope) : ASTStatement
    {
        public ASTCondition cond = cond;
        public ASTScope scope = scope;
    }

    public record ASTIf(ASTCondition cond, ASTScope scope, ASTIfPred pred) : ASTStatement
    {
        public ASTCondition cond = cond;
        public ASTScope scope = scope;
        public ASTIfPred pred = pred;
    }

    public abstract record ASTIfPred : ASTStatement { }

    public record ASTElifPred(ASTCondition cond, ASTScope scope, ASTIfPred pred) : ASTIfPred
    {
        public ASTCondition cond = cond;
        public ASTScope scope = scope;
        public ASTIfPred pred = pred;
    }

    public record ASTElsePred(ASTScope scope) : ASTIfPred
    {
        public ASTScope scope = scope;
    }

    public record ASTCondition : ASTStatement
    {
        public ASTExpression leftNode;
        public enum ConditionType { EQUAL, NOT_EQUAL, LESS, LESS_EQUAL, GREATER, GREATER_EQUAL, NONE }
        public ConditionType conditionType;

        public ASTExpression rightNode;

        public ASTCondition(ASTExpression leftNode, Token conditionSymbol, ASTExpression rightNode)
        {
            this.leftNode = leftNode;

            switch(conditionSymbol?.TokenType)
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

    public record ASTArrayDeclaration : ASTExpression
    {
        public List<ASTExpression> initialValues;

        public ASTArrayDeclaration(List<ASTExpression> initialValues)
        {
            this.initialValues = initialValues;
        }
    }
}
