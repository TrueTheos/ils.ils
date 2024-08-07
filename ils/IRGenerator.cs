﻿using static ils.ASTCondition;
using static ils.TypeSystem;

namespace ils;

public class IRGenerator
{
    public const string MAIN_FUNCTION_LABEL = "FUNC_MAIN_START";
    public const string MAIN_FUNCTION_NAME = "main";

    private static Dictionary<string, IRLabel> _Labels = new();

    private static Dictionary<string, NamedVariable> _GlobalVariables = new();
    private static Dictionary<string, TempVariable> _TempVariables = new();
    public static Dictionary<string, IRFunction> _Functions = new();

    public static Map<string, string> _StringLiterals = new();

    private List<IRNode> ir = new();

    public static Dictionary<string, Variable> _AllVariables = new();

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
            currentFunction.Nodes.Add(node);
            ir.Add(node);
        }
        else
        {
            ir.Add(node);
        }
    }

    public List<IRNode> Generate(ASTScope mainScope)
    {
        ParseScope(mainScope, null, ScopeType.DEFAULT);

        Console.WriteLine("\n");
        foreach (IRNode irNode in ir)
        {
            if (irNode == null) continue;
            Console.WriteLine(irNode.GetString());
        }

        /*foreach (var func in _functions.Values)
        {
            IR.Add(func);
        }*/

        return ir;
    }

    private string CreateNewTempVar(TypeSystem.Type varType, string value, string name = "")
    {
        string varName = $"TEMP_{(name != "" ? name + "_" : "")}{_AllVariables.Keys.Count}";
        TempVariable tempVar = new(varName, varType, value);
        _TempVariables.Add(varName, tempVar);
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
                _GlobalVariables.Add(dec.Name.Value, null);
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

                _Functions.Add(func.Identifier.Value, irfunc);
            }
        }

        if (currentScope.scopeType == ScopeType.FUNCTION && parentNode is ASTFunction parenAstFunc)
            foreach (NamedVariable parameter in _Functions[parenAstFunc.Identifier.Value].Parameters)
                currentScope.AddLocalVariable(parameter);

        IRScopeEnd irScopeEnd = new(currentScope);

        foreach (ASTStatement statement in astScope.Statements)
            ParseStatement(statement, astScope, parentNode, irScopeEnd, scopeStart, scopeEnd);

        foreach (var temp in _TempVariables) AddIR(new IRDestroyTemp(temp.Key));

        foreach (var local in currentScope.localVariables)
        {
            if (local.Value is TempVariable) AddIR(new IRDestroyTemp(local.Key));
            if (local.Value is NamedVariable named && !named.isGlobal && !named.isFuncArg)
                AddIR(new IRDestroyTemp(local.Key));
        }

        _TempVariables.Clear();

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
            currentFunction = _Functions[func.Identifier.Value];
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

            if (currentScope.id == 0) _GlobalVariables[vardec.Name.Value] = (NamedVariable)newVar;

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

        if (!_Functions.ContainsKey(call.Identifier.Value) &&
            !Builtins.BuiltinFunctions.Any(x => x.name == call.Identifier.Value))
        {
            ErrorHandler.Custom($"Function '{call.Identifier.Value}' does not exist!");
            return null;
        }
        else if (_Functions.ContainsKey(call.Identifier.Value))
        {
            func = _Functions[call.Identifier.Value];
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
                TempVariable tempVar = _TempVariables[temp];
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

        if (_Functions.ContainsKey(call.Identifier.Value) &&
            arguments.Count != _Functions[call.Identifier.Value].Parameters.Count)
        {
            ErrorHandler.Custom(
                $"Function '{call.Identifier.Value}' takes {_Functions[call.Identifier.Value].Parameters.Count} arguments, you provided {arguments.Count}!");
            return null;
        }

        //add return somehow 

        IRFunctionCall ircall = new(call.Identifier.Value, arguments);
        if(_Functions.ContainsKey(call.Identifier.Value)) _Functions[call.Identifier.Value].UseCount++;
        if (func != null && func.ReturnType.DataType != DataType.VOID)
        {
            AddIR(ircall);
            if (_Functions.ContainsKey(func.Name)) _Functions[func.Name].UseCount++;
            return new FunctionReturnVariable(func.Name, func.ReturnType, currentScope.localVariables.Count, ircall);
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
        Variable asnVar = currentScope.allVariables[assign.Identifier.Value];

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
            Variable saveLocation = currentScope.allVariables[assign.Identifier.Value];
            Variable var = ParseExpression(assign.Value);

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
        int labelnum = _Labels.Count;

        //string conditionResultName = CreateNewTempVar(DataType.BOOL, "0");

        IRLabel startLabel = new($"WHILE_{labelnum}_START");
        IRLabel endLabel = new($"WHILE_{labelnum}_END");
        IRLabel condLabel = new($"WHILE_{labelnum}_COND");

        //IRCompare comp = ParseCondition(whilestmt.Condition /*, _tempVariables[conditionResultName]*/);
        
        AddIR(new IRJump(condLabel.labelName, ConditionType.NONE));
        AddIR(startLabel);
        ParseScope(whilestmt.Scope, currentScope, ScopeType.LOOP);
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
                        AddIR(new IRJump(falseLabel.labelName, oppositeCondition[_cond.Conditions[i].ConditionType]));
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
        Variable leftNodeEvalResult = ParseExpression(cond.LeftNode);
        Variable rightNodeEvalResult;
        if (cond.RightNode != null)
            rightNodeEvalResult = ParseExpression(cond.RightNode);
        else
            rightNodeEvalResult = new LiteralVariable("1", Types[DataType.INT]);
        return new IRCompare(leftNodeEvalResult, rightNodeEvalResult);
    }

    private void ParseIf(ASTIf ifstmt)
    {
        int labelnum = _Labels.Count;
        IRLabel label = new($"IF_{labelnum}_START");
        IRLabel bodyLabel = new($"IF_{labelnum}_BODY");
        IRLabel endLabel = new($"IF_{labelnum}_END");

        //IRCompare comp = ParseCondition(ifstmt.Condition);
        //AddIR(comp);
        ParseLogicalCondition(ifstmt.Condition, bodyLabel, endLabel);
        //AddIR(new IRJump(label.labelName, oppositeCondition[ifstmt.Condition.ConditionType]));

        AddIR(bodyLabel);
        ParseScope(ifstmt.Scope, currentScope, ScopeType.IF);

        if (ifstmt.Pred != null)
        {    
            AddIR(new IRJump(endLabel.labelName, ConditionType.NONE));
            AddIR(label);
            ParseIfPred(ifstmt.Pred, endLabel);
            AddIR(endLabel);
        }
        else
        {
            AddIR(label);
            AddIR(endLabel);
        }
    }
    
    private void ParseIfPred(ASTIfPred pred, IRLabel endLabel)
    {
        int labelNum = _Labels.Count;

        if (pred is ASTElifPred elif)
        {
            //string conditionResultName = CreateNewTempVar(DataType.BOOL, "0");

            //IRCompare comp = ParseCondition(elif.Condition /*, _tempVariables[conditionResultName]*/);
            //AddIR(comp);
            


            IRLabel label = new($"ELSE_{labelNum}_START");
            if (elif.Pred != null)
                ParseLogicalCondition(elif.Condition, null, label);
                //AddIR(new IRJump(label.labelName, oppositeCondition[elif.Condition.ConditionType]));
            else
                ParseLogicalCondition(elif.Condition, null, endLabel);
                //AddIR(new IRJump(endLabel.labelName, oppositeCondition[elif.Condition.ConditionType]));

            ParseScope(elif.Scope, currentScope, ScopeType.ELIF);

            AddIR(new IRJump(endLabel.labelName, ConditionType.NONE));

            if (elif.Pred != null)
            {
                AddIR(label);
                ParseIfPred(elif.Pred, endLabel);
            }
        }
        else if (pred is ASTElsePred elsepred)
        {
            ParseScope(elsepred.Scope, currentScope, ScopeType.ELSE);
        }
    }

    private Variable ParseExpression(ASTExpression _expression)
    {
        if (_expression is ASTIdentifier identifier)
        {
            currentScope.VariableExistsErr(identifier.token);
            return currentScope.allVariables[identifier.Name];
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
                Variable parsedItem = ParseExpression(item);
                vals.Add(parsedItem.value);
            }

            val = string.Join(", ", vals);

            return new ArrayVariable(arrayConstructor.Type, val, arrayConstructor.Length);
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
            TempVariable result = _TempVariables[resultName];

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
        public string Name;
        public TypeSystem.Type ReturnType = Types[DataType.VOID];
        public List<NamedVariable> Parameters = new();
        public List<IRNode> Nodes = new();
        public int UseCount;

        public bool WasUsed => UseCount > 0;

        public IRFunction(string name, TypeSystem.Type returnType, List<NamedVariable> parameters)
        {
            base.Name = "FUNC";
            this.Name = name;
            if (returnType != null) ReturnType = returnType;
            Parameters = parameters;
        }

        public override string GetString()
        {
            string r = $"({base.Name}, {Name}, {ReturnType}";
            foreach (NamedVariable parameter in Parameters) r += $", {parameter.variableName}";

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
            if (val is FunctionReturnVariable) SetValue("rax", val.variableType);
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

            _AllVariables[guid.ToString()] = this;
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

            _AllVariables.Add(guid.ToString(), this);
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

            _AllVariables.Add(guid.ToString(), this);
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
                if (!_StringLiterals.Reverse.Contains(value))
                {
                    string name = "STR_" + _StringLiterals.Forward.Count.ToString();
                    _StringLiterals.Add(name, value);
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

            _AllVariables.Add(guid.ToString(), this);
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

            _AllVariables.Add(guid.ToString(), this);
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
        public ArrayIndexedVariable indexedArray; //todo fix

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

            if (_Labels.ContainsKey(labelName)) ErrorHandler.Custom($"Label {labelName} already exists!");

            _Labels.Add(labelName, this);
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
                   (_GlobalVariables.ContainsKey(name) && _GlobalVariables[name] != null);
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