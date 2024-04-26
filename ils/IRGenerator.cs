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

namespace ils
{
    public class IRGenerator
    {
        public enum DataType { STRING, INT, CHAR, BOOL, IDENTIFIER, VOID }
        protected static Dictionary<string, IRLabel> _labels = new();

        protected static Dictionary<string, NamedVariable> _globalVariables = new();
        protected static Dictionary<string, TempVariable> _tempVariables = new();
        protected static Dictionary<string, IRFunction> _functions = new();

        public List<IRNode> _IR = new();

        public static Dictionary<string, Variable> _allVariables = new();

        public static int literalVarsCount = 0;

        private Dictionary<ASTScope, ScopeLabels> _scopeLabels = new();

        private Scope _currentScope;

        private Scope _mainScope;
        private Dictionary<int, Scope> _scopes = new();

        private ScopeLabels _currentFunc;

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

        public List<IRNode> Generate(ASTScope mainScope)
        {
            ParseScope(mainScope, null, ScopeType.DEFAULT);

            Console.WriteLine("\n");
            foreach (IRNode irNode in _IR)
            {
                Console.WriteLine(irNode.GetString());
            }

            return _IR;
        }

        private string CreateNewTempVar(DataType varType, string value, string name = "")
        {
            string varName = $"TEMP_{(name != "" ? name+"_" : "")}{_allVariables.Keys.Count}";
            TempVariable tempVar = new TempVariable(varName, varType, value);
            _tempVariables.Add(varName, tempVar);
            _IR.Add(tempVar);
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

            IRFunctionPrologue? funcPrologue = null;
            if(scopeType == ScopeType.FUNCTION)
            {
                funcPrologue = new IRFunctionPrologue();
                _IR.Add(funcPrologue);
            }
            
            IRLabel scopeStart = new($"SCOPE_{_currentScope.id}_START");
            IRLabel scopeEnd = new($"SCOPE_{_currentScope.id}_END");
            _scopeLabels.Add(astScope, new ScopeLabels() { startLabel = scopeStart, endLabel = scopeEnd });
            if (_currentScope.id != 0)
            {
                _IR.Add(scopeStart);
                if (scopeType == ScopeType.FUNCTION)
                {
                    _currentFunc = _scopeLabels[astScope];
                    _IR.Add(new IRScopeStart(_currentScope));
                }
            }          

            if(_currentScope.id == 0) //We are in the main scope
            {
                List<ASTVariableDeclaration> vars = astScope.GetStatementsOfType<ASTVariableDeclaration>();
                foreach (ASTVariableDeclaration dec in vars) 
                {
                    _globalVariables.Add(dec.name.value, null);
                    ParseVarialbeDeclaration(dec, astScope);
                }

                List<ASTFunction> funcs = astScope.GetStatementsOfType<ASTFunction>();
                foreach (ASTFunction func in funcs)
                {
                    string identifier = func.identifier.value;

                    List<NamedVariable> parameters = new();
                    foreach (var parameter in func.parameters)
                    {
                        NamedVariable par = new NamedVariable(parameter, isGlobal: false, isFuncArg: true);
                        parameters.Add(par);
                        //_currentScope.AddLocalVariable(par);
                        //add using arguments
                    }

                    IRFunction irfunc = new IRFunction(identifier, func.returnType != null ? func.returnType.tokenType : null, parameters);
                    
                    _functions.Add(func.identifier.value, irfunc);
                }
            }

            if(_currentScope.scopeType == ScopeType.FUNCTION && parentNode is ASTFunction parenAstFunc)
            {
                foreach (var parameter in _functions[parenAstFunc.identifier.value].parameters)
                {
                    _currentScope.AddLocalVariable(parameter);
                }             
            }

            IRScopeEnd irScopeEnd = new IRScopeEnd(_currentScope);


            foreach (ASTStatement statement in astScope.statements)
            {
                if(statement is ASTFunctionCall call)
                {
                    ParseFunctionCall(call);
                }
                else if(statement is ASTFunction func)
                {
                    _IR.Add(_functions[func.identifier.value]);
                    ParseScope(func.scope, _currentScope, ScopeType.FUNCTION, func);
                }
                else if(statement is ASTVariableDeclaration varDeclaration)
                {
                    ParseVarialbeDeclaration(varDeclaration, astScope);
                }
                else if(statement is ASTScope scope)
                {
                    ParseScope(scope, _currentScope, ScopeType.DEFAULT);
                }
                else if(statement is ASTReturn ret)
                {
                    IRReturn irret = new IRReturn(ParseExpression(ret.value), 0);
                    
                    if (parentNode != null && parentNode is ASTFunction astFunc)
                    {
                        irScopeEnd.valuesToClear = astFunc.parameters.Count;
                    }

                    _IR.Add(irret);
                    _IR.Add(new IRJump(_currentFunc.endLabel.labelName, ConditionType.NONE));

                    //AAAAAAAAA

                }
                else if(statement is ASTAssign assign)
                {
                    Variable asnVar = _currentScope.allVariables[assign.identifier.value];

                    if (assign.value is ASTIdentifier identifier)
                    {
                        _IR.Add(new IRAssign(asnVar, identifier.name, DataType.IDENTIFIER));
                    }
                    else if (assign.value is ASTIntLiteral intLiteral)
                    {
                        _IR.Add(new IRAssign(asnVar, intLiteral.value.ToString(), DataType.INT));
                    }
                    else if (assign.value is ASTStringLiteral strLiteral)
                    {
                        _IR.Add(new IRAssign(asnVar, strLiteral.value, DataType.STRING));
                    }
                    else if (assign.value is ASTCharLiteral charLiteral)
                    {
                        _IR.Add(new IRAssign(asnVar, charLiteral.value.ToString(), DataType.CHAR));
                    }
                    else if (assign.value is ASTBoolLiteral boolLiteral)
                    {
                        _IR.Add(new IRAssign(asnVar, boolLiteral.value.ToString(), DataType.BOOL));
                    }
                    else
                    {
                        Variable saveLocation = _currentScope.allVariables[assign.identifier.value];
                        Variable var = ParseExpression(assign.value);

                        if (var is TempVariable tempVar)
                        {
                            _IR.Add(new IRAssign(saveLocation, tempVar.variableName, DataType.IDENTIFIER));
                        }
                        if (var is NamedVariable namedVar)
                        {
                            _IR.Add(new IRAssign(saveLocation, namedVar.variableName, DataType.IDENTIFIER));
                        }
                        if (var is LiteralVariable literalVar)
                        {
                            _IR.Add(new IRAssign(saveLocation, literalVar.value.ToString(), literalVar.variableType));
                        }
                        if (var is FunctionReturnVariable regVar)
                        {
                            _IR.Add(new IRAssign(saveLocation, regVar.value.ToString(), regVar.variableType));
                        }
                    }
                }
                else if(statement is ASTIf ifstmt)
                {
                    ParseIf(ifstmt, _currentScope.allVariables);
                }
                else if(statement is ASTWhile whilestmt)
                {
                    ParseWhile(whilestmt);
                }
                else if(statement is ASTBreak breakstmt)
                {
                    if(astScope.scopeType == ScopeType.LOOP)
                    {
                        _IR.Add(new IRJump(scopeEnd.labelName, ConditionType.NONE));
                    }
                    else
                    {
                        ASTScope parentScopeOfType = GetParentScopeOfType(ScopeType.LOOP, astScope);

                        if(parentScopeOfType != null)
                        {
                            _IR.Add(new IRJump(_scopeLabels[parentScopeOfType].endLabel.labelName, ConditionType.NONE));
                        }
                        else
                        {
                            _IR.Add(new IRJump(scopeEnd.labelName, ConditionType.NONE));
                        }
                    }
                }    
            }

            foreach (var temp in _tempVariables)
            {
                _IR.Add(new IRDestroyTemp(temp.Key));
            }

            foreach (var local in _currentScope.localVariables)
            {
                if (local.Value is TempVariable)
                {
                    _IR.Add(new IRDestroyTemp(local.Key));
                }
                if(local.Value is NamedVariable named && !named.isGlobal && !named.isFuncArg)
                {
                    _IR.Add(new IRDestroyTemp(local.Key));
                }
            }

            _tempVariables.Clear();

            if (_currentScope.id != 0)
            {
                _IR.Add(scopeEnd);
                if (scopeType == ScopeType.FUNCTION)
                {
                    //jezu _currentScope.localVariables.Values.Select(x => x as NamedVariable).Where(x => !x.isFuncArg).Count();
                    funcPrologue.localVariables = _currentScope.localVariables.Count;
                    _IR.Add(new IRFunctionEpilogue());
                    _IR.Add(irScopeEnd);              
                }
            }

            _currentScope = parentScope;
        }

        private void ParseVarialbeDeclaration(ASTVariableDeclaration vardec, ASTScope scope)
        {
            if (_currentScope.VariableExists(vardec.name.value))
            {
                if (_currentScope.id == 0) return;
                ErrorHandler.Custom($"[{vardec.name.line}] Variable '{vardec.name.value}' already exists!'");
            }
            else
            {
                Variable newVar = null;
                switch (scope.scopeType)
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
                    _globalVariables[vardec.name.value] = (NamedVariable)newVar;
                }

                //_variables.Add(newVar.variableName, newVar);
                _currentScope.AddLocalVariable(newVar);
                _IR.Add(newVar);

                if (vardec.value != null)
                {
                    Variable var = ParseExpression(vardec.value);

                    newVar.AssignVariable(var);
                }
            }
        }

        private Variable ParseFunctionCall(ASTFunctionCall call)
        {
            IRFunction func = null;

            if(!_functions.ContainsKey(call.identifier.value) && !Builtins.BuiltinFunctions.Any(x => x.name == call.identifier.value))
            {
                ErrorHandler.Custom($"Function '{call.identifier.value}' does not exist!");
                return null;
            }
            else if(_functions.ContainsKey(call.identifier.value))
            {
                func = _functions[call.identifier.value];
            }

            List<Variable> arguments = new();

            foreach (var argument in call.arguemnts)
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
                else
                {
                    LiteralVariable lit = new LiteralVariable(arg.value, arg.variableType);
                    arguments.Add(lit);
                }
           
            }

            if (_functions.ContainsKey(call.identifier.value) && arguments.Count != _functions[call.identifier.value].parameters.Count)
            {
                ErrorHandler.Custom($"Function '{call.identifier.value}' takes {_functions[call.identifier.value].parameters.Count} arguments, you provided {arguments.Count}!");
                return null;
            }

            //add return somehow 

            IRFunctionCall ircall = new IRFunctionCall(call.identifier.value, arguments);
            if (func != null && func.returnType != DataType.VOID)
            {
                return new FunctionReturnVariable(func.name, func.returnType, _currentScope.localVariables.Count, ircall);
            }
            else
            {
                _IR.Add(ircall);
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
            if (scope.parentScope != null)
            {
                if (scope.parentScope.scopeType == scopeType) return scope.parentScope;
                else return GetParentScopeOfType(scopeType, scope.parentScope);
            }
            else
            {
                return null;
            }
        }
     
        private void ParseWhile(ASTWhile whilestmt)
        {
            int labelnum = _labels.Count;

            //string conditionResultName = CreateNewTempVar(DataType.BOOL, "0");

            IRLabel startLabel = new($"WHILE_{labelnum}_START");
            IRLabel endLabel = new($"WHILE_{labelnum}_END");

            IRCompare comp = ParseCondition(whilestmt.cond/*, _tempVariables[conditionResultName]*/);

            _IR.Add(startLabel);
            _IR.Add(comp);
            _IR.Add(new IRJump(endLabel.labelName, oppositeCondition[whilestmt.cond.conditionType]));

            ParseScope(whilestmt.scope, _currentScope, ScopeType.LOOP);
            _IR.Add(new IRJump(startLabel.labelName, ConditionType.NONE));

            _IR.Add(endLabel);
        }

        private IRCompare ParseCondition(ASTCondition cond/*, Variable result*/) 
        {
            //string leftNodeDestinationName = CreateNewTempVar(DataType.INT, "0");
            //Variable leftNodeDestination = _tempVariables[leftNodeDestinationName];
            Variable leftNodeEvalResult = ParseExpression(cond.leftNode);

            //leftNodeDestination.AssignVariable(leftNodeEvalResult);

            Variable rightNodeEvalResult;

            if (cond.rightNode != null)
            {
                //string rightNodeDestination = CreateNewTempVar(DataType.INT, "0");
                rightNodeEvalResult = ParseExpression(cond.rightNode);

                //_tempVariables[rightNodeDestination].AssignVariable(rightNodeEvalResult);
            }
            else
            {
                rightNodeEvalResult = new LiteralVariable("1", DataType.INT);
            }

            return new IRCompare(leftNodeEvalResult, rightNodeEvalResult);
        }

        private void ParseIf(ASTIf ifstmt, Dictionary<string, Variable> scopeVariables)
        {
            int labelnum = _labels.Count;

            //string conditionResultName = CreateNewTempVar(DataType.BOOL, "0");

            IRLabel label = new($"IF_{labelnum}");

            IRCompare comp = ParseCondition(ifstmt.cond/*, _tempVariables[conditionResultName]*/);

            //_IR.Add(new IRTest(conditionResultName));
            _IR.Add(comp);
            _IR.Add(new IRJump(label.labelName, oppositeCondition[ifstmt.cond.conditionType]));

            ParseScope(ifstmt.scope, _currentScope, ScopeType.IF);

            if (ifstmt.pred != null)
            {
                IRLabel endLabel = new($"IF_{labelnum}_END");
                _IR.Add(new IRJump(endLabel.labelName, ConditionType.NONE));
                _IR.Add(label);
                ParseIfPred(ifstmt.pred, endLabel);
                _IR.Add(endLabel);
            }
            else
            {
                _IR.Add(label);
            }
        }


        private void ParseIfPred(ASTIfPred pred, IRLabel endLabel)
        {
            int labelNum = _labels.Count;

            if(pred is ASTElifPred elif)
            {
                //string conditionResultName = CreateNewTempVar(DataType.BOOL, "0");

                IRCompare comp = ParseCondition(elif.cond/*, _tempVariables[conditionResultName]*/);
                _IR.Add(comp);

                IRLabel label = new($"ELSE_{labelNum}_START");
                if (elif.pred != null)
                {
                    _IR.Add(new IRJump(label.labelName, oppositeCondition[elif.cond.conditionType]));
                } 
                else
                {
                    _IR.Add(new IRJump(endLabel.labelName, oppositeCondition[elif.cond.conditionType]));
                }

                ParseScope(elif.scope, _currentScope, ScopeType.IF);

                _IR.Add(new IRJump(endLabel.labelName, ConditionType.NONE));

                if (elif.pred != null)
                {
                    _IR.Add(label);
                    ParseIfPred(elif.pred, endLabel);
                }
            }
            else if(pred is ASTElsePred elsepred)
            {
                ParseScope(elsepred.scope, _currentScope, ScopeType.IF);
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
            else if (_expression is ASTArithmeticOperation arithmeticOp)
            {
                Variable leftVar = ParseExpression(arithmeticOp.leftNode);

                Variable rightVar = ParseExpression(arithmeticOp.rightNode);

                string resultName = CreateNewTempVar(DataType.INT, "0", "OP_RES");
                TempVariable result = _tempVariables[resultName];

                _IR.Add(new IRArithmeticOp(result, leftVar, rightVar, arithmeticOp.operation));
                if(leftVar is TempVariable) _IR.Add(new IRDestroyTemp(leftVar.variableName));
                if(rightVar is TempVariable) _IR.Add(new IRDestroyTemp(rightVar.variableName));
                return result;
            }

            return null;
        }

        public class IRReturn : IRNode
        {
            public Variable ret;

            public IRReturn(Variable ret, int valuesOnStackToClear)
            {
                Name = "RET";

                this.ret = ret;
            }

            public override string GetString()
            {
                return $"({Name}, {ret.variableName})";
            }
        }

        public class IRDestroyTemp : IRNode
        {
            public string temp;

            public IRDestroyTemp(string temp)
            {
                Name = "DTP";
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
                Name = "NST";
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
                Name = "RST";
            }

            public override string GetString()
            {
                return $"({Name})";
            }
        }

        public class IRFunction : IRNode
        {
            public string name;
            public DataType returnType = DataType.VOID;
            public List<NamedVariable> parameters = new();

            public IRFunction(string name, TokenType? returnType, List<NamedVariable> parameters)
            {
                Name = "FUNC";
                this.name = name;
                if (returnType != null)
                {
                    switch (returnType)
                    {
                        case TokenType.TYPE_STRING: this.returnType = DataType.STRING; break;
                        case TokenType.TYPE_INT: this.returnType = DataType.INT; break;
                        case TokenType.TYPE_CHAR: this.returnType = DataType.CHAR; break;
                        case TokenType.TYPE_BOOLEAN: this.returnType = DataType.BOOL; break;
                        case TokenType.IDENTIFIER: this.returnType = DataType.IDENTIFIER; break;
                    }
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
                Name = "CALL";

                this.name = name;
                this.arguments = arguments;
            }

            public override string GetString()
            {
                string r = $"(CALL {name}";
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
                Name = "CMP";

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
                        Name = "JE";
                        break;
                    case ConditionType.NOT_EQUAL:
                        Name = "JNE";
                        break;
                    case ConditionType.LESS:
                        Name = "JL";
                        break;
                    case ConditionType.LESS_EQUAL:
                        Name = "JLE";
                        break;
                    case ConditionType.GREATER:
                        Name = "JG";
                        break;
                    case ConditionType.GREATER_EQUAL:
                        Name = "JGE";
                        break;
                    case ConditionType.NONE:
                        Name = "JMP";
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
            public DataType variableType;
            public string value { private set; get; }

            public IRNode lastUse = null;

            public bool needsPreservedReg = false;

            public Guid guid;

            public Variable()
            {
                guid = Guid.NewGuid();
            }

            public void SetValue(string val, DataType valType)
            {
                this.variableType = valType;


                switch (this.variableType)  
                {
                    case DataType.STRING:
                        this.value = $"\"{val}\"";
                        break;
                    case DataType.INT:
                        this.value = val;
                        break;
                    case DataType.CHAR:
                        this.value = val;
                        break;
                    case DataType.BOOL:
                        this.value = val;
                        break;
                    case DataType.IDENTIFIER:
                        this.value = val;
                        break;
                }
            }

            public void AssignVariable(Variable val)
            {
                if (val is TempVariable tempVar)
                {
                    SetValue(tempVar.guid.ToString(), DataType.IDENTIFIER);
                }
                if (val is NamedVariable namedVar)
                {
                    SetValue(namedVar.guid.ToString(), DataType.IDENTIFIER);
                }
                if (val is LiteralVariable literalVar)
                {
                    SetValue(literalVar.value, literalVar.variableType);
                }
                if (val is FunctionReturnVariable regvar)
                {
                    SetValue(regvar.funcName + regvar.index.ToString(), val.variableType);
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
            public FunctionReturnVariable(string funcName, DataType varType, int index, IRFunctionCall call)
            {
                Name = "FVAR";
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
                Name = "VAR";

                this.variableName = declaration.name.value;
                this.isGlobal = isGlobal;
                this.isFuncArg = isFuncArg;

                switch (declaration.type)
                {
                    case TokenType.TYPE_STRING:
                        variableType = DataType.STRING;
                        if (declaration.value == null) SetValue(@"\0", variableType);
                        break;
                    case TokenType.TYPE_INT:
                        variableType = DataType.INT;
                        if (declaration.value == null) SetValue("0", variableType);
                        break;
                    case TokenType.TYPE_CHAR:
                        variableType = DataType.CHAR;
                        if (declaration.value == null) SetValue("0", variableType);
                        break;
                    case TokenType.TYPE_BOOLEAN:
                        variableType = DataType.BOOL;
                        if (declaration.value == null) SetValue("0", variableType);
                        break;
                    case TokenType.IDENTIFIER:
                        variableType = DataType.IDENTIFIER;
                        if (declaration.value == null) SetValue("[]", variableType);
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
            public TempVariable(string variableName, DataType varType, string value)
            {
                Name = "TEMP";
                this.variableName = variableName;
                this.variableType = varType;
                SetValue(value, variableType);

                _allVariables.Add(guid.ToString(), this);
            }

            public override string GetString()
            {
                return $"({Name}, {variableName}, {value})";
            }
        }

        public class LiteralVariable : Variable
        {
            public LiteralVariable(string value, DataType type)
            {
                Name = "LIT";

                this.variableType = type;
                this.variableName = $"LIT_{literalVarsCount}_{value}";
                literalVarsCount++;

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
            public DataType assignedType;
            public string value;

            public IRAssign(Variable identifier, string value, DataType assignedType)
            {
                Name = "ASN";

                this.identifier = identifier;
                this.value = value;
                this.assignedType = assignedType;
            }

            public override string GetString()
            {
                return $"({Name}, {identifier.variableName} = {value})";
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
                return $"({Name}, {resultLocation.variableName} = {a.variableName}, {b.variableName})";
            }
        }

        public class IRScopeStart : IRNode
        {
            public Scope scope;

            public IRScopeStart(Scope scope)
            {
                Name = "START";
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
                Name = "END";
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
    }
}
