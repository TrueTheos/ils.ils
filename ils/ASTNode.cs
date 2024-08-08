using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        public Token Identifier = identifier;
        public ASTExpression Value = value;
    }

    public record ASTIdentifier(Token token) : ASTExpression
    {
        public string Name = token.Value;
    }

    public abstract record ASTLiteral : ASTExpression
    {
        public string Value;
        public Type VariableType;
    }

    public record ASTIntLiteral : ASTLiteral
    {
        public ASTIntLiteral(string value) { Value = value; VariableType = TypeSystem.Types[DataType.INT]; }
    }

    public record ASTStringLiteral : ASTLiteral
    {
        public ASTStringLiteral(string value) { Value = value; VariableType = TypeSystem.Types[DataType.STRING]; ; }
    }

    public record ASTCharLiteral : ASTLiteral
    {
        public ASTCharLiteral(string value) { Value = ((int)value[0]).ToString(); VariableType = TypeSystem.Types[DataType.CHAR]; ; }
    }

    public record ASTBoolLiteral : ASTLiteral
    {
        public ASTBoolLiteral(string value) { Value = value == "true" ? "1" : "0"; VariableType = TypeSystem.Types[DataType.BOOL]; ; }
    }

    public record ASTArrayConstructor : ASTExpression
    {
        public List<ASTExpression> Values;
        public TypeSystem.Type Type;
        public int Length;
        public ASTArrayConstructor(List<ASTExpression> vals, TypeSystem.Type type, int length) { Values = vals; Type = type; Length = length; }
    }

    public record ASTArrayIndex : ASTExpression
    {
        public ASTExpression Index;
        public Token Identifier;
        public ASTArrayIndex(ASTExpression index, Token identifier) { Index = index; Identifier = identifier; }
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
        public ASTScope Scope;
        public ASTBreak(ASTScope scope) { Scope = scope; }
    }

    public record ASTWhile(ASTLogicCondition cond, ASTScope scope) : ASTStatement
    {
        public ASTLogicCondition Condition = cond;
        public ASTScope Scope = scope;
    }

    public record ASTIf(ASTLogicCondition cond, ASTScope scope, ASTIfPred pred) : ASTStatement
    {
        public ASTLogicCondition Condition = cond;
        public ASTScope Scope = scope;
        public ASTIfPred Pred = pred;
    }

    public abstract record ASTIfPred : ASTStatement { }

    public record ASTElifPred(ASTLogicCondition cond, ASTScope scope, ASTIfPred pred) : ASTIfPred
    {
        public ASTLogicCondition Condition = cond;
        public ASTScope Scope = scope;
        public ASTIfPred Pred = pred;
    }

    public record ASTElsePred(ASTScope scope) : ASTIfPred
    {
        public ASTScope Scope = scope;
    }

    public record ASTCondition : ASTStatement
    {
        public ASTExpression LeftNode;
        public ConditionType ConditionType;
        public ASTExpression RightNode;

        public ASTCondition(ASTExpression leftNode, Token conditionSymbol, ASTExpression rightNode)
        {
            LeftNode = leftNode;

            switch(conditionSymbol?.TokenType)
            {
                case TokenType.EQUALS: ConditionType = ConditionType.EQUAL; break;
                case TokenType.NOT_EQUAL: ConditionType = ConditionType.NOT_EQUAL; break;
                case TokenType.LESS: ConditionType = ConditionType.LESS; break;
                case TokenType.GREATER: ConditionType = ConditionType.GREATER; break;
                case TokenType.LESS_EQUAL: ConditionType = ConditionType.LESS_EQUAL; break;
                case TokenType.GREATER_EQUAL: ConditionType = ConditionType.GREATER_EQUAL; break;
            }

            RightNode = rightNode;
        }
    }

    public record ASTLogicCondition : ASTStatement
    {
        public ASTStatement LeftNode;
        public TokenType LogicContitionType;
        public ASTStatement RightNode;

        public ASTLogicCondition(ASTStatement leftNode, TokenType logicContitionType, ASTStatement rightNode)
        {
            LeftNode = leftNode;

            LogicContitionType = logicContitionType;

            RightNode = rightNode;
        }
    }

    public record ASTArrayDeclaration : ASTExpression
    {
        public List<ASTExpression> InitialValues;

        public ASTArrayDeclaration(List<ASTExpression> initialValues)
        {
            this.InitialValues = initialValues;
        }
    }
}
