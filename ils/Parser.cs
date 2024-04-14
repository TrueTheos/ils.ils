namespace ils
{
    public class Parser
    {
        int _index = 0;
        List<Token> _tokens = new List<Token>();

        List<ASTStatement> _statements = new();

        private List<(string variableName, int line)> variables = new();

        ASTScope _mainScope;

        ASTScope _currentScope;

        public ASTScope Parse(List<Token> tokens)
        {
            _tokens = tokens;
            _mainScope = new(null, null, ScopeType.DEFAULT);
            _currentScope = _mainScope;

            while (CanPeek())
            {
                if (ParseStatement() is ASTStatement statement && statement != null)
                {
                    _statements.Add(statement);
                }
                else
                {
                    ErrorHandler.Expected("statement", Peek());
                    return null;
                }
            }

            Verificator.CheckKeywordUsage(variables);

            _mainScope.statements = _statements;
            return _mainScope;
        }

        private ASTExpression ParseExpressionNode()
        {
            if (TryConsume(TokenType.LITERAL_INT) is Token intLiteral && intLiteral != null)
            {
                return new ASTIntLiteral(intLiteral.value);
            }
            if (Expect(TokenType.IDENTIFIER))
            {
                //todo add function call
                if (Expect(TokenType.OPEN_PARENTHESIS, 1))
                {
                    ASTFunctionCall funcCall = ParseFunctionCall();
                    if (funcCall != null)
                    {
                        return funcCall;
                    }
                }

                return new ASTIdentifier(Consume());
            }
            if (TryConsume(TokenType.QUOTATION) != null)
            {
                if (TryConsume(TokenType.LITERAL_STR) is Token strLiteral && strLiteral != null)
                {
                    if (TryConsume(TokenType.QUOTATION) != null)
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
            if (TryConsume(TokenType.OPEN_PARENTHESIS) != null)
            {
                ASTExpression expr = ParseExpression();
                if (expr == null)
                {
                    ErrorHandler.Expected("expression", Peek());
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
                //ErrorHandler.Expected("expression", Peek().line);
                return null;
            }

            while (true)
            {
                Token currentToken = Peek();
                int prec = 0;

                if (currentToken != null)
                {
                    prec = GetArithmeticOperationPrec(currentToken.tokenType);
                    if (prec == -1 || prec < minPrec)
                    {
                        break;
                    }

                    if (leftNode is ASTCharLiteral)
                    {
                        ErrorHandler.Custom($"[{currentToken.line}] You can't do arithmrtic operations on characters!");
                    }
                    if (leftNode is ASTBoolLiteral)
                    {
                        ErrorHandler.Custom($"[{currentToken.line}] You can't do arithmrtic operations on booleans!");
                    }
                }
                else
                {
                    break;
                }

                Token nextToken = Consume();

                int nextMinPrec = prec + 1;
                ASTExpression rightNode = ParseExpression(nextMinPrec);

                if (rightNode == null)
                {
                    ErrorHandler.Expected("expression", nextToken);
                    return null;
                }

                ASTArithmeticOperation arithmeticOp;
                arithmeticOp = new ASTArithmeticOperation(leftNode, rightNode, currentToken);

                leftNode = arithmeticOp;
            }

            return leftNode;
        }

        private ASTScope ParseScope(ASTScope parentScope, ScopeType scopeType)
        {
            if (TryConsume(TokenType.OPEN_CURLY) == null)
            {
                return null;
            }

            List<ASTStatement> statements = new();

            ASTScope scope = new ASTScope(null, parentScope, scopeType);
            _currentScope = scope;
            while (ParseStatement() is ASTStatement stmt && stmt != null)
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
                ErrorHandler.Expected("condition", Peek());
                return null;
            }

            Token conditionType = Peek();
            TokenType type = conditionType.tokenType;
            if (type == TokenType.EQUALS || type == TokenType.NOT_EQUAL || type == TokenType.GREATER
                || type == TokenType.GREATER_EQUAL || type == TokenType.LESS || type == TokenType.LESS_EQUAL)
            {
                Consume();

                ASTExpression rightNode = ParseExpression();

                if (rightNode == null)
                {
                    ErrorHandler.Expected("condition", Peek());
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
            if (TryConsume(TokenType.ELIF) != null)
            {
                ASTCondition cond = ParseCondition();

                if (cond == null)
                {
                    ErrorHandler.Expected("condition", Peek());
                    return null;
                }

                ASTScope scope = ParseScope(_currentScope, ScopeType.IF);

                if (scope == null)
                {
                    ErrorHandler.Expected("scope", Peek());
                    return null;
                }

                ASTIfPred pred = ParseIfPred();

                return new ASTElifPred(cond, scope, pred);
            }
            if (TryConsume(TokenType.ELSE) != null)
            {
                ASTScope scope = ParseScope(_currentScope, ScopeType.IF);

                if (scope == null)
                {
                    ErrorHandler.Expected("scope", Peek());
                    return null;
                }

                return new ASTElsePred(scope);
            }

            return null;
        }

        private ASTFunction ParseFunction()
        {
            Token identifier = TryConsumeErr(TokenType.IDENTIFIER);
            Token variableType = null;
            TryConsumeErr(TokenType.OPEN_PARENTHESIS);

            List<ASTVariableDeclaration> parameters = new();

            while (Expect(TokenType.IDENTIFIER))
            {
                parameters.Add(ParseVariableDeclaration(isFuncArg: true));
            }

            TryConsumeErr(TokenType.CLOSE_PARENTHESIS);

            if (TryConsume(TokenType.RETURN_TYPE) != null)
            {
                variableType = Consume();

                switch (variableType.tokenType)
                {
                    case TokenType.TYPE_STRING:
                    case TokenType.TYPE_CHAR:
                    case TokenType.TYPE_BOOLEAN:
                    case TokenType.TYPE_INT:
                        break;
                    default:
                        ErrorHandler.Expected("return type", variableType);
                        break;
                }
            }

            ASTScope scope = ParseScope(_currentScope, ScopeType.FUNCTION);

            return new ASTFunction(identifier, parameters, variableType, scope, variableType != null ? scope.GetStatementsOfType<ASTReturn>().Last() : null);
        }

        private ASTVariableDeclaration ParseVariableDeclaration(bool isFuncArg = false)
        {
            Token identifier = Consume();
            Consume();
            Token variableType = Consume();

            switch (variableType.tokenType)
            {
                case TokenType.TYPE_STRING:
                case TokenType.TYPE_CHAR:
                case TokenType.TYPE_BOOLEAN:
                case TokenType.TYPE_INT:
                    break;
                default:
                    ErrorHandler.Expected("variable type", variableType);
                    break;
            }

            ASTExpression value = null;

            if (TryConsume(TokenType.ASSIGN) != null)
            {
                ASTExpression expr = ParseExpression();
                if (expr != null)
                {
                    if (expr is ASTCharLiteral)
                    {
                        if (variableType.tokenType != TokenType.TYPE_CHAR)
                        {
                            ErrorHandler.Expected("char", variableType);
                        }
                    }
                    else if (expr is ASTBoolLiteral)
                    {
                        if (variableType.tokenType != TokenType.TYPE_BOOLEAN)
                        {
                            ErrorHandler.Expected("bool", variableType);
                        }
                    }
                    else if (expr is ASTIntLiteral)
                    {
                        if (variableType.tokenType != TokenType.TYPE_INT)
                        {
                            ErrorHandler.Expected("int ", variableType);
                        }
                    }
                    else if (expr is ASTStringLiteral)
                    {
                        if (variableType.tokenType != TokenType.TYPE_STRING)
                        {
                            ErrorHandler.Expected("string ", variableType);
                        }
                    }

                    value = expr;
                }
                else
                {
                    ErrorHandler.Expected("value", variableType);
                }

                if (isFuncArg)
                {
                    if (Expect(TokenType.IDENTIFIER, 1))
                    {
                        TryConsumeErr(TokenType.COMMA);
                    }
                }
                else
                {
                    TryConsumeErr(TokenType.SEMICOLON);
                }
            }
            else
            {
                if (isFuncArg)
                {
                    if(Expect(TokenType.IDENTIFIER, 1))
                    {
                        TryConsumeErr(TokenType.COMMA);
                    }                
                }
                else
                {
                    TryConsumeErr(TokenType.SEMICOLON);
                }
            }

            variables.Add((identifier.value, identifier.line));
            return new ASTVariableDeclaration(identifier, variableType, value);
        }

        private ASTFunctionCall ParseFunctionCall()
        {
            Token identifier = Consume();
            if(identifier.tokenType != TokenType.IDENTIFIER)
            {
                ErrorHandler.Expected("identifier", identifier);
                return null;
            }
            Consume();

            List<ASTExpression> arguments = new();

            ASTExpression expr = ParseExpression();

            while (expr != null)
            {
                arguments.Add(expr);

                if (TryConsume(TokenType.COMMA) != null)
                {
                    expr = ParseExpression();
                }
                else
                {
                    break;
                }
            }

            TryConsumeErr(TokenType.CLOSE_PARENTHESIS);

            return new ASTFunctionCall(identifier, arguments);
        }

        private ASTStatement ParseStatement()
        {
            if (Expect(TokenType.IDENTIFIER))
            {
                if (Expect(TokenType.COLON, 1))
                {
                    return ParseVariableDeclaration();
                }

                if (Expect(TokenType.ASSIGN, 1))
                {
                    Token identifier = Consume();
                    Consume();

                    ASTExpression expr = ParseExpression();

                    if (expr == null)
                    {
                        ErrorHandler.Expected("expression", Peek());
                        return null;
                    }

                    TryConsumeErr(TokenType.SEMICOLON);

                    return new ASTAssign(identifier, expr);
                }

                if (Expect(TokenType.OPEN_PARENTHESIS, 1))
                {
                    ASTFunctionCall call = ParseFunctionCall();
                    TryConsumeErr(TokenType.SEMICOLON);
                    return call;
                }
            }

            if (Expect(TokenType.OPEN_CURLY))
            {
                ASTScope scope = ParseScope(_currentScope, ScopeType.DEFAULT);

                if (scope == null)
                {
                    ErrorHandler.Expected("scope", Peek());
                    return null;
                }

                return scope;
            }

            if (TryConsume(TokenType.FUNCTION) != null)
            {
                return ParseFunction();
            }

            if (TryConsume(TokenType.IF) != null)
            {
                ASTCondition cond = ParseCondition();

                if (cond == null)
                {
                    ErrorHandler.Expected("condition", Peek());
                    return null;
                }

                ASTScope scope = ParseScope(_currentScope, ScopeType.IF);

                if (scope == null)
                {
                    ErrorHandler.Expected("scope", Peek());
                    return null;
                }

                ASTIfPred pred = ParseIfPred();
                return new ASTIf(cond, scope, pred);
            }

            if (TryConsume(TokenType.RETURN) != null)
            {
                ASTExpression ret = ParseExpression();

                if (ret == null)
                {
                    ErrorHandler.Expected("expression", Peek());
                    return null;
                }

                TryConsumeErr(TokenType.SEMICOLON);

                return new ASTReturn(ret);
            }

            if (TryConsume(TokenType.WHILE) != null)
            {
                ASTCondition cond = ParseCondition();

                if (cond == null)
                {
                    ErrorHandler.Expected("condition", Peek());
                    return null;
                }

                ASTScope scope = ParseScope(_currentScope, ScopeType.LOOP);

                if (scope == null)
                {
                    ErrorHandler.Expected("scope", Peek());
                    return null;
                }

                return new ASTWhile(cond, scope);
            }

            if (TryConsume(TokenType.BREAK) != null)
            {
                if (_currentScope == _mainScope)
                {
                    ErrorHandler.Custom($"[{Peek().line}] You can not break out of main scope!");
                    return null;
                }

                TryConsumeErr(TokenType.SEMICOLON);

                return new ASTBreak(_currentScope);
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
            if (_index + offset >= _tokens.Count)
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
            Token p = Peek(offset);
            bool r = p.tokenType == type;
            bool c = CanPeek();
            return c && r;
        }

        private Token TryConsumeErr(TokenType type)
        {
            if (Expect(type))
            {
                return Consume();
            }
            ErrorHandler.Expected(type.ToString(), Peek(-1));
            return null;
        }

        private Token TryConsume(TokenType type)
        {
            if (Expect(type))
            {
                return Consume();
            }
            return null;
        }
    }
}
