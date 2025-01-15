using static ils.TypeSystem;

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
            _mainScope = new(null, null, ScopeType.MAIN);
            _currentScope = _mainScope;

            while (CanPeek())
            {
                if (ParseStatement() is ASTStatement statement && statement != null)
                {
                    _statements.Add(statement);
                }
                else
                {
                    ErrorHandler.Throw(new ExpectedError("statement", Peek().TokenType.ToString(), Peek().Line));
                    return null;
                }
            }

            Verificator.CheckKeywordUsage(variables);

            _mainScope.Statements = _statements;
            return _mainScope;
        }

        private ASTExpression ParseExpressionNode()
        {
            if (TryConsume(TokenType.LITERAL_INT) is Token intLiteral)
            {
                return new ASTIntLiteral(intLiteral.Value);
            }
            if (TryConsume(TokenType.LITERAL_CHAR) is Token charLiteral)
            {
                return new ASTCharLiteral(charLiteral.Value);
            }
            if (TryConsume(TokenType.LITERAL_STR) is Token strLiteral)
            {
                return new ASTStringLiteral(strLiteral.Value);
            }
            if (TryConsume(TokenType.TYPE_BOOLEAN) is Token boolLiteral)
            {
                return new ASTBoolLiteral(boolLiteral.Value);
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
                else if(Expect(TokenType.OPEN_SQUARE, 1))
                {
                    ASTArrayIndex arrIndex;
                    Token identifier = Consume();

                    Consume();
                    ASTExpression expr = ParseExpression();

                    if (expr == null)
                    {
                        ErrorHandler.Throw(new ExpectedError("expression", Peek().TokenType.ToString(), Peek().Line));
                        return null;
                    }

                    TryConsumeErr(TokenType.CLOSE_SQUARE);

                    return new ASTArrayIndex(expr, identifier);

                }

                return new ASTIdentifier(Consume());
            }
            if (TryConsume(TokenType.QUOTATION) != null)
            {
                if (TryConsume(TokenType.LITERAL_STR) is Token strLit)
                {
                    if (TryConsume(TokenType.QUOTATION) != null)
                    {
                        return new ASTStringLiteral(strLit.Value);
                    }
                }

            }
            if (TryConsume(TokenType.SINGLE_QUATATION) != null)
            {
                if (TryConsume(TokenType.LITERAL_CHAR) is Token charLit)
                {
                    if (TryConsume(TokenType.SINGLE_QUATATION) != null)
                    {
                        return new ASTCharLiteral(charLit.Value);
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
                    ErrorHandler.Throw(new ExpectedError("expression", Peek().TokenType.ToString(), Peek().Line));
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
                    prec = GetArithmeticOperationPrec(currentToken.TokenType);
                    if (prec == -1 || prec < minPrec)
                    {
                        break;
                    }

                    if (leftNode is ASTCharLiteral)
                    {
                        ErrorHandler.Custom($"[{currentToken.Line}] You can't do arithmrtic operations on characters!");
                    }
                    if (leftNode is ASTBoolLiteral)
                    {
                        ErrorHandler.Custom($"[{currentToken.Line}] You can't do arithmrtic operations on booleans!");
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
                    ErrorHandler.Throw(new ExpectedError("expression", nextToken.TokenType.ToString(), nextToken.Line));
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
            while (ParseStatement() is ASTStatement stmt)
            {
                statements.Add(stmt);
            }

            TryConsumeErr(TokenType.CLOSE_CURLY);

            scope.Statements = statements;
            _currentScope = parentScope;
            return scope;
        }

        private ASTCondition ParseCondition()
        {
            ASTExpression leftNode = ParseExpression();

            if (leftNode == null)
            {
                ErrorHandler.Throw(new ExpectedError("condition", Peek().TokenType.ToString(), Peek().Line));
                return null;
            }

            Token conditionType = Peek();
            TokenType type = conditionType.TokenType;
            if (type == TokenType.EQUALS || type == TokenType.NOT_EQUAL || type == TokenType.GREATER
                || type == TokenType.GREATER_EQUAL || type == TokenType.LESS || type == TokenType.LESS_EQUAL)
            {
                Consume();

                ASTExpression rightNode = ParseExpression();

                if (rightNode == null)
                {
                    ErrorHandler.Throw(new ExpectedError("condition", Peek().TokenType.ToString(), Peek().Line));
                    return null;
                }

                //TryConsumeErr(TokenType.CLOSE_PARENTHESIS);

                return new ASTCondition(leftNode, conditionType, rightNode);
            }
            else
            {
                //TryConsumeErr(TokenType.CLOSE_PARENTHESIS);

                return new ASTCondition(leftNode, null, null);
            }
        }

        private ASTLogicCondition ParseLogicCondition()
        {
            TryConsumeErr(TokenType.OPEN_PARENTHESIS);
            ASTLogicCondition logicCondition = new ASTLogicCondition();

            while (true)
            {
                ASTCondition condition = ParseCondition();

                if (condition == null)
                {
                    ErrorHandler.Throw(new ExpectedError("condition", Peek().TokenType.ToString(), Peek().Line));
                    return null;
                }

                logicCondition.AddCondition(condition);

                Token nextToken = Peek();
                if (nextToken.TokenType == TokenType.AND || nextToken.TokenType == TokenType.OR)
                {
                    Consume(); // Consume the logical operator
                    logicCondition.AddCondition(null, nextToken.TokenType); // Add null as a placeholder, it will be replaced in the next iteration
                }
                else if (nextToken.TokenType == TokenType.CLOSE_PARENTHESIS)
                {
                    break;
                }
                else
                {
                    ErrorHandler.Throw(new ExpectedError("AND, OR, or )", nextToken.TokenType.ToString(), nextToken.Line));
                    return null;
                }
            }

            TryConsumeErr(TokenType.CLOSE_PARENTHESIS);
            return logicCondition;
        }

        private ASTIfPred ParseIfPred()
        {
            if (TryConsume(TokenType.ELIF) != null)
            {
                ASTLogicCondition cond = ParseLogicCondition();

                if (cond == null)
                {
                    ErrorHandler.Throw(new ExpectedError("condition", Peek().TokenType.ToString(), Peek().Line));
                    return null;
                }

                ASTScope scope = ParseScope(_currentScope, ScopeType.IF);

                if (scope == null)
                {
                    ErrorHandler.Throw(new ExpectedError("scope", Peek().TokenType.ToString(), Peek().Line));
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
                    ErrorHandler.Throw(new ExpectedError("scope", Peek().TokenType.ToString(), Peek().Line));
                    return null;
                }

                return new ASTElsePred(scope);
            }

            return null;
        }

        private ASTFunction ParseFunction()
        {
            Token identifier = TryConsumeErr(TokenType.IDENTIFIER);
            TypeSystem.Type variableType = null;
            TryConsumeErr(TokenType.OPEN_PARENTHESIS);

            List<ASTVariableDeclaration> parameters = new();

            while (Expect(TokenType.IDENTIFIER))
            {
                parameters.Add(ParseVariableDeclaration(isFuncArg: true));
            }

            TryConsumeErr(TokenType.CLOSE_PARENTHESIS);

            if(TryConsume(TokenType.RETURN_TYPE) != null)
            {
                variableType = ConsumeType();
            }
            /*if (TryConsume(TokenType.RETURN_TYPE) != null)
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
                        ErrorHandler.Throw(new ExpectedError("return type", variableType.tokenType.ToString(), variableType.line));
                        break;
                }
            }*/

            ASTScope scope = ParseScope(_currentScope, ScopeType.FUNCTION);

            if(variableType != null && scope.GetStatementsOfType<ASTReturn>().ToList().Count == 0)
            {
                ErrorHandler.Custom($"Function '{identifier.Value}' doesn't return a value!");
            }

            return new ASTFunction(identifier, parameters, variableType, scope, variableType != null ? scope.GetStatementsOfType<ASTReturn>().Last() : null);
        }

        public TypeSystem.Type ConsumeType()
        {
            Token firsToken = Consume();
            if (firsToken.TokenType == TokenType.TYPE_INT) return TypeSystem.Types[DataType.INT];
            if (firsToken.TokenType == TokenType.TYPE_BOOLEAN) return TypeSystem.Types[DataType.BOOL];
            if (firsToken.TokenType == TokenType.TYPE_STRING) return TypeSystem.Types[DataType.STRING];
            if (firsToken.TokenType == TokenType.TYPE_CHAR) return TypeSystem.Types[DataType.CHAR];

            if(firsToken.TokenType == TokenType.OPEN_SQUARE)
            {
                Token elementType = Consume();
                if (!IsValidElementType(elementType.TokenType))
                {
                    ErrorHandler.Throw(new ExpectedError("valid array element type", elementType.TokenType.ToString(), elementType.Line));
                    return null;
                }

                int size = -1;

                if (TryConsume(TokenType.COMMA) != null)
                {
                    size = Int32.Parse(TryConsumeErr(TokenType.LITERAL_INT).Value);
                }

                TryConsumeErr(TokenType.CLOSE_SQUARE);

                ArrayType arrayType = new();
                arrayType.elementType = TypeSystem.GetTypeFromToken(elementType);
                arrayType.length = size;

                return arrayType;
            }

            ErrorHandler.Throw(new NotExistingType(firsToken.Value, firsToken.Line));
            return null;
        }

        private ASTVariableDeclaration ParseVariableDeclaration(bool isFuncArg = false)
        {
            Token identifier = Consume();
            Consume();
            TypeSystem.Type variableType = ConsumeType();

            ASTExpression value = null;

            if (TryConsume(TokenType.ASSIGN) != null)
            {
                if (variableType.DataType != DataType.ARRAY)
                {
                    ASTExpression expr = ParseExpression();
                    if (expr != null)
                    {
                        if (variableType is PrimitiveType primitiveType && expr is ASTLiteral)
                        {
                            primitiveType.CanAssignLiteral(expr, identifier.Line);
                        }

                        value = expr;
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
                else if(variableType is ArrayType array)
                {
                    TryConsumeErr(TokenType.OPEN_CURLY);
                    List<ASTExpression> initialValues = new List<ASTExpression>();

                    while (!Expect(TokenType.CLOSE_CURLY))
                    {
                        ASTExpression arrayValue = ParseExpression();
                        if (arrayValue == null)
                        {
                            ErrorHandler.Throw(new ExpectedError("expression", Peek().TokenType.ToString(), Peek().Line));
                            return null;
                        }

                        if (!IsValidElementValue(array.elementType.DataType, arrayValue))
                        {
                            ErrorHandler.Throw(new ExpectedError($"{array.elementType.DataType} value", Peek().TokenType.ToString(), Peek().Line));
                            return null;
                        }

                        initialValues.Add(arrayValue);

                        if (TryConsume(TokenType.COMMA) == null)
                        {
                            break;
                        }
                    }

                    if (array.length != -1 && initialValues.Count > array.length) ErrorHandler.Custom($"Wrong amount of elements in array. Provided {initialValues.Count}, expected {array.length}");
                    if (array.length == -1) array.length = initialValues.Count;
                    TryConsumeErr(TokenType.CLOSE_CURLY);
                    TryConsumeErr(TokenType.SEMICOLON);
                    value = new ASTArrayConstructor(initialValues, variableType, array.length);
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

            if(variableType is ArrayType arrayType && value is ASTArrayDeclaration arrayDeclaration)
            {
                if (arrayType.length == -1 && arrayDeclaration.InitialValues != null)
                {
                    arrayType.length = arrayDeclaration.InitialValues.Count;
                }
            }

            variables.Add((identifier.Value, identifier.Line));
            return new ASTVariableDeclaration(identifier, variableType, value);
        }

        private ASTFunctionCall ParseFunctionCall()
        {
            Token identifier = Consume();
            if(identifier.TokenType != TokenType.IDENTIFIER)
            {
                ErrorHandler.Throw(new ExpectedError("identifier", identifier.TokenType.ToString(), identifier.Line));
                return null;
            }
            Consume();

            Token peek = Peek();
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

        private bool IsValidElementType(TokenType type)
        {
            return type == TokenType.TYPE_INT || type == TokenType.TYPE_STRING ||
                   type == TokenType.TYPE_CHAR || type == TokenType.TYPE_BOOLEAN;
        }

        private bool IsValidElementValue(DataType expectedType, ASTExpression value)
        {
            return (expectedType == DataType.INT && value is ASTIntLiteral) ||
                   (expectedType == DataType.STRING && value is ASTStringLiteral) ||
                   (expectedType == DataType.CHAR && value is ASTCharLiteral) ||
                   (expectedType == DataType.BOOL && value is ASTBoolLiteral) ||
                   value is ASTIdentifier || value is ASTFunctionCall ||
                   value is ASTArithmeticOperation;
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
                        ErrorHandler.Throw(new ExpectedError("expression", Peek().TokenType.ToString(), Peek().Line));
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
                ASTScope scope = ParseScope(_currentScope, ScopeType.MAIN);

                if (scope == null)
                {
                    ErrorHandler.Throw(new ExpectedError("scope", Peek().TokenType.ToString(), Peek().Line));
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
                ASTLogicCondition cond = ParseLogicCondition();

                if (cond == null)
                {
                    ErrorHandler.Throw(new ExpectedError("condition", Peek().TokenType.ToString(), Peek().Line));
                    return null;
                }

                ASTScope scope = ParseScope(_currentScope, ScopeType.IF);

                if (scope == null)
                {
                    ErrorHandler.Throw(new ExpectedError("scope", Peek().TokenType.ToString(), Peek().Line));
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
                    ErrorHandler.Throw(new ExpectedError("expression", Peek().TokenType.ToString(), Peek().Line));
                    return null;
                }

                TryConsumeErr(TokenType.SEMICOLON);

                return new ASTReturn(ret);
            }

            if (TryConsume(TokenType.WHILE) != null)
            {
                ASTLogicCondition cond = ParseLogicCondition();

                if (cond == null)
                {
                    ErrorHandler.Throw(new ExpectedError("condition", Peek().TokenType.ToString(), Peek().Line));
                    return null;
                }

                ASTScope scope = ParseScope(_currentScope, ScopeType.LOOP);

                if (scope == null)
                {
                    ErrorHandler.Throw(new ExpectedError("scope", Peek().TokenType.ToString(), Peek().Line));
                    return null;
                }

                return new ASTWhile(cond, scope);
            }

            if (TryConsume(TokenType.BREAK) != null)
            {
                if (_currentScope == _mainScope)
                {
                    ErrorHandler.Custom($"[{Peek().Line}] You can not break out of main scope!");
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
                TokenType.SLASH or TokenType.STAR or TokenType.PERCENT => 1,
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
            bool r = p.TokenType == type;
            bool c = CanPeek();
            return c && r;
        }

        private Token TryConsumeErr(TokenType type)
        {
            if (Expect(type))
            {
                return Consume();
            }
            ErrorHandler.Throw(new ExpectedError(type.ToString(), Peek().TokenType.ToString(), Peek().Line));
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
