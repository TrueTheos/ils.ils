using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ils
{
    public class Parser
    {
        int _index = 0;
        List<Token> _tokens = new List<Token>();

        List<ASTStatement> _statements = new();

        ASTScope _mainScope;

        ASTScope _currentScope;

        public ASTScope Parse(List<Token> tokens) 
        {
            _tokens = tokens;
            _mainScope = new(null, null);
            _currentScope = _mainScope;

            while(CanPeek())
            {
                if(ParseStatement() is ASTStatement statement && statement != null)
                {
                    _statements.Add(statement);
                }
                else
                {
                    ErrorHandler.Expected("statement", Peek().line);
                    return null;
                }
            }

            _mainScope.statements = _statements;
            return _mainScope;
        }

        private ASTExpression ParseExpressionNode()
        {
            if (TryConsume(TokenType.LITERAL_INT) is Token intLiteral && intLiteral != null)
            {
                return new ASTIntLiteral(intLiteral.value);
            }
            if (TryConsume(TokenType.IDENTIFIER) is Token identifier && identifier != null)
            {
                return new ASTIdentifier(identifier);
            }
            if (TryConsume(TokenType.QUOTATION) != null)
            {
                if (TryConsume(TokenType.LITERAL_STR) is Token strLiteral && strLiteral != null)
                {
                    if(TryConsume(TokenType.QUOTATION) != null)
                    {
                        return new ASTStringLiteral(strLiteral.value);
                    }
                }
                
            }
            if (TryConsume(TokenType.SINGLE_QUATATION) != null)
            {
                if (TryConsume(TokenType.LITERAL_CHAR) is Token charLiteral && charLiteral != null)
                {
                    if (TryConsume(TokenType.SINGLE_QUATATION) != null)
                    {
                        return new ASTCharLiteral(charLiteral.value);
                    }
                }

            }
            if (TryConsume(TokenType.TRUE) != null)
            {
                return new ASTBoolLiteral("true");
            }
            if (TryConsume(TokenType.FALSE) != null)
            {
                return new ASTBoolLiteral("false");
            }
            if(TryConsume(TokenType.OPEN_PARENTHESIS) != null)
            {
                ASTExpression expr = ParseExpression();
                if(expr == null) 
                {
                    ErrorHandler.Expected("expression", Peek().line);
                    return null;
                } 
                TryConsumeErr(TokenType.CLOSE_PARENTHESIS);
                return expr;
            }

            return null;
        }

        private ASTExpression ParseExpression(int minPrec = 0)
        {
            ASTExpression leftNode = ParseExpressionNode();

            if (leftNode == null) 
            {
                ErrorHandler.Expected("expression", Peek().line);
                return null;
            }

            while(true)
            {
                Token currentToken = Peek();
                int prec = 0;

                if(currentToken != null)
                {
                    prec = GetArithmeticOperationPrec(currentToken.tokenType);
                    if(prec == -1 || prec < minPrec)
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }

                Token nextToken = Consume();

                int nextMinPrec = prec + 1;
                ASTExpression rightNode = ParseExpression(nextMinPrec);

                if(rightNode == null) 
                {
                    ErrorHandler.Expected("expression", nextToken.line);
                    return null;
                }

                ASTArithmeticOperation arithmeticOp;
                arithmeticOp = new ASTArithmeticOperation(leftNode, rightNode, currentToken);

                leftNode = arithmeticOp;
            }

            return leftNode;
        }

        private ASTScope ParseScope(ASTScope parentScope)
        {
            if(TryConsume(TokenType.OPEN_CURLY) == null)
            {
                return null;
            }

            List<ASTStatement> statements = new();

            ASTScope scope = new ASTScope(null, parentScope);
            _currentScope = scope;
            while(ParseStatement() is ASTStatement stmt && stmt != null)
            {
                statements.Add(stmt);
            }

            TryConsumeErr(TokenType.CLOSE_CURLY);

            scope.statements = statements;
            _currentScope = parentScope;
            return scope;
        }

        private ASTCondition ParseCondition()
        {
            TryConsumeErr(TokenType.OPEN_PARENTHESIS);

            ASTExpression leftNode = ParseExpression();

            if (leftNode == null)
            {
                ErrorHandler.Expected("condition", Peek().line);
                return null;
            }

            Token conditionType = Peek();
            TokenType type = conditionType.tokenType;
            if(type == TokenType.EQUALS || type == TokenType.NOT_EQUAL || type == TokenType.GREATER 
                || type == TokenType.GREATER_EQUAL || type == TokenType.LESS || type == TokenType.LESS_EQUAL)
            {
                Consume();

                ASTExpression rightNode = ParseExpression();

                if (rightNode == null)
                {
                    ErrorHandler.Expected("condition", Peek().line);
                    return null;
                }

                TryConsumeErr(TokenType.CLOSE_PARENTHESIS);

                return new ASTCondition(leftNode, conditionType, rightNode);
            }
            else
            {
                TryConsumeErr(TokenType.CLOSE_PARENTHESIS);

                return new ASTCondition(leftNode, null, null);
            }
        }

        private ASTIfPred ParseIfPred()
        {
            if(TryConsume(TokenType.ELIF) != null)
            {
                ASTCondition cond = ParseCondition();

                if (cond == null)
                {
                    ErrorHandler.Expected("condition", Peek().line);
                    return null;
                }

                ASTScope scope = ParseScope(_currentScope);

                if (scope == null)
                {
                    ErrorHandler.Expected("scope", Peek().line);
                    return null;
                }

                ASTIfPred pred = ParseIfPred();

                return new ASTElifPred(cond, scope, pred);
            }
            if(TryConsume(TokenType.ELSE) != null)
            {
                ASTScope scope = ParseScope(_currentScope);

                if (scope == null)
                {
                    ErrorHandler.Expected("scope", Peek().line);
                    return null;
                }

                return new ASTElsePred(scope);
            }

            return null;
        }

        private ASTStatement ParseStatement()
        {
            if(Expect(TokenType.IDENTIFIER))
            {
                if(Expect(TokenType.COLON, 1))
                {
                    Token identifier = Consume();
                    Consume();
                    Token variableType = Consume();

                    switch(variableType.tokenType) 
                    {
                        case TokenType.TYPE_STRING:
                        case TokenType.TYPE_CHAR:
                        case TokenType.TYPE_BOOLEAN:
                        case TokenType.TYPE_INT:
                            break;
                        default:
                            ErrorHandler.Expected("variable type", variableType.line);
                            break;
                    }

                    ASTExpression value = null;

                    if(TryConsume(TokenType.ASSIGN) != null)
                    {
                        ASTExpression expr = ParseExpression();
                        if(expr != null)
                        {
                            value = expr;
                        }
                        else
                        {
                            switch (variableType.tokenType)
                            {
                                case TokenType.TYPE_STRING:
                                    ErrorHandler.Expected("string", variableType.line);
                                    break;
                                case TokenType.TYPE_CHAR:
                                    ErrorHandler.Expected("char", variableType.line);
                                    break;
                                case TokenType.TYPE_BOOLEAN:
                                    ErrorHandler.Expected("bool", variableType.line);
                                    break;
                                case TokenType.TYPE_INT:
                                    ErrorHandler.Expected("int", variableType.line);
                                    break;
                                default:
                                    ErrorHandler.Expected("value", variableType.line);
                                    break;
                            }
                        }

                        TryConsumeErr(TokenType.SEMICOLON);
                    }
                    else
                    {
                        TryConsumeErr(TokenType.SEMICOLON);
                    }

                    return new ASTVariableDeclaration(identifier, variableType, value);
                }
            
                if(Expect(TokenType.ASSIGN, 1))
                {
                    Token identifier = Consume();
                    Consume();

                    ASTExpression expr = ParseExpression();

                    if(expr == null)
                    {
                        ErrorHandler.Expected("expression", Peek().line);
                        return null;
                    }

                    TryConsumeErr(TokenType.SEMICOLON);

                    return new ASTAssign(identifier, expr);
                }
            }

            if(Expect(TokenType.OPEN_CURLY))
            {
                ASTScope scope = ParseScope(_currentScope);

                if(scope == null)
                {
                    ErrorHandler.Expected("scope", Peek().line);
                    return null;
                }

                return scope;
            }

            if(TryConsume(TokenType.IF) != null)
            {
                ASTCondition cond = ParseCondition();

                if (cond == null)
                {
                    ErrorHandler.Expected("condition", Peek().line);
                    return null;
                }

                ASTScope scope = ParseScope(_currentScope);

                if (scope == null)
                {
                    ErrorHandler.Expected("scope", Peek().line);
                    return null;
                }

                ASTIfPred pred = ParseIfPred();
                return new ASTIf(cond, scope, pred);
            }
            return null;
        }

        private int GetArithmeticOperationPrec(TokenType type)
        {
            return type switch
            {
                TokenType.MINUS or TokenType.PLUS => 0,
                TokenType.SLASH or TokenType.STAR => 1,
                _ => -1,
            };
        }

        private Token Peek(int offset = 0)
        {
            if(_index + offset >= _tokens.Count)
            {
                return null;
            }
            return _tokens[_index + offset];
        }

        private Token Consume()
        {
            return _tokens[_index++];
        }

        private bool CanPeek()
        {
            return _index < _tokens.Count && _tokens[_index] != null;
        }

        private bool Expect(TokenType type, int offset = 0)
        {
            return CanPeek() && Peek(offset).tokenType == type;
        }

        private Token TryConsumeErr(TokenType type) 
        {
            if(Expect(type))
            {
                return Consume();
            }
            ErrorHandler.Expected(type.ToString(), Peek(-1).line);
            return null;
        }

        private Token TryConsume(TokenType type)
        {
            if(Expect(type))
            {
                return Consume();
            }
            return null;
        }
    }
}
