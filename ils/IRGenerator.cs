using ils.IR;
using ils.IR.Variables;
using static ils.ASTCondition;
using static ils.TypeSystem;

namespace ils;

public class IRGenerator
{
    public const string MAIN_FUNCTION_LABEL = "FUNC_MAIN_START";
    public const string MAIN_FUNCTION_NAME = "main";

    public static Dictionary<string, IRLabel> Labels = new();

    // Variables
    public static Dictionary<string, BaseVariable> AllVariables = new();

    private static Dictionary<string, NamedVariable> _globalVariables = new();
    public static Dictionary<string, NamedVariable> GlobalVariables { get => _globalVariables; }

    private static Dictionary<string, TempVariable> _tempVariables = new();

    public static Dictionary<string, IRFunction> Functions = new();
    public static Map<string, string> StringLiterals = new();

    private List<IRNode> _ir = new();

    private Dictionary<ASTScope, ScopeLabels> _scopeLabels = new();

    private Scope _currentScope;

    private Scope _mainScope;

    private Dictionary<int, Scope> _scopes = new();

    private ScopeLabels _currentFuncScope;
    private IRFunction _currentFunction;

    private Dictionary<ConditionType, ConditionType> _oppositeCondition = new()
    {
        { ConditionType.EQUAL, ConditionType.NOT_EQUAL },
        { ConditionType.NOT_EQUAL, ConditionType.EQUAL },
        { ConditionType.LESS, ConditionType.GREATER_EQUAL },
        { ConditionType.LESS_EQUAL, ConditionType.GREATER },
        { ConditionType.GREATER, ConditionType.LESS_EQUAL },
        { ConditionType.GREATER_EQUAL, ConditionType.LESS },
        { ConditionType.NONE, ConditionType.NONE }
    };

    public List<IRNode> Generate(ASTScope mainScope)
    {
        ParseScope(mainScope, null, ScopeType.MAIN);

        return _ir;
    }

    public static void AddLabel(IRLabel label)
    {
        if (Labels.ContainsKey(label.labelName)) ErrorHandler.Custom($"Label {label.labelName} already exists!");

        IRGenerator.Labels.Add(label.labelName, label);
    }

    public void AddIR(IRNode node)
    {
        if (_currentFunction != null)
        {
            _currentFunction.Nodes.Add(node);
            _ir.Add(node);
        }
        else
        {
            _ir.Add(node);
        }
    }

    private string CreateNewTempVar(TypeSystem.Type varType, string value, string name = "")
    {
        string varName = $"TEMP_{(name != "" ? name + "_" : "")}{AllVariables.Keys.Count}";
        TempVariable tempVar = new(varName, varType, value);
        _tempVariables.Add(varName, tempVar);
        AddIR(tempVar);
        return varName;
    }

    private void ParseScope(ASTScope astScope, Scope parentScope, ScopeType scopeType, ASTStatement parentNode = null)
    {
        if (_mainScope == null)
        {
            _mainScope = new Scope(0, scopeType);
            _scopes.Add(0, _mainScope);
            _currentScope = _mainScope;
        }
        else
        {
            _currentScope = new Scope(_scopes.Keys.Count, scopeType);
            _currentScope.SetParent(parentScope);
            _scopes.Add(_currentScope.Id, _currentScope);
        }

        IRLabel scopeStart = null;
        IRLabel scopeEnd = null;

        IRFunctionPrologue? funcPrologue = null;
        if (scopeType == ScopeType.FUNCTION)
        {
            funcPrologue = new IRFunctionPrologue();
            AddIR(funcPrologue);
        }

        switch (scopeType)
        {
            case ScopeType.FUNCTION:
                ASTFunction fun = parentNode as ASTFunction;
                if (fun.Identifier.Value == "main") //We are in the main scope
                    scopeStart = new IRLabel(MAIN_FUNCTION_LABEL);
                else
                    scopeStart = new IRLabel($"FUNC_{fun.Identifier.Value}_START");
                scopeEnd = new IRLabel($"FUNC_{fun.Identifier.Value}_END");
                break;
            default:
                scopeStart = new IRLabel($"{scopeType}_{_currentScope.Id}_START");
                scopeEnd = new IRLabel($"{scopeType}_{_currentScope.Id}_END");
                break;
        }

        _scopeLabels.Add(astScope, new ScopeLabels() { startLabel = scopeStart, endLabel = scopeEnd });
        if (_currentScope.Id != 0)
        {
            AddIR(scopeStart);
            if (scopeType == ScopeType.FUNCTION)
            {
                _currentFuncScope = _scopeLabels[astScope];
                AddIR(new IRScopeStart(_currentScope));
            }
        }

        if (_currentScope.Id == 0) //We are in the main scope
        {
            var vars = astScope.GetStatementsOfType<ASTVariableDeclaration>().ToList();
            foreach (ASTVariableDeclaration dec in vars)
            {
                _globalVariables.Add(dec.Name.Value, null);
                ParseVarialbeDeclaration(dec, astScope);
            }

            var funcs = astScope.GetStatementsOfType<ASTFunction>().ToList();
            foreach (ASTFunction func in funcs)
            {
                string identifier = func.Identifier.Value;

                List<NamedVariable> parameters = new();
                foreach (ASTVariableDeclaration parameter in func.Parameters)
                {
                    NamedVariable par = new(parameter, false, true);
                    parameters.Add(par);
                }

                IRFunction irfunc = new(identifier, func.ReturnType != null ? func.ReturnType : null, parameters);

                Functions.Add(func.Identifier.Value, irfunc);
            }
        }

        if (_currentScope.scopeType == ScopeType.FUNCTION && parentNode is ASTFunction parenAstFunc)
            foreach (NamedVariable parameter in Functions[parenAstFunc.Identifier.Value].Parameters)
                _currentScope.AddLocalVariable(parameter);

        IRScopeEnd irScopeEnd = new(_currentScope);

        foreach (ASTStatement statement in astScope.Statements)
            ParseStatement(statement, astScope, parentNode, irScopeEnd, scopeStart, scopeEnd);

        foreach (var temp in _tempVariables) AddIR(new IRDestroyTemp(temp.Key));

        foreach (var local in _currentScope.LocalVariables)
        {
            if (local.Value is TempVariable) AddIR(new IRDestroyTemp(local.Key));
            if (local.Value is NamedVariable named && !named.isGlobal && !named.isFuncArg)
                AddIR(new IRDestroyTemp(local.Key));
        }

        _tempVariables.Clear();

        if (_currentScope.Id != 0)
        {
            AddIR(scopeEnd);
            if (scopeType == ScopeType.FUNCTION)
            {
                funcPrologue.localVariables = _currentScope.LocalVariables.Count;
                AddIR(new IRFunctionEpilogue());
                AddIR(irScopeEnd);
                _currentFunction = null;
            }
        }

        _currentScope = parentScope;
    }

    private void ParseStatement(ASTStatement statement, ASTScope astScope, ASTStatement parentNode,
        IRScopeEnd irScopeEnd, IRLabel scopeStart, IRLabel scopeEnd)
    {
        if (statement is ASTFunctionCall call)
        {
            ParseFunctionCall(call);
        }
        else if (statement is ASTFunction func)
        {
            _currentFunction = Functions[func.Identifier.Value];
            AddIR(_currentFunction);
            ParseScope(func.Scope, _currentScope, ScopeType.FUNCTION, func);
        }
        else if (statement is ASTVariableDeclaration varDeclaration)
        {
            ParseVarialbeDeclaration(varDeclaration, astScope);
        }
        else if (statement is ASTScope scope)
        {
            ParseScope(scope, _currentScope, ScopeType.MAIN);
        }
        else if (statement is ASTReturn ret)
        {
            IRReturn irret = new(ParseExpression(ret.Value), 0);

            if (parentNode != null && parentNode is ASTFunction astFunc)
                irScopeEnd.valuesToClear = astFunc.Parameters.Count;

            AddIR(irret);
            AddIR(new IRJump(_currentFuncScope.endLabel.labelName, ConditionType.NONE));
        }
        else if (statement is ASTAssign assign)
        {
            ParseAssign(assign);
        }
        else if (statement is ASTIf ifstmt)
        {
            ParseIf(ifstmt);
        }
        else if (statement is ASTWhile whilestmt)
        {
            ParseWhile(whilestmt);
        }
        else if (statement is ASTBreak)
        {
            ParseBreak(astScope, scopeStart, scopeEnd);
        }
        else if (statement is ASTArrayDeclaration arrayDecl)
        {
            ParseArrayDeclaration(arrayDecl);
        }
    }

    private void ParseArrayDeclaration(ASTArrayDeclaration array)
    {
    }

    private BaseVariable ParseIndex(ASTArrayIndex ast)
    {
        BaseVariable index = ParseExpression(ast.Index);
        _currentScope.VariableExistsErr(ast.Identifier);

        BaseVariable array = _currentScope.GetVariable(ast.Identifier);

        return new ArrayIndexedVariable(index, array);
    }

    private void ParseBreak(ASTScope scope, IRLabel scopeStart, IRLabel scopeEnd)
    {
        if (scope.ScopeType == ScopeType.IF)
        {
            if (GetParentScopeOfType(ScopeType.LOOP, scope) is ASTScope parentScopeOfType)
            {
                if (parentScopeOfType.ScopeType == ScopeType.LOOP)
                    AddIR(new IRJump(_scopeLabels[parentScopeOfType].endLabel.labelName, ConditionType.NONE));
            }
            else
            {
                AddIR(new IRJump(scopeEnd.labelName, ConditionType.NONE));
            }
        }
        else if (scope.ScopeType == ScopeType.LOOP)
        {
            AddIR(new IRJump(scopeEnd.labelName, ConditionType.NONE));
        }
    }

    private void ParseVarialbeDeclaration(ASTVariableDeclaration vardec, ASTScope scope)
    {
        if (_currentScope.VariableExists(vardec.Name.Value))
        {
            if (_currentScope.Id == 0) return;
            ErrorHandler.Custom($"[{vardec.Name.Line}] Variable '{vardec.Name.Value}' already exists!'");
        }
        else
        {
            BaseVariable newVar = null;
            switch (scope.ScopeType)
            {
                case ScopeType.FUNCTION:
                case ScopeType.LOOP:
                case ScopeType.IF:
                    newVar = new NamedVariable(vardec, false, false);
                    break;
                case ScopeType.MAIN:
                    newVar = new NamedVariable(vardec, true, false);
                    break;
            }

            if (_currentScope.Id == 0) _globalVariables[vardec.Name.Value] = (NamedVariable)newVar;

            //_variables.Add(newVar.variableName, newVar);
            _currentScope.AddLocalVariable(newVar);

            if (vardec.Value != null)
            {
                BaseVariable var = ParseExpression(vardec.Value);

                if (var is ArrayIndexedVariable indexed)
                {
                    newVar.indexedVar = indexed;
                }
                else
                {
                    newVar.AssignVariable(var);
                }
            }

            AddIR(newVar);
        }
    }

    private BaseVariable ParseFunctionCall(ASTFunctionCall call)
    {
        IRFunction func = null;

        if (call.Identifier.Value.StartsWith('@') && !Builtins.IsBuiltIn(call.Identifier.Value))
        {
            ErrorHandler.Custom($"Builtin function '{call.Identifier.Value} doesn't exist!");
            return null;
        }

        if (!Functions.ContainsKey(call.Identifier.Value) &&
            !Builtins.BuiltinFunctions.Any(x => x.name == call.Identifier.Value))
        {
            ErrorHandler.Custom($"Function '{call.Identifier.Value}' does not exist!");
            return null;
        }
        else if (Functions.ContainsKey(call.Identifier.Value))
        {
            func = Functions[call.Identifier.Value];
        }

        List<BaseVariable> arguments = new();

        foreach (ASTExpression argument in call.Arguments)
        {
            BaseVariable arg = ParseExpression(argument);

            //string temp = CreateNewTempVar(arg.variableType, "0");
            //TempVariable tempVar = _tempVariables[temp];
            //tempVar.AssignVariable(arg);
            //arguments.Add(tempVar);

            if (arg is TempVariable || arg is NamedVariable)
            {
                string temp = CreateNewTempVar(arg.variableType, "0");
                TempVariable tempVar = _tempVariables[temp];
                tempVar.AssignVariable(arg);
                arguments.Add(tempVar);
            }
            else if (arg is FunctionReturnVariable)
            {
                arguments.Add(arg);
            }
            else
            {
                arguments.Add(arg);
            }
        }

        if (Functions.ContainsKey(call.Identifier.Value) &&
            arguments.Count != Functions[call.Identifier.Value].Parameters.Count)
        {
            ErrorHandler.Custom(
                $"Function '{call.Identifier.Value}' takes {Functions[call.Identifier.Value].Parameters.Count} arguments, you provided {arguments.Count}!");
            return null;
        }

        //add return somehow 

        IRFunctionCall ircall = new(call.Identifier.Value, arguments);
        if(Functions.ContainsKey(call.Identifier.Value)) Functions[call.Identifier.Value].UseCount++;
        if (func != null && func.ReturnType.DataType != DataType.VOID)
        {
            AddIR(ircall);
            if (Functions.ContainsKey(func.Name)) Functions[func.Name].UseCount++;
            return new FunctionReturnVariable(func.Name, func.ReturnType, _currentScope.LocalVariables.Count, ircall);
        }
        else
        {
            AddIR(ircall);
            foreach (BaseVariable arg in arguments) arg.needsPreservedReg = true;
        }

        return null;
    }

    private struct ScopeLabels
    {
        public IRLabel startLabel, endLabel;
    }

    public ASTScope GetParentScopeOfType(ScopeType scopeType, ASTScope scope)
    {
        if (scope.ParentScope != null)
        {
            if (scope.ParentScope.ScopeType == scopeType) return scope.ParentScope;
            else return GetParentScopeOfType(scopeType, scope.ParentScope);
        }
        else
        {
            return null;
        }
    }

    public static bool CanEvalArtihmeticExpr(ASTArithmeticOperation op)
    {
        if (op.LeftNode == null || op.RightNode == null) return false;

        bool CheckNode(ASTExpression expr)
        {
            if (expr is ASTIntLiteral)
                return true;
            else if (expr is ASTArithmeticOperation operation) return CanEvalArtihmeticExpr(operation);
            return false;
        }

        return CheckNode(op.LeftNode) && CheckNode(op.RightNode);
    }

    private void ParseAssign(ASTAssign assign)
    {
        BaseVariable asnVar = _currentScope.AllVariables[assign.Identifier.Value];

        if (assign.Value is ASTIdentifier identifier)
        {
            AddIR(new IRAssign(asnVar, identifier.Name, Types[DataType.IDENTIFIER]));
        }
        else if (assign.Value is ASTIntLiteral intLiteral)
        {
            AddIR(new IRAssign(asnVar, intLiteral.Value.ToString(), Types[DataType.INT]));
        }
        else if (assign.Value is ASTStringLiteral strLiteral)
        {
            AddIR(new IRAssign(asnVar, strLiteral.Value, Types[DataType.STRING]));
        }
        else if (assign.Value is ASTCharLiteral charLiteral)
        {
            AddIR(new IRAssign(asnVar, charLiteral.Value.ToString(), Types[DataType.CHAR]));
        }
        else if (assign.Value is ASTBoolLiteral boolLiteral)
        {
            AddIR(new IRAssign(asnVar, boolLiteral.Value.ToString(), Types[DataType.BOOL]));
        }
        else
        {
            BaseVariable saveLocation = _currentScope.AllVariables[assign.Identifier.Value];
            BaseVariable var = ParseExpression(assign.Value);

            if (var is TempVariable tempVar)
                AddIR(new IRAssign(saveLocation, tempVar.variableName, Types[DataType.IDENTIFIER]));
            if (var is NamedVariable namedVar)
                AddIR(new IRAssign(saveLocation, namedVar.variableName, Types[DataType.IDENTIFIER]));
            if (var is LiteralVariable literalVar)
                AddIR(new IRAssign(saveLocation, literalVar.value.ToString(), literalVar.variableType));
            if (var is FunctionReturnVariable funcVar)
                //_IR.Add(new IRAssign(saveLocation, regVar.value.ToString(), regVar.variableType));
                AddIR(new IRAssign(saveLocation, funcVar.variableName, Types[DataType.IDENTIFIER]));
            if (var is ArrayIndexedVariable indexVar)
            {
                var asgn = new IRAssign(saveLocation, indexVar.Index.ToString(), Types[DataType.IDENTIFIER]);
                asgn.indexedArray = indexVar;

                AddIR(asgn);
            }             
        }
    }

    private void ParseWhile(ASTWhile whilestmt)
    {
        int labelnum = Labels.Count;

        //string conditionResultName = CreateNewTempVar(DataType.BOOL, "0");

        IRLabel startLabel = new($"WHILE_{labelnum}_START");
        IRLabel endLabel = new($"WHILE_{labelnum}_END");
        IRLabel condLabel = new($"WHILE_{labelnum}_COND");

        //IRCompare comp = ParseCondition(whilestmt.Condition /*, _tempVariables[conditionResultName]*/);
        
        AddIR(new IRJump(condLabel.labelName, ConditionType.NONE));
        AddIR(startLabel);
        ParseScope(whilestmt.Scope, _currentScope, ScopeType.LOOP);
        AddIR(condLabel);
        //AddIR(comp);
        ParseLogicalCondition(whilestmt.Condition, startLabel, endLabel);
        //AddIR(new IRJump(startLabel.labelName, whilestmt.Condition.ConditionType));
        AddIR(endLabel);
    }

    private void ParseLogicalCondition(ASTLogicCondition _cond, IRLabel trueLabel, IRLabel falseLabel)
    {
        if (_cond.Conditions.Count == 0)
        {
            // Handle empty condition (shouldn't happen in practice)
            return;
        }

        for (int i = 0; i < _cond.Conditions.Count; i++)
        {
            if (_cond.Conditions[i] != null)
            {
                IRCompare compare = ParseCondition(_cond.Conditions[i]);
                AddIR(compare);

                if (i < _cond.LogicOperators.Count)
                {
                    if (_cond.LogicOperators[i] == TokenType.OR)
                    {
                        // For OR, jump to true label if condition is true
                        AddIR(new IRJump(trueLabel.labelName, _cond.Conditions[i].ConditionType));
                    }
                    else if (_cond.LogicOperators[i] == TokenType.AND)
                    {
                        // For AND, jump to false label if condition is false
                        AddIR(new IRJump(falseLabel.labelName, _oppositeCondition[_cond.Conditions[i].ConditionType]));
                    }
                }
                else
                {
                    // Last condition
                    AddIR(new IRJump(trueLabel.labelName, _cond.Conditions[i].ConditionType));
                }
            }
            else
            {
                // Handle null condition
                if (i < _cond.LogicOperators.Count)
                {
                    if (_cond.LogicOperators[i] == TokenType.OR)
                    {
                        // For OR, continue to next condition
                        continue;
                    }
                    else if (_cond.LogicOperators[i] == TokenType.AND)
                    {
                        // For AND, jump to false label as this condition is effectively false
                        AddIR(new IRJump(falseLabel.labelName, ConditionType.NONE));
                        return;
                    }
                }
            }
        }

        // After all conditions, if we reach here:
        // - For OR chain: all were false or null, so jump to false label
        // - For AND chain: this should not be reached for non-null conditions, but add a jump to be safe
        AddIR(new IRJump(falseLabel.labelName, ConditionType.NONE));
    }

    private IRCompare ParseCondition(ASTCondition cond)
    {
        BaseVariable leftNodeEvalResult = ParseExpression(cond.LeftNode);
        BaseVariable rightNodeEvalResult;
        if (cond.RightNode != null)
            rightNodeEvalResult = ParseExpression(cond.RightNode);
        else
            rightNodeEvalResult = new LiteralVariable("1", Types[DataType.INT]);
        return new IRCompare(leftNodeEvalResult, rightNodeEvalResult);
    }

    private void ParseIf(ASTIf ifstmt)
    {
        int labelnum = Labels.Count;
        IRLabel bodyLabel = new($"IF_{labelnum}_BODY");
        IRLabel endLabel = new($"IF_{labelnum}_END");
        IRLabel totalEnd = new($"IF_{labelnum}_TOTALEND");

        //IRCompare comp = ParseCondition(ifstmt.Condition);
        //AddIR(comp);
        ParseLogicalCondition(ifstmt.Condition, bodyLabel, endLabel);
        //AddIR(new IRJump(label.labelName, oppositeCondition[ifstmt.Condition.ConditionType]));

        AddIR(bodyLabel);
        ParseScope(ifstmt.Scope, _currentScope, ScopeType.IF);
        AddIR(new IRJump(totalEnd.labelName, ConditionType.NONE));  

        if (ifstmt.Pred != null)
        {    
            //AddIR(new IRJump(endLabel.labelName, ConditionType.NONE));
            AddIR(endLabel);
            ParseIfPred(ifstmt.Pred, totalEnd);           
        }
        else
        {
            AddIR(endLabel);
        }
        AddIR(totalEnd);
    }
    
    private void ParseIfPred(ASTIfPred pred, IRLabel totalEndLabel)
    {
        int labelNum = Labels.Count;

        if (pred is ASTElifPred elif)
        {
            //string conditionResultName = CreateNewTempVar(DataType.BOOL, "0");

            //IRCompare comp = ParseCondition(elif.Condition /*, _tempVariables[conditionResultName]*/);
            //AddIR(comp);
            


            IRLabel bodyLabel = new($"ELSE_{labelNum}_BODY");
            IRLabel endLabel = new($"ELSE_{labelNum}_END");
            if (elif.Pred != null)
                ParseLogicalCondition(elif.Condition, bodyLabel, endLabel);
                //AddIR(new IRJump(label.labelName, oppositeCondition[elif.Condition.ConditionType]));
            else
                ParseLogicalCondition(elif.Condition, bodyLabel, endLabel);
            //AddIR(new IRJump(endLabel.labelName, oppositeCondition[elif.Condition.ConditionType]));

            AddIR(bodyLabel);
            ParseScope(elif.Scope, _currentScope, ScopeType.ELIF);
            AddIR(new IRJump(totalEndLabel.labelName, ConditionType.NONE));

            if (elif.Pred != null)
            {
                AddIR(endLabel);
                ParseIfPred(elif.Pred, totalEndLabel);
            }
        }
        else if (pred is ASTElsePred elsepred)
        {
            ParseScope(elsepred.Scope, _currentScope, ScopeType.ELSE);
        }
    }

    private BaseVariable ParseExpression(ASTExpression _expression)
    {
        if (_expression is ASTIdentifier identifier)
        {
            _currentScope.VariableExistsErr(identifier.token);
            return _currentScope.AllVariables[identifier.Name];
        }
        else if (_expression is ASTLiteral literal)
        {
            return new LiteralVariable(literal.Value, literal.VariableType);
        }
        else if (_expression is ASTFunctionCall funcCall)
        {
            return ParseFunctionCall(funcCall);
        }
        else if (_expression is ASTArrayIndex index)
        {
            return ParseIndex(index);
        }
        else if (_expression is ASTArrayConstructor arrayConstructor)
        {
            string val = "";
            List<string> vals = new();

            foreach (ASTExpression item in arrayConstructor.Values)
            {
                BaseVariable parsedItem = ParseExpression(item);
                vals.Add(parsedItem.value);
            }

            val = string.Join(", ", vals);

            return new ArrayVariable(arrayConstructor.Type, val, arrayConstructor.Length);
        }
        else if (_expression is ASTArithmeticOperation arithmeticOp)
        {
            BaseVariable leftVar = ParseExpression(arithmeticOp.LeftNode);

            BaseVariable rightVar = ParseExpression(arithmeticOp.RightNode);

            if (!VerifyOperation(leftVar.variableType.DataType, rightVar.variableType.DataType,
                    arithmeticOp.Operation)) return null;

            if (CanEvalArtihmeticExpr(arithmeticOp))
            {
                int x = MathEvaluator.Evaluate(arithmeticOp);
                return new LiteralVariable(x.ToString(), Types[DataType.INT]);
            }

            string resultName = CreateNewTempVar(Types[DataType.INT], "0", "OP_RES");
            TempVariable result = _tempVariables[resultName];

            AddIR(new IRArithmeticOp(result, leftVar, rightVar, arithmeticOp.Operation));
            if (leftVar is TempVariable) AddIR(new IRDestroyTemp(leftVar.variableName));
            if (rightVar is TempVariable) AddIR(new IRDestroyTemp(rightVar.variableName));
            return result;
        }

        return null;
    }

    private bool VerifyOperation(DataType aType, DataType bType, ArithmeticOpType opType)
    {
        if (opType is (ArithmeticOpType.ADD or ArithmeticOpType.MUL or ArithmeticOpType.DIV or ArithmeticOpType.SUB
            or ArithmeticOpType.MOD))
            if (aType == DataType.BOOL || bType == DataType.BOOL)
            {
                ErrorHandler.Custom("You can't do arithmetic operations on bools!");
                return false;
            }

        return true;
    }

    public static int ids = 0;

    public static string NewId()
    {
        return "ID_" + ids++;
    }
}