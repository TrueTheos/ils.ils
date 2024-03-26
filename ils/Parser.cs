using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils
{
    public class Parser
    {
        int _index = 0;
        List<Token> _tokens = new List<Token>();

        List<ASTStatement> _statements = new();

        public List<ASTStatement> Parse(List<Token> tokens) 
        {
            _tokens = tokens;

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

            return _statements;
        }

        private ASTExpression ParseExpression(int minPrec = 0)
        {
            ASTExpression leftNode = null;

            if(TryConsume(TokenType.LITERAL_INT) is Token intLiteral && intLiteral != null)
            {
                leftNode = new ASTIntLiteral(intLiteral.value);
            }
            else if(TryConsume(TokenType.IDENTIFIER) is Token identifier && identifier != null)
            {
                leftNode = new ASTIdentifier(identifier);
            }

            if(leftNode == null) 
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
            }

            return null;
        }

        private int GetArithmeticOperationPrec(TokenType type)
        {
            switch(type)
            {
                case TokenType.MINUS:
                case TokenType.PLUS:
                    return 0;
                case TokenType.SLASH:
                case TokenType.STAR:
                    return 1;
            }

            return -1;
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
