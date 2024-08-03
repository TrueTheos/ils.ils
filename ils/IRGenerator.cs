using static ils.ASTCondition;
using static ils.TypeSystem;

namespace ils;

public class IRGenerator
{
    public const string MAIN_FUNCTION_LABEL = "FUNC_MAIN_START";
    public const string MAIN_FUNCTION_NAME = "main";

    private static Dictionary<string, IRLabel> _labels = new();

    private static Dictionary<string, NamedVariable> _globalVariables = new();
    private static Dictionary<string, TempVariable> _tempVariables = new();
    private static Dictionary<string, IRFunction> _functions = new();

    public static Map<string, string> StringLiterals = new();

    public List<IRNode> IR = new();

    public static Dictionary<string, Variable> _allVariables = new();

    private Dictionary<ASTScope, ScopeLabels> scopeLabels = new();

    private Scope currentScope;

    private Scope mainScope;
    private Dictionary<int, Scope> scopes = new();

    private ScopeLabels currentFuncScope;
    private IRFunction currentFunction;

    private Dictionary<ConditionType, ConditionType> oppositeCondition = new()
    {
        { ConditionType.EQUAL, ConditionType.NOT_EQUAL },
        { ConditionType.NOT_EQUAL, ConditionType.EQUAL },
        { ConditionType.LESS, ConditionType.GREATER_EQUAL },
        { ConditionType.LESS_EQUAL, ConditionType.GREATER },
        { ConditionType.GREATER, ConditionType.LESS_EQUAL },
        { ConditionType.GREATER_EQUAL, ConditionType.LESS },
        { ConditionType.NONE, ConditionType.NONE }
    };

    public void AddIR(IRNode node)
    {
        if (currentFunction != null)
        {
            currentFunction.nodes.Add(node);
            IR.Add(node);
        }
        else
        {
            IR.Add(node);
        }
    }

    public List<IRNode> Generate(ASTScope mainScope)
    {
        ParseScope(mainScope, null, ScopeType.DEFAULT);

        Console.WriteLine("\n");
        foreach (IRNode irNode in IR) Console.WriteLine(irNode.GetString());

        /*foreach (var func in _functions.Values)
        {
            IR.Add(func);
        }*/

        return IR;
    }

    private string CreateNewTempVar(TypeSystem.Type varType, string value, string name = "")
    {
        string varName = $"TEMP_{(name != "" ? name + "_" : "")}{_allVariables.Keys.Count}";
        TempVariable tempVar = new(varName, varType, value);
        _tempVariables.Add(varName, tempVar);
        AddIR(tempVar);
        return varName;
    }

    private void ParseScope(ASTScope astScope, Scope parentScope, ScopeType scopeType, ASTStatement parentNode = null)
    {
        if (mainScope == null)
        {
            mainScope = new Scope(0, scopeType);
            scopes.Add(0, mainScope);
            currentScope = mainScope;
        }
        else
        {
            currentScope = new Scope(scopes.Keys.Count, scopeType);
            currentScope.SetParent(parentScope);
            scopes.Add(currentScope.id, currentScope);
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
            case ScopeType.DEFAULT:
                scopeStart = new IRLabel($"SCOPE_{currentScope.id}_START");
                scopeEnd = new IRLabel($"SCOPE_{currentScope.id}_END");
                break;
            case ScopeType.IF:
                scopeStart = new IRLabel($"IF_{currentScope.id}_START");
                scopeEnd = new IRLabel($"IF_{currentScope.id}_END");
                break;
            case ScopeType.ELIF:
                scopeStart = new IRLabel($"ELIF_{currentScope.id}_START");
                scopeEnd = new IRLabel($"ELIF_{currentScope.id}_END");
                break;
            case ScopeType.ELSE:
                scopeStart = new IRLabel($"ELSE_{currentScope.id}_START");
                scopeEnd = new IRLabel($"ELSE_{currentScope.id}_END");
                break;
            case ScopeType.LOOP:
                scopeStart = new IRLabel($"LOOP_{currentScope.id}_START");
                scopeEnd = new IRLabel($"LOOP_{currentScope.id}_END");
                break;
            case ScopeType.FUNCTION:
                ASTFunction fun = parentNode as ASTFunction;
                if (fun.Identifier.Value == "main") //We are in the main scope
                    scopeStart = new IRLabel(MAIN_FUNCTION_LABEL);
                else
                    scopeStart = new IRLabel($"FUNC_{fun.Identifier.Value}_START");
                scopeEnd = new IRLabel($"FUNC_{fun.Identifier.Value}_END");
                break;
        }

        scopeLabels.Add(astScope, new ScopeLabels() { startLabel = scopeStart, endLabel = scopeEnd });
        if (currentScope.id != 0)
        {
            AddIR(scopeStart);
            if (scopeType == ScopeType.FUNCTION)
            {
                currentFuncScope = scopeLabels[astScope];
                AddIR(new IRScopeStart(currentScope));
            }
        }

        if (currentScope.id == 0) //We are in the main scope
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

                _functions.Add(func.Identifier.Value, irfunc);
            }
        }

        if (currentScope.scopeType == ScopeType.FUNCTION && parentNode is ASTFunction parenAstFunc)
            foreach (NamedVariable parameter in _functions[parenAstFunc.Identifier.Value].parameters)
                currentScope.AddLocalVariable(parameter);

        IRScopeEnd irScopeEnd = new(currentScope);

        foreach (ASTStatement statement in astScope.Statements)
            ParseStatement(statement, astScope, parentNode, irScopeEnd, scopeStart, scopeEnd);

        foreach (var temp in _tempVariables) AddIR(new IRDestroyTemp(temp.Key));

        foreach (var local in currentScope.localVariables)
        {
            if (local.Value is TempVariable) AddIR(new IRDestroyTemp(local.Key));
            if (local.Value is NamedVariable named && !named.isGlobal && !named.isFuncArg)
                AddIR(new IRDestroyTemp(local.Key));
        }

        _tempVariables.Clear();

        if (currentScope.id != 0)
        {
            AddIR(scopeEnd);
            if (scopeType == ScopeType.FUNCTION)
            {
                funcPrologue.localVariables = currentScope.localVariables.Count;
                AddIR(new IRFunctionEpilogue());
                AddIR(irScopeEnd);
                currentFunction = null;
            }
        }

        currentScope = parentScope;
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
            currentFunction = _functions[func.Identifier.Value];
            AddIR(currentFunction);
            ParseScope(func.Scope, currentScope, ScopeType.FUNCTION, func);
        }
        else if (statement is ASTVariableDeclaration varDeclaration)
        {
            ParseVarialbeDeclaration(varDeclaration, astScope);
        }
        else if (statement is ASTScope scope)
        {
            ParseScope(scope, currentScope, ScopeType.DEFAULT);
        }
        else if (statement is ASTReturn ret)
        {
            IRReturn irret = new(ParseExpression(ret.Value), 0);

            if (parentNode != null && parentNode is ASTFunction astFunc)
                irScopeEnd.valuesToClear = astFunc.Parameters.Count;

            AddIR(irret);
            AddIR(new IRJump(currentFuncScope.endLabel.labelName, ConditionType.NONE));
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

    private Variable ParseIndex(ASTArrayIndex ast)
    {
        Variable index = ParseExpression(ast.Index);
        currentScope.VariableExistsErr(ast.Identifier);

        Variable array = currentScope.GetVariable(ast.Identifier);
        if (index is LiteralVariable lit)
        {
            
        }

        return new ArrayIndexedVariable(index, array);
    }

    private void ParseBreak(ASTScope scope, IRLabel scopeStart, IRLabel scopeEnd)
    {
        if (scope.ScopeType == ScopeType.IF)
        {
            if (GetParentScopeOfType(ScopeType.LOOP, scope) is ASTScope parentScopeOfType)
            {
                if (parentScopeOfType.ScopeType == ScopeType.LOOP)
                    AddIR(new IRJump(scopeLabels[parentScopeOfType].endLabel.labelName, ConditionType.NONE));
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
        if (currentScope.VariableExists(vardec.Name.Value))
        {
            if (currentScope.id == 0) return;
            ErrorHandler.Custom($"[{vardec.Name.Line}] Variable '{vardec.Name.Value}' already exists!'");
        }
        else
        {
            Variable newVar = null;
            switch (scope.ScopeType)
            {
                case ScopeType.FUNCTION:
                case ScopeType.LOOP:
                case ScopeType.IF:
                    newVar = new NamedVariable(vardec, false, false);
                    break;
                case ScopeType.DEFAULT:
                    newVar = new NamedVariable(vardec, true, false);
                    break;
            }

            if (currentScope.id == 0) _globalVariables[vardec.Name.Value] = (NamedVariable)newVar;

            //_variables.Add(newVar.variableName, newVar);
            currentScope.AddLocalVariable(newVar);

            if (vardec.Value != null)
            {
                Variable var = ParseExpression(vardec.Value);

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

    private Variable ParseFunctionCall(ASTFunctionCall call)
    {
        IRFunction func = null;

        if (call.Identifier.Value.StartsWith('@') && !Builtins.IsBuiltIn(call.Identifier.Value))
        {
            ErrorHandler.Custom($"Builtin function '{call.Identifier.Value} doesn't exist!");
            return null;
        }

        if (!_functions.ContainsKey(call.Identifier.Value) &&
            !Builtins.BuiltinFunctions.Any(x => x.name == call.Identifier.Value))
        {
            ErrorHandler.Custom($"Function '{call.Identifier.Value}' does not exist!");
            return null;
        }
        else if (_functions.ContainsKey(call.Identifier.Value))
        {
            func = _functions[call.Identifier.Value];
        }

        List<Variable> arguments = new();

        foreach (ASTExpression argument in call.Arguments)
        {
            Variable arg = ParseExpression(argument);

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

        if (_functions.ContainsKey(call.Identifier.Value) &&
            arguments.Count != _functions[call.Identifier.Value].parameters.Count)
        {
            ErrorHandler.Custom(
                $"Function '{call.Identifier.Value}' takes {_functions[call.Identifier.Value].parameters.Count} arguments, you provided {arguments.Count}!");
            return null;
        }

        //add return somehow 

        IRFunctionCall ircall = new(call.Identifier.Value, arguments);
        if (func != null && func.returnType.DataType != DataType.VOID)
        {
            //_IR.Add(ircall); //na razie nie pozwole na wywołanie funkcji która nie zwraca voida i nie jest przypisywana nigdzie
            return new FunctionReturnVariable(func.name, func.returnType, currentScope.localVariables.Count, ircall);
        }
        else
        {
            AddIR(ircall);
            foreach (Variable arg in arguments) arg.needsPreservedReg = true;
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
        Variable asnVar = currentScope.allVariables[assign.identifier.Value];

        if (assign.value is ASTIdentifier identifier)
        {
            AddIR(new IRAssign(asnVar, identifier.name, Types[DataType.IDENTIFIER]));
        }
        else if (assign.value is ASTIntLiteral intLiteral)
        {
            AddIR(new IRAssign(asnVar, intLiteral.value.ToString(), Types[DataType.INT]));
        }
        else if (assign.value is ASTStringLiteral strLiteral)
        {
            AddIR(new IRAssign(asnVar, strLiteral.value, Types[DataType.STRING]));
        }
        else if (assign.value is ASTCharLiteral charLiteral)
        {
            AddIR(new IRAssign(asnVar, charLiteral.value.ToString(), Types[DataType.CHAR]));
        }
        else if (assign.value is ASTBoolLiteral boolLiteral)
        {
            AddIR(new IRAssign(asnVar, boolLiteral.value.ToString(), Types[DataType.BOOL]));
        }
        else
        {
            Variable saveLocation = currentScope.allVariables[assign.identifier.Value];
            Variable var = ParseExpression(assign.value);

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
                AddIR(new IRAssign(saveLocation, indexVar.Index.ToString(), Types[DataType.IDENTIFIER]));
        }
    }

    private void ParseWhile(ASTWhile whilestmt)
    {
        int labelnum = _labels.Count;

        //string conditionResultName = CreateNewTempVar(DataType.BOOL, "0");

        IRLabel startLabel = new($"WHILE_{labelnum}_START");
        IRLabel endLabel = new($"WHILE_{labelnum}_END");

        IRCompare comp = ParseCondition(whilestmt.cond /*, _tempVariables[conditionResultName]*/);

        AddIR(startLabel);
        AddIR(comp);
        AddIR(new IRJump(endLabel.labelName, oppositeCondition[whilestmt.cond.conditionType]));

        ParseScope(whilestmt.scope, currentScope, ScopeType.LOOP);
        AddIR(new IRJump(startLabel.labelName, ConditionType.NONE));

        AddIR(endLabel);
    }

    private IRCompare ParseCondition(ASTCondition cond)
    {
        Variable leftNodeEvalResult = ParseExpression(cond.leftNode);

        Variable rightNodeEvalResult;

        if (cond.rightNode != null)
            rightNodeEvalResult = ParseExpression(cond.rightNode);
        else
            rightNodeEvalResult = new LiteralVariable("1", Types[DataType.INT]);

        return new IRCompare(leftNodeEvalResult, rightNodeEvalResult);
    }

    private void ParseIf(ASTIf ifstmt)
    {
        int labelnum = _labels.Count;
        IRLabel label = new($"IF_{labelnum}_START");

        IRCompare comp = ParseCondition(ifstmt.cond);
        AddIR(comp);
        AddIR(new IRJump(label.labelName, oppositeCondition[ifstmt.cond.conditionType]));

        ParseScope(ifstmt.scope, currentScope, ScopeType.IF);

        if (ifstmt.pred != null)
        {
            IRLabel endLabel = new($"IF_{labelnum}_END");
            AddIR(new IRJump(endLabel.labelName, ConditionType.NONE));
            AddIR(label);
            ParseIfPred(ifstmt.pred, endLabel);
            AddIR(endLabel);
        }
        else
        {
            AddIR(label);
        }
    }
    
    private void ParseIfPred(ASTIfPred pred, IRLabel endLabel)
    {
        int labelNum = _labels.Count;

        if (pred is ASTElifPred elif)
        {
            //string conditionResultName = CreateNewTempVar(DataType.BOOL, "0");

            IRCompare comp = ParseCondition(elif.cond /*, _tempVariables[conditionResultName]*/);
            AddIR(comp);

            IRLabel label = new($"ELSE_{labelNum}_START");
            if (elif.pred != null)
                AddIR(new IRJump(label.labelName, oppositeCondition[elif.cond.conditionType]));
            else
                AddIR(new IRJump(endLabel.labelName, oppositeCondition[elif.cond.conditionType]));

            ParseScope(elif.scope, currentScope, ScopeType.ELIF);

            AddIR(new IRJump(endLabel.labelName, ConditionType.NONE));

            if (elif.pred != null)
            {
                AddIR(label);
                ParseIfPred(elif.pred, endLabel);
            }
        }
        else if (pred is ASTElsePred elsepred)
        {
            ParseScope(elsepred.scope, currentScope, ScopeType.ELSE);
        }
    }

    private Variable ParseExpression(ASTExpression _expression)
    {
        if (_expression is ASTIdentifier identifier)
        {
            currentScope.VariableExistsErr(identifier.token);
            return currentScope.allVariables[identifier.name];
        }
        else if (_expression is ASTLiteral literal)
        {
            return new LiteralVariable(literal.value, literal.variableType);
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

            foreach (ASTExpression item in arrayConstructor.values)
            {
                Variable parsedItem = ParseExpression(item);
                vals.Add(parsedItem.value);
            }

            val = string.Join(", ", vals);

            return new ArrayVariable(arrayConstructor.type, val, arrayConstructor.length);
        }
        else if (_expression is ASTArithmeticOperation arithmeticOp)
        {
            Variable leftVar = ParseExpression(arithmeticOp.LeftNode);

            Variable rightVar = ParseExpression(arithmeticOp.RightNode);

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

    public class IRReturn : IRNode
    {
        public Variable ret;

        public IRReturn(Variable ret, int valuesOnStackToClear)
        {
            Name = "RETURN";

            this.ret = ret;
        }

        public override string GetString()
        {
            return $"({Name}, {ret.variableName})";
        }
    }

    public class IRArray : IRNode
    {
        public override string GetString()
        {
            throw new NotImplementedException();
        }
    }

    public class IRDestroyTemp : IRNode
    {
        public string temp;

        public IRDestroyTemp(string temp)
        {
            Name = "DESTROY_TEMP";
            this.temp = temp;
        }

        public override string GetString()
        {
            return $"({Name}, {temp})";
        }
    }

    public class IRFunctionPrologue : IRNode
    {
        public int localVariables;

        public IRFunctionPrologue()
        {
            Name = "PROLOGUE";
        }

        public override string GetString()
        {
            return $"({Name})";
        }
    }

    public class IRFunctionEpilogue : IRNode
    {
        public IRFunctionEpilogue()
        {
            Name = "EPILOGUE";
        }

        public override string GetString()
        {
            return $"({Name})";
        }
    }

    public class IRFunction : IRNode
    {
        public string name;
        public TypeSystem.Type returnType = Types[DataType.VOID];
        public List<NamedVariable> parameters = new();
        public List<IRNode> nodes = new();

        public IRFunction(string name, TypeSystem.Type returnType, List<NamedVariable> parameters)
        {
            Name = "FUNC";
            this.name = name;
            if (returnType != null) this.returnType = returnType;
            this.parameters = parameters;
        }

        public override string GetString()
        {
            string r = $"({Name}, {name}, {returnType}";
            foreach (NamedVariable parameter in parameters) r += $", {parameter.variableName}";

            r += ")";
            return r;
        }
    }

    public class IRFunctionCall : IRNode
    {
        public string name;
        public List<Variable> arguments = new();

        public IRFunctionCall(string name, List<Variable> arguments)
        {
            Name = "FUNC_CALL";

            this.name = name;
            this.arguments = arguments;
        }

        public override string GetString()
        {
            string r = $"(CALL, {name}";
            foreach (Variable parameter in arguments) r += $", {parameter.variableName}";

            r += ")";
            return r;
        }
    }

    public class IRCompare : IRNode
    {
        public Variable a;
        public Variable b;

        public IRCompare(Variable a, Variable b)
        {
            Name = "COMPARE";

            this.a = a;
            this.b = b;
        }

        public override string GetString()
        {
            return $"({Name}, {a.variableName}, {b.variableName})";
        }
    }

    public class IRJump : IRNode
    {
        public string label;
        public ConditionType conditionType;

        public IRJump(string label, ConditionType conditionType)
        {
            this.label = label;
            this.conditionType = conditionType;

            switch (this.conditionType)
            {
                case ConditionType.EQUAL:
                    Name = "JUMP_EQUAL";
                    break;
                case ConditionType.NOT_EQUAL:
                    Name = "JUMP_NOT_EQUAL";
                    break;
                case ConditionType.LESS:
                    Name = "JUMP_LESS";
                    break;
                case ConditionType.LESS_EQUAL:
                    Name = "JUMP_LESS_EQUAL";
                    break;
                case ConditionType.GREATER:
                    Name = "JUMP_GREATER";
                    break;
                case ConditionType.GREATER_EQUAL:
                    Name = "JUMP_GREATER_EQUAL";
                    break;
                case ConditionType.NONE:
                    Name = "JUMP";
                    break;
            }
        }

        public override string GetString()
        {
            return $"({Name}, {label})";
        }
    }

    public abstract class Variable : IRNode
    {
        public string variableName = "";
        public TypeSystem.Type variableType;
        public string value { private set; get; }
        public ArrayIndexedVariable indexedVar; //THATS STUPID FIX ME

        public IRNode lastUse = null;

        public bool needsPreservedReg = false;

        public string guid;

        public Variable()
        {
            guid = NewId();
        }

        public void SetValue(string val, TypeSystem.Type valType)
        {
            variableType = valType;

            value = val;
        }

        public abstract string GetValueAsString();

        public void AssignVariable(Variable val)
        {
            if (val is TempVariable) SetValue(val.GetValueAsString(), Types[DataType.IDENTIFIER]);
            if (val is NamedVariable) SetValue(val.GetValueAsString(), Types[DataType.IDENTIFIER]);
            if (val is LiteralVariable literalVar) SetValue(val.GetValueAsString(), literalVar.variableType);
            if (val is FunctionReturnVariable) SetValue(val.GetValueAsString(), val.variableType);
            if (val is ArrayVariable arrayVar) SetValue(val.GetValueAsString(), arrayVar.variableType);
        }

        public void UpdateDestroyAfter(IRNode node)
        {
            lastUse = node;
        }
    }

    public class FunctionReturnVariable : Variable
    {
        public string funcName;
        public int index;
        public IRFunctionCall call;

        public FunctionReturnVariable(string funcName, TypeSystem.Type varType, int index, IRFunctionCall call)
        {
            Name = "FUNC_RETURN_VAR";
            this.funcName = funcName;
            variableName = funcName;
            variableType = varType;
            this.index = index;
            this.call = call;
            //SetValue(reg, variableType);

            _allVariables[guid.ToString()] = this;
        }

        public override string GetString()
        {
            return $"({Name}, {variableName})";
        }

        public override string GetValueAsString()
        {
            return funcName + index.ToString();
        }
    }

    public class ArrayIndexedVariable : Variable
    {
        public Variable Array;
        public Variable Index;

        public ArrayIndexedVariable(Variable index, Variable array)
        {
            Name = "ARRAY_INDEXED_VAR";
            Index = index;
            Array = array;
        }

        public override string GetString()
        {
            return $"({Name}, {Index})";
        }

        public override string GetValueAsString()
        {
            return "";
        }
    }

    public class NamedVariable : Variable
    {
        public bool isGlobal = false;
        public bool isFuncArg = false;

        public NamedVariable(ASTVariableDeclaration declaration, bool isGlobal, bool isFuncArg)
        {
            Name = "NAMED_VAR";

            variableName = declaration.Name.Value;
            this.isGlobal = isGlobal;
            this.isFuncArg = isFuncArg;

            variableType = declaration.Type;

            if (declaration.Value is not ASTArrayIndex index)
            {
                switch (declaration.Type.DataType)
                {
                    case DataType.STRING:
                        if (declaration.Value == null) SetValue(@"\0", variableType);
                        break;
                    case DataType.INT:
                        if (declaration.Value == null) SetValue("0", variableType);
                        break;
                    case DataType.CHAR:
                        if (declaration.Value == null) SetValue("0", variableType);
                        break;
                    case DataType.BOOL:
                        if (declaration.Value == null) SetValue("0", variableType);
                        break;
                    case DataType.IDENTIFIER:
                        if (declaration.Value == null) SetValue("[]", variableType);
                        break;
                    case DataType.ARRAY:
                        if (declaration.Type is ArrayType arrayType)
                            if (declaration.Value == null)
                                SetValue($"{arrayType.length * 4}", variableType);
                        break;
                }
            }

            _allVariables.Add(guid.ToString(), this);
        }

        public override string GetString()
        {
            return $"({Name}, {variableName}, {variableType}, {value})";
        }

        public override string GetValueAsString()
        {
            return guid.ToString();
        }
    }

    public class TempVariable : Variable
    {
        public TempVariable(string variableName, TypeSystem.Type varType, string value)
        {
            Name = "TEMP_VAR";
            this.variableName = variableName;
            variableType = varType;
            SetValue(value, variableType);

            _allVariables.Add(guid.ToString(), this);
        }

        public override string GetString()
        {
            return $"({Name}, {guid}, {value})";
        }

        public override string GetValueAsString()
        {
            return guid.ToString();
        }
    }

    public class LiteralVariable : Variable
    {
        public LiteralVariable(string value, TypeSystem.Type type)
        {
            Name = "LIT_VAR";

            variableType = type;
            //this.variableName = $"LIT_{literalVarsCount}_{value}";

            if (type.DataType == DataType.STRING)
            {
                if (!StringLiterals.Reverse.Contains(value))
                {
                    string name = "STR_" + StringLiterals.Forward.Count.ToString();
                    StringLiterals.Add(name, value);
                    SetValue(name, variableType);
                }
                else
                {
                    SetValue(value, variableType);
                }
            }
            else
            {
                variableName = value.ToString();
                SetValue(value, variableType);
            }

            _allVariables.Add(guid.ToString(), this);
        }

        public override string GetString()
        {
            return $"({Name}, {value})";
        }

        public override string GetValueAsString()
        {
            return value;
        }
    }

    public class ArrayVariable : Variable
    {
        public int Length;

        public ArrayVariable(TypeSystem.Type varType, string value, int arrayLength)
        {
            Name = "ARRAY_VAR";
            variableType = varType;
            SetValue(value, variableType);

            _allVariables.Add(guid.ToString(), this);
            Length = arrayLength;
        }

        public override string GetString()
        {
            return $"({Name}, {value})";
        }

        public override string GetValueAsString()
        {
            return value;
        }
    }

    public abstract class IRNode
    {
        protected string Name;

        public abstract string GetString();
    }

    public class IRAssign : IRNode
    {
        public Variable identifier;
        public TypeSystem.Type assignedType;
        public string value;

        public IRAssign(Variable identifier, string value, TypeSystem.Type assignedType)
        {
            Name = "ASSIGN";

            this.identifier = identifier;
            this.value = value;
            this.assignedType = assignedType;
        }

        public override string GetString()
        {
            return $"({Name}, {identifier.variableName}, {value})";
        }
    }

    public class IRLabel : IRNode
    {
        public string labelName;

        public IRLabel(string name)
        {
            Name = "LABEL";

            labelName = name;

            if (_labels.ContainsKey(labelName)) ErrorHandler.Custom($"Label {labelName} already exists!");

            _labels.Add(labelName, this);
        }

        public override string GetString()
        {
            return $"({Name}, {labelName})";
        }
    }

    public class IRArithmeticOp : IRNode
    {
        public Variable resultLocation;
        public Variable a;
        public Variable b;
        public ArithmeticOpType opType;

        public IRArithmeticOp(Variable resultLocation, Variable a, Variable b, ArithmeticOpType opType)
        {
            this.resultLocation = resultLocation;
            this.a = a;
            this.b = b;
            this.opType = opType;

            switch (opType)
            {
                case ArithmeticOpType.ADD:
                    Name = "ADD";
                    break;
                case ArithmeticOpType.MUL:
                    Name = "MUL";
                    break;
                case ArithmeticOpType.SUB:
                    Name = "SUB";
                    break;
                case ArithmeticOpType.DIV:
                    Name = "DIV";
                    break;
                case ArithmeticOpType.MOD:
                    Name = "MOD";
                    break;
            }
        }

        public override string GetString()
        {
            return $"({Name}, {resultLocation.guid} = {a.guid}, {b.guid})";
        }
    }

    public class IRScopeStart : IRNode
    {
        public Scope scope;

        public IRScopeStart(Scope scope)
        {
            Name = "START_SCOPE";
            this.scope = scope;
        }

        public override string GetString()
        {
            return $"({Name})";
        }
    }

    public class IRScopeEnd : IRNode
    {
        public Scope scope;
        public int valuesToClear;

        public IRScopeEnd(Scope scope)
        {
            Name = "END_SCOPE";
            this.scope = scope;
        }

        public override string GetString()
        {
            return $"({Name})";
        }
    }

    public class Scope
    {
        public int id;
        public Scope parent = null;
        public Dictionary<int, Scope> childs = new();
        public Dictionary<string, Variable> allVariables = new();
        public Dictionary<string, Variable> localVariables = new();
        public ScopeType scopeType;

        public Scope(int _id, ScopeType _scopeType)
        {
            id = _id;
            scopeType = _scopeType;
        }

        public void SetParent(Scope _parent)
        {
            parent = _parent;
            foreach (var var in _parent.allVariables)
                allVariables.Add(var.Key, var.Value);

            _parent.childs.Add(id, this);
        }

        public Variable GetVariable(Token name)
        {
            VariableExistsErr(name);
            return allVariables[name.Value];
        }

        public bool VariableExists(string name)
        {
            return (allVariables.ContainsKey(name) && allVariables[name] != null) ||
                   (_globalVariables.ContainsKey(name) && _globalVariables[name] != null);
        }

        public void VariableExistsErr(Token name)
        {
            if(!VariableExists(name.Value)) ErrorHandler.Throw(new VariableDoesntExistError(name.Value, name.Line));
        }

        public void AddLocalVariable(Variable var)
        {
            localVariables.Add(var.variableName, var);
            allVariables.Add(var.variableName, var);
        }
    }

    public static int ids = 0;

    public static string NewId()
    {
        return "ID_" + ids++;
    }
}