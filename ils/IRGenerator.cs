using Iced.Intel;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static ils.ASTCondition;
using static ils.IRGenerator;
using static ils.IRGenerator.NamedVariable;
using static ils.TypeSystem;

namespace ils
{
    public class IRGenerator
    {
        public const string MAIN_FUNCTION_LABEL = "FUNC_MAIN_START";
        public const string MAIN_FUNCTION_NAME = "main";

        protected static Dictionary<string, IRLabel> _labels = new();

        protected static Dictionary<string, NamedVariable> _globalVariables = new();
        protected static Dictionary<string, TempVariable> _tempVariables = new();
        protected static Dictionary<string, IRFunction> _functions = new();

        public static Map<string, string> stringLiterals = new();

        public List<IRNode> IR = new();

        public static Dictionary<string, Variable> _allVariables = new();

        private Dictionary<ASTScope, ScopeLabels> _scopeLabels = new();

        private Scope _currentScope;

        private Scope _mainScope;
        private Dictionary<int, Scope> _scopes = new();

        private ScopeLabels _currentFuncScope;
        private IRFunction _currentFunction;

        private Dictionary<ConditionType, ConditionType> oppositeCondition = new()
        {
            { ConditionType.EQUAL, ConditionType.NOT_EQUAL },
            { ConditionType.NOT_EQUAL, ConditionType.EQUAL },
            { ConditionType.LESS, ConditionType.GREATER_EQUAL },
            { ConditionType.LESS_EQUAL, ConditionType.GREATER },
            { ConditionType.GREATER, ConditionType.LESS_EQUAL },
            { ConditionType.GREATER_EQUAL, ConditionType.LESS },
            { ConditionType.NONE, ConditionType.NONE },
        };

        public void AddIR(IRNode node)
        {
            if (_currentFunction != null)
            {
                _currentFunction.nodes.Add(node);
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
            foreach (IRNode irNode in IR)
            {
                Console.WriteLine(irNode.GetString());
            }

            /*foreach (var func in _functions.Values)
            {
                IR.Add(func);
            }*/

            return IR;
        }

        private string CreateNewTempVar(TypeSystem.Type varType, string value, string name = "")
        {
            string varName = $"TEMP_{(name != "" ? name+"_" : "")}{_allVariables.Keys.Count}";
            TempVariable tempVar = new TempVariable(varName, varType, value);
            _tempVariables.Add(varName, tempVar);
            AddIR(tempVar);
            return varName;
        }

        private void ParseScope(ASTScope astScope, Scope parentScope, ScopeType scopeType, ASTStatement parentNode = null)
        {
            if(_mainScope == null)
            {
                _mainScope = new(0, scopeType);
                _scopes.Add(0, _mainScope);
                _currentScope = _mainScope;
            }
            else
            {
                _currentScope = new(_scopes.Keys.Count, scopeType);
                _currentScope.SetParent(parentScope);
                _scopes.Add(_currentScope.id, _currentScope);
            }

            IRLabel scopeStart = null;
            IRLabel scopeEnd = null;

            IRFunctionPrologue? funcPrologue = null;
            if(scopeType == ScopeType.FUNCTION)
            {
                funcPrologue = new IRFunctionPrologue();
                AddIR(funcPrologue);
            }
          
            switch (scopeType)
            {
                case ScopeType.DEFAULT:
                    scopeStart = new($"SCOPE_{_currentScope.id}_START");
                    scopeEnd = new($"SCOPE_{_currentScope.id}_END");
                    break;
                case ScopeType.IF:
                    scopeStart = new($"IF_{_currentScope.id}_START");
                    scopeEnd = new($"IF_{_currentScope.id}_END");
                    break;
                case ScopeType.ELIF:
                    scopeStart = new($"ELIF_{_currentScope.id}_START");
                    scopeEnd = new($"ELIF_{_currentScope.id}_END");
                    break;
                case ScopeType.ELSE:
                    scopeStart = new($"ELSE_{_currentScope.id}_START");
                    scopeEnd = new($"ELSE_{_currentScope.id}_END");
                    break;
                case ScopeType.LOOP:
                    scopeStart = new($"LOOP_{_currentScope.id}_START");
                    scopeEnd = new($"LOOP_{_currentScope.id}_END");
                    break;
                case ScopeType.FUNCTION:
                    ASTFunction fun = parentNode as ASTFunction;
                    if (fun.Identifier.Value == "main") //We are in the main scope
                    {
                        scopeStart = new(MAIN_FUNCTION_LABEL);
                    }
                    else
                    {
                        scopeStart = new($"FUNC_{fun.Identifier.Value}_START");
                    }
                    scopeEnd = new($"FUNC_{fun.Identifier.Value}_END");
                    break;
            }        
            _scopeLabels.Add(astScope, new ScopeLabels() { startLabel = scopeStart, endLabel = scopeEnd });
            if (_currentScope.id != 0)
            {
                AddIR(scopeStart);
                if (scopeType == ScopeType.FUNCTION)
                {
                    _currentFuncScope = _scopeLabels[astScope];
                    AddIR(new IRScopeStart(_currentScope));
                }
            }          

            if(_currentScope.id == 0) //We are in the main scope
            {
                List<ASTVariableDeclaration> vars = astScope.GetStatementsOfType<ASTVariableDeclaration>().ToList();
                foreach (ASTVariableDeclaration dec in vars) 
                {
                    _globalVariables.Add(dec.Name.Value, null);
                    ParseVarialbeDeclaration(dec, astScope);
                }

                List<ASTFunction> funcs = astScope.GetStatementsOfType<ASTFunction>().ToList();
                foreach (ASTFunction func in funcs)
                {
                    string identifier = func.Identifier.Value;

                    List<NamedVariable> parameters = new();
                    foreach (var parameter in func.Parameters)
                    {
                        NamedVariable par = new NamedVariable(parameter, isGlobal: false, isFuncArg: true);
                        parameters.Add(par);
                    }

                    IRFunction irfunc = new IRFunction(identifier, func.ReturnType != null ? func.ReturnType : null, parameters);
                    
                    _functions.Add(func.Identifier.Value, irfunc);
                }
            }

            if(_currentScope.scopeType == ScopeType.FUNCTION && parentNode is ASTFunction parenAstFunc)
            {
                foreach (var parameter in _functions[parenAstFunc.Identifier.Value].parameters)
                {
                    _currentScope.AddLocalVariable(parameter);
                }             
            }

            IRScopeEnd irScopeEnd = new IRScopeEnd(_currentScope);

            foreach (ASTStatement statement in astScope.Statements)
            {
                ParseStatement(statement, astScope, parentNode, irScopeEnd, scopeStart, scopeEnd);
            }

            foreach (var temp in _tempVariables)
            {
                AddIR(new IRDestroyTemp(temp.Key));
            }

            foreach (var local in _currentScope.localVariables)
            {
                if (local.Value is TempVariable)
                {
                    AddIR(new IRDestroyTemp(local.Key));
                }
                if(local.Value is NamedVariable named && !named.isGlobal && !named.isFuncArg)
                {
                    AddIR(new IRDestroyTemp(local.Key));
                }
            }

            _tempVariables.Clear();

            if (_currentScope.id != 0)
            {
                AddIR(scopeEnd);
                if (scopeType == ScopeType.FUNCTION)
                {
                    funcPrologue.localVariables = _currentScope.localVariables.Count;
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
                _currentFunction = _functions[func.Identifier.Value];
                AddIR(_currentFunction);
                ParseScope(func.Scope, _currentScope, ScopeType.FUNCTION, func);
            }
            else if (statement is ASTVariableDeclaration varDeclaration)
            {
                ParseVarialbeDeclaration(varDeclaration, astScope);
            }
            else if (statement is ASTScope scope)
            {
                ParseScope(scope, _currentScope, ScopeType.DEFAULT);
            }
            else if (statement is ASTArrayIndex index)
            {
                ParseIndex(index);
            }
            else if (statement is ASTReturn ret)
            {
                IRReturn irret = new IRReturn(ParseExpression(ret.Value), 0);

                if (parentNode != null && parentNode is ASTFunction astFunc)
                {
                    irScopeEnd.valuesToClear = astFunc.Parameters.Count;
                }

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
            else if(statement is ASTArrayDeclaration arrayDecl) 
            {
                ParseArrayDeclaration(arrayDecl);
            }
        }

        private void ParseArrayDeclaration(ASTArrayDeclaration array)
        {

        }

        private void ParseIndex(ASTArrayIndex index)
        {
            //todo dodac sprawdzanie czy array istnieje, czy index nie za duzy itd
            Variable idnexVar = ParseExpression(index.index);
        }

        private void ParseBreak(ASTScope scope, IRLabel scopeStart, IRLabel scopeEnd)
        {
            if (scope.ScopeType == ScopeType.IF)
            {
                ASTScope parentScopeOfType = GetParentScopeOfType(ScopeType.LOOP, scope);

                if (parentScopeOfType != null)
                {
                    if (parentScopeOfType.ScopeType == ScopeType.LOOP)
                    {
                        AddIR(new IRJump(_scopeLabels[parentScopeOfType].endLabel.labelName, ConditionType.NONE));
                    }
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
                if (_currentScope.id == 0) return;
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
                        newVar = new NamedVariable(vardec, isGlobal: false, isFuncArg: false);
                        break;
                    case ScopeType.DEFAULT:
                        newVar = new NamedVariable(vardec, isGlobal: true, isFuncArg: false);
                        break;
                }

                if (_currentScope.id == 0)
                {
                    _globalVariables[vardec.Name.Value] = (NamedVariable)newVar;
                }

                //_variables.Add(newVar.variableName, newVar);
                _currentScope.AddLocalVariable(newVar);            

                if (vardec.Value != null)
                {
                    Variable var = ParseExpression(vardec.Value);

                    newVar.AssignVariable(var);
                }

                AddIR(newVar);
            }
        }

        private Variable ParseFunctionCall(ASTFunctionCall call)
        {
            IRFunction func = null;

            if(call.Identifier.Value.StartsWith('@') && !Builtins.IsBuiltIn(call.Identifier.Value))
            {
                ErrorHandler.Custom($"Builtin function '{call.Identifier.Value} doesn't exist!");
                return null;
            }

            if(!_functions.ContainsKey(call.Identifier.Value) && !Builtins.BuiltinFunctions.Any(x => x.name == call.Identifier.Value))
            {
                ErrorHandler.Custom($"Function '{call.Identifier.Value}' does not exist!");
                return null;
            }
            else if(_functions.ContainsKey(call.Identifier.Value))
            {
                func = _functions[call.Identifier.Value];
            }

            List<Variable> arguments = new();

            foreach (var argument in call.Arguments)
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
                else if(arg is FunctionReturnVariable)
                {
                    arguments.Add(arg);
                }
                else
                {
                    arguments.Add(arg);
                }
           
            }

            if (_functions.ContainsKey(call.Identifier.Value) && arguments.Count != _functions[call.Identifier.Value].parameters.Count)
            {
                ErrorHandler.Custom($"Function '{call.Identifier.Value}' takes {_functions[call.Identifier.Value].parameters.Count} arguments, you provided {arguments.Count}!");
                return null;
            }

            //add return somehow 

            IRFunctionCall ircall = new IRFunctionCall(call.Identifier.Value, arguments);
            if (func != null && func.returnType.DataType != DataType.VOID)
            {
                //_IR.Add(ircall); //na razie nie pozwole na wywołanie funkcji która nie zwraca voida i nie jest przypisywana nigdzie
                return new FunctionReturnVariable(func.name, func.returnType, _currentScope.localVariables.Count, ircall);
            }
            else
            {
                AddIR(ircall);
                foreach (var arg in arguments)
                {
                    arg.needsPreservedReg = true;
                }
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
                {
                    return true;
                }
                else if (expr is ASTArithmeticOperation operation)
                {
                    return CanEvalArtihmeticExpr(operation);
                }
                return false;
            }
            return CheckNode(op.LeftNode) && CheckNode(op.RightNode);
        }

        private void ParseAssign(ASTAssign assign)
        {
            Variable asnVar = _currentScope.allVariables[assign.identifier.Value];

            if (assign.value is ASTIdentifier identifier)
            {
                AddIR(new IRAssign(asnVar, identifier.name, TypeSystem.Types[DataType.IDENTIFIER]));
            }
            else if (assign.value is ASTIntLiteral intLiteral)
            {
                AddIR(new IRAssign(asnVar, intLiteral.value.ToString(), TypeSystem.Types[DataType.INT]));
            }
            else if (assign.value is ASTStringLiteral strLiteral)
            {
                AddIR(new IRAssign(asnVar, strLiteral.value, TypeSystem.Types[DataType.STRING]));
            }
            else if (assign.value is ASTCharLiteral charLiteral)
            {
                AddIR(new IRAssign(asnVar, charLiteral.value.ToString(), TypeSystem.Types[DataType.CHAR]));
            }
            else if (assign.value is ASTBoolLiteral boolLiteral)
            {
                AddIR(new IRAssign(asnVar, boolLiteral.value.ToString(), TypeSystem.Types[DataType.BOOL]));
            }
            else
            {
                Variable saveLocation = _currentScope.allVariables[assign.identifier.Value];
                Variable var = ParseExpression(assign.value);

                if (var is TempVariable tempVar)
                {
                    AddIR(new IRAssign(saveLocation, tempVar.variableName, TypeSystem.Types[DataType.IDENTIFIER]));
                }
                if (var is NamedVariable namedVar)
                {
                    AddIR(new IRAssign(saveLocation, namedVar.variableName, TypeSystem.Types[DataType.IDENTIFIER]));
                }
                if (var is LiteralVariable literalVar)
                {
                    AddIR(new IRAssign(saveLocation, literalVar.value.ToString(), literalVar.variableType));
                }
                if (var is FunctionReturnVariable regVar)
                {
                    //_IR.Add(new IRAssign(saveLocation, regVar.value.ToString(), regVar.variableType));
                    AddIR(new IRAssign(saveLocation, regVar.variableName, TypeSystem.Types[DataType.IDENTIFIER]));
                }
            }
        }
     
        private void ParseWhile(ASTWhile whilestmt)
        {
            int labelnum = _labels.Count;

            //string conditionResultName = CreateNewTempVar(DataType.BOOL, "0");

            IRLabel startLabel = new($"WHILE_{labelnum}_START");
            IRLabel endLabel = new($"WHILE_{labelnum}_END");

            IRCompare comp = ParseCondition(whilestmt.cond/*, _tempVariables[conditionResultName]*/);

            AddIR(startLabel);
            AddIR(comp);
            AddIR(new IRJump(endLabel.labelName, oppositeCondition[whilestmt.cond.conditionType]));

            ParseScope(whilestmt.scope, _currentScope, ScopeType.LOOP);
            AddIR(new IRJump(startLabel.labelName, ConditionType.NONE));

            AddIR(endLabel);
        }

        private IRCompare ParseCondition(ASTCondition cond) 
        {
            Variable leftNodeEvalResult = ParseExpression(cond.leftNode);

            Variable rightNodeEvalResult;

            if (cond.rightNode != null)
            {
                rightNodeEvalResult = ParseExpression(cond.rightNode);
            }
            else
            {
                rightNodeEvalResult = new LiteralVariable("1", TypeSystem.Types[DataType.INT]);
            }

            return new IRCompare(leftNodeEvalResult, rightNodeEvalResult);
        }

        private void ParseIf(ASTIf ifstmt)
        {
            int labelnum = _labels.Count;
            IRLabel label = new($"IF_{labelnum}_START");

            IRCompare comp = ParseCondition(ifstmt.cond);
            AddIR(comp);
            AddIR(new IRJump(label.labelName, oppositeCondition[ifstmt.cond.conditionType]));

            ParseScope(ifstmt.scope, _currentScope, ScopeType.IF);

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

            if(pred is ASTElifPred elif)
            {
                //string conditionResultName = CreateNewTempVar(DataType.BOOL, "0");

                IRCompare comp = ParseCondition(elif.cond/*, _tempVariables[conditionResultName]*/);
                AddIR(comp);

                IRLabel label = new($"ELSE_{labelNum}_START");
                if (elif.pred != null)
                {
                    AddIR(new IRJump(label.labelName, oppositeCondition[elif.cond.conditionType]));
                } 
                else
                {
                    AddIR(new IRJump(endLabel.labelName, oppositeCondition[elif.cond.conditionType]));
                }

                ParseScope(elif.scope, _currentScope, ScopeType.ELIF);

                AddIR(new IRJump(endLabel.labelName, ConditionType.NONE));

                if (elif.pred != null)
                {
                    AddIR(label);
                    ParseIfPred(elif.pred, endLabel);
                }
            }
            else if(pred is ASTElsePred elsepred)
            {
                ParseScope(elsepred.scope, _currentScope, ScopeType.ELSE);
            }  
        }

        private Variable ParseExpression(ASTExpression _expression)
        {
            if (_expression is ASTIdentifier identifier)
            {
                if(_currentScope.allVariables.ContainsKey(identifier.name))
                {
                    return _currentScope.allVariables[identifier.name];
                }
                else
                {
                    ErrorHandler.Custom($"Variable '{identifier.name}' does not exist!");
                    return null;
                }
            }
            else if(_expression is ASTLiteral literal)
            {
                return new LiteralVariable(literal.value, literal.variableType);
            }    
            else if (_expression is ASTFunctionCall funcCall)
            {
                return ParseFunctionCall(funcCall);
            }
            else if(_expression is ASTArrayConstructor arrayConstructor)
            {
                string val = "";
                List<string> vals = new();

                foreach (var item in arrayConstructor.values)
                {
                    Variable parsedItem = ParseExpression(item);
                    vals.Add(parsedItem.value);
                }

                val = String.Join(", ", vals);

                return new ArrayVariable(arrayConstructor.type, val);
            }
            else if (_expression is ASTArithmeticOperation arithmeticOp)
            {
                Variable leftVar = ParseExpression(arithmeticOp.LeftNode);

                Variable rightVar = ParseExpression(arithmeticOp.RightNode);

                if(!VerifyOperation(leftVar.variableType.DataType, rightVar.variableType.DataType, arithmeticOp.Operation))
                {
                    return null;
                }

                if (IRGenerator.CanEvalArtihmeticExpr(arithmeticOp))
                {
                    var x = MathEvaluator.Evaluate(arithmeticOp);
                    return new LiteralVariable(x.ToString(), TypeSystem.Types[DataType.INT]);
                }

                string resultName = CreateNewTempVar(TypeSystem.Types[DataType.INT], "0", "OP_RES");
                TempVariable result = _tempVariables[resultName];

                AddIR(new IRArithmeticOp(result, leftVar, rightVar, arithmeticOp.Operation));
                if(leftVar is TempVariable) AddIR(new IRDestroyTemp(leftVar.variableName));
                if(rightVar is TempVariable) AddIR(new IRDestroyTemp(rightVar.variableName));
                return result;
            }

            return null;
        }

        private bool VerifyOperation(DataType aType, DataType bType, ArithmeticOpType opType)
        {
            if(opType is (ArithmeticOpType.ADD or ArithmeticOpType.MUL or ArithmeticOpType.DIV or ArithmeticOpType.SUB or ArithmeticOpType.MOD))
            {
                if(aType == DataType.BOOL || bType == DataType.BOOL)
                {
                    ErrorHandler.Custom("You can't do arithmetic operations on bools!");
                    return false;
                }
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
            public TypeSystem.Type returnType = TypeSystem.Types[DataType.VOID];
            public List<NamedVariable> parameters = new();
            public List<IRNode> nodes = new();

            public IRFunction(string name, TypeSystem.Type returnType, List<NamedVariable> parameters)
            {
                Name = "FUNC";
                this.name = name;
                if (returnType != null)
                {
                    this.returnType = returnType;
                }
                this.parameters = parameters;
            }

            public override string GetString()
            {
                string r = $"({Name}, {name}, {returnType}";
                foreach (var parameter in parameters)
                {
                    r += $", {parameter.variableName}";
                }

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
                foreach (var parameter in arguments)
                {
                    r += $", {parameter.variableName}";
                }

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

            public IRNode lastUse = null;

            public bool needsPreservedReg = false;

            //public Guid guid;
            public string guid;

            public Variable()
            {
                //guid = Guid.NewGuid();
                guid = IRGenerator.NewId();
            }

            public void SetValue(string val, TypeSystem.Type valType)
            {
                this.variableType = valType;

                this.value = val;
            }

            public void AssignVariable(Variable val)
            {
                if (val is TempVariable tempVar)
                {
                    SetValue(tempVar.guid.ToString(), TypeSystem.Types[DataType.IDENTIFIER]);
                }
                if (val is NamedVariable namedVar)
                {
                    SetValue(namedVar.guid.ToString(), TypeSystem.Types[DataType.IDENTIFIER]);
                }
                if (val is LiteralVariable literalVar)
                {
                    SetValue(literalVar.value, literalVar.variableType);
                }
                if (val is FunctionReturnVariable regvar)
                {
                    SetValue(regvar.funcName + regvar.index.ToString(), val.variableType);
                }
                if(val is ArrayVariable arrayvar)
                {
                    SetValue(arrayvar.value, arrayvar.variableType);
                }
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
                this.variableName = funcName;
                this.variableType = varType;
                this.index = index;
                this.call = call;
                //SetValue(reg, variableType);

                _allVariables[this.guid.ToString()] = this;
            }

            public override string GetString()
            {
                return $"({Name}, {variableName})";
            }
        }

        public class NamedVariable : Variable
        {
            public bool isGlobal = false;
            public bool isFuncArg = false;

            public NamedVariable(ASTVariableDeclaration declaration, bool isGlobal, bool isFuncArg)
            {
                Name = "NAMED_VAR";

                this.variableName = declaration.Name.Value;
                this.isGlobal = isGlobal;
                this.isFuncArg = isFuncArg;

                this.variableType = declaration.Type;

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
                        {
                            if (declaration.Value == null) SetValue($"{arrayType.size * 4}", variableType);
                        }
                        break;
                }

                _allVariables.Add(guid.ToString(), this);
            }

            public override string GetString()
            {
                return $"({Name}, {variableName}, {variableType}, {value})";
            }
        }

        public class TempVariable : Variable
        {
            public TempVariable(string variableName, TypeSystem.Type varType, string value)
            {
                Name = "TEMP_VAR";
                this.variableName = variableName;
                this.variableType = varType;
                SetValue(value, variableType);

                _allVariables.Add(guid.ToString(), this);
            }

            public override string GetString()
            {
                return $"({Name}, {guid}, {value})";
            }
        }

        public class LiteralVariable : Variable
        {
            public LiteralVariable(string value, TypeSystem.Type type)
            {
                Name = "LIT_VAR";

                this.variableType = type;
                //this.variableName = $"LIT_{literalVarsCount}_{value}";

                if(type.DataType == DataType.STRING)
                {
                    if (!stringLiterals.Reverse.Contains(value))
                    {
                        string name = "STR_" + stringLiterals.Forward.Count.ToString();
                        stringLiterals.Add(name, value);
                        SetValue(name, variableType);
                    }
                    else
                    {
                        SetValue(value, variableType);
                    }                 
                }
                else
                {
                    this.variableName = value.ToString();
                    SetValue(value, variableType);
                }               

                _allVariables.Add(guid.ToString(), this);
            }

            

            public override string GetString()
            {
                return $"({Name}, {value})";
            }
        }

        public class ArrayVariable : Variable
        {
            public ArrayVariable(TypeSystem.Type varType, string value)
            {
                Name = "ARRAY_VAR";
                this.variableType = varType;
                SetValue(value, variableType);

                _allVariables.Add(guid.ToString(), this);
            }

            public override string GetString()
            {
                return $"({Name}, {value})";
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

                this.labelName = name;

                if(_labels.ContainsKey(labelName))
                {
                    ErrorHandler.Custom($"Label {labelName} already exists!");
                }

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

            public bool VariableExists(string name)
            {
                return (allVariables.ContainsKey(name) && allVariables[name] != null) ||
                    (_globalVariables.ContainsKey(name) && _globalVariables[name] != null);
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
}
