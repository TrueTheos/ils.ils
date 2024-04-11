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
        public enum VariableType { STRING, INT, CHAR, BOOL, IDENTIFIER, VOID }
        protected static Dictionary<string, IRLabel> _labels = new();

        protected static Dictionary<string, NamedVariable> _globalVariables = new();
        protected static Dictionary<string, TempVariable> _tempVariables = new();
        protected static Dictionary<string, IRFunction> _functions = new();

        public List<IRNode> _IR = new();

        private Dictionary<ASTScope, ScopeLabels> _scopeLabels = new();

        private Scope _currentScope;

        private Scope _mainScope;
        private Dictionary<int, Scope> _scopes = new();

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

        private string CreateNewTempVar(VariableType varType, string value, string name = "")
        {
            string varName = $"TEMP_{(name != "" ? name+"_" : "")}{_tempVariables.Keys.Count}";
            TempVariable tempVar = new TempVariable(varName, varType, value);
            _tempVariables.Add(varName, tempVar);
            _IR.Add(tempVar);
            return varName;
        }

        private void ParseScope(ASTScope astScope, Scope parentScope, ScopeType scopeType)
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
            
            IRLabel scopeStart = new($"SCOPE_{_currentScope.id}_START");
            if(_currentScope.id != 0) _IR.Add(scopeStart);
            IRLabel scopeEnd = new($"SCOPE_{_currentScope.id}_END");

            _scopeLabels.Add(astScope, new ScopeLabels() { startLabel = scopeStart, endLabel = scopeEnd });

            if(_currentScope.id == 0) //We are in the main scope
            {
                List<ASTVariableDeclaration> vars = astScope.GetStatementsOfType<ASTVariableDeclaration>();
                foreach (ASTVariableDeclaration dec in vars) 
                {
                    _globalVariables.Add(dec.name.value, null);
                }
            }

            foreach (ASTStatement statement in astScope.statements)
            {
                if(statement is ASTFunction func)
                {
                    ParseFunction(func);
                }
                else if(statement is ASTFunctionCall call)
                {
                    ParseFunctionCall(call);
                }
                else if(statement is ASTVariableDeclaration varDeclaration)
                {
                    if (_currentScope.VariableExists(varDeclaration.name.value))
                    {
                        ErrorHandler.Custom($"[{varDeclaration.name.line}] Variable '{varDeclaration.name.value}' already exists!'");
                    }
                    else
                    {
                        Variable newVar = null;
                        switch (astScope.scopeType)
                        {
                            case ScopeType.FUNCTION:
                            case ScopeType.LOOP:
                            case ScopeType.IF:
                                newVar = new LocalVariable(varDeclaration);
                                break;
                            case ScopeType.DEFAULT:
                                newVar = new NamedVariable(varDeclaration);
                                break;
                        }

                        if (_currentScope.id == 0)
                        {
                            _globalVariables[varDeclaration.name.value] = (NamedVariable)newVar;
                        }

                        //_variables.Add(newVar.variableName, newVar);
                        _currentScope.AddLocalVariable(newVar);
                        _IR.Add(newVar);

                        if(varDeclaration.value != null)
                        {                         
                            Variable var = ParseExpression(varDeclaration.value);

                            /*if(var is TempVariable tempVar)
                            {
                                _IR.Add(new IRAssign(newVar, tempVar.variableName, tempVar.variableType));
                            }
                            if(var is NamedVariable namedVar)
                            {
                                _IR.Add(new IRAssign(newVar, namedVar.variableName, namedVar.variableType));
                            }
                            if (var is LiteralVariable literalVar)
                            {
                                newVar.SetValue(literalVar.value);
                            }
                            if(var is LocalVariable localVar)
                            {
                                _IR.Add(new IRAssign(newVar, localVar.variableName, localVar.variableType));
                            }*/

                            if (var is TempVariable tempVar)
                            {
                                newVar.SetValue(tempVar.variableName);
                                newVar.variableType = VariableType.IDENTIFIER;
                            }
                            if (var is NamedVariable namedVar)
                            {
                                newVar.SetValue(namedVar.variableName);
                                newVar.variableType = VariableType.IDENTIFIER;
                            }
                            if (var is LiteralVariable literalVar)
                            {
                                newVar.SetValue(literalVar.value);
                                newVar.variableType = literalVar.variableType;
                            }
                            if (var is LocalVariable localVar)
                            {
                                newVar.SetValue(localVar.variableName);
                                newVar.variableType = VariableType.IDENTIFIER;
                            }
                        }
                    }
                }
                else if(statement is ASTScope scope)
                {
                    ParseScope(scope, _currentScope, ScopeType.DEFAULT);
                }
                else if(statement is ASTAssign assign)
                {
                    Variable asnVar = _currentScope.allVariables[assign.identifier.value];

                    if (assign.value is ASTIdentifier identifier)
                    {
                        _IR.Add(new IRAssign(asnVar, identifier.name, VariableType.IDENTIFIER));
                    }
                    else if (assign.value is ASTIntLiteral intLiteral)
                    {
                        _IR.Add(new IRAssign(asnVar, intLiteral.value.ToString(), VariableType.INT));
                    }
                    else if (assign.value is ASTStringLiteral strLiteral)
                    {
                        _IR.Add(new IRAssign(asnVar, strLiteral.value, VariableType.STRING));
                    }
                    else if (assign.value is ASTCharLiteral charLiteral)
                    {
                        _IR.Add(new IRAssign(asnVar, charLiteral.value.ToString(), VariableType.CHAR));
                    }
                    else if (assign.value is ASTBoolLiteral boolLiteral)
                    {
                        _IR.Add(new IRAssign(asnVar, boolLiteral.value.ToString(), VariableType.BOOL));
                    }
                    else
                    {
                        Variable saveLocation = _currentScope.allVariables[assign.identifier.value];
                        Variable var = ParseExpression(assign.value);

                        if (var is TempVariable tempVar)
                        {
                            _IR.Add(new IRAssign(saveLocation, tempVar.variableName, VariableType.IDENTIFIER));
                        }
                        if (var is NamedVariable namedVar)
                        {
                            _IR.Add(new IRAssign(saveLocation, namedVar.variableName, VariableType.IDENTIFIER));
                        }
                        if (var is LiteralVariable literalVar)
                        {
                            _IR.Add(new IRAssign(saveLocation, literalVar.value.ToString(), literalVar.variableType));
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
                        _IR.Add(new IRJump(scopeEnd.labelName));
                    }
                    else
                    {
                        ASTScope parentScopeOfType = GetParentScopeOfType(ScopeType.LOOP, astScope);

                        if(parentScopeOfType != null)
                        {
                            _IR.Add(new IRJump(_scopeLabels[parentScopeOfType].endLabel.labelName));
                        }
                        else
                        {
                            _IR.Add(new IRJump(scopeEnd.labelName));
                        }
                    }
                }

                foreach (var temp in _tempVariables)
                {
                    _IR.Add(new IRDestroyTemp(temp.Key));                    
                }

                _tempVariables.Clear();
            }

            if (_currentScope.id != 0) _IR.Add(scopeEnd);

            _currentScope = parentScope;
        }

        private void ParseFunction(ASTFunction func)
        {
            string identifier = func.identifier.value;

            List<LocalVariable> parameters = new();
            foreach (var parameter in func.parameters)
            {
                LocalVariable par = new LocalVariable(parameter);
                parameters.Add(par);
                _currentScope.AddLocalVariable(par);
                //_variables.Add(par.variableName, par);
            }

            IRFunction irfunc = new IRFunction(identifier, func.returnType != null ? func.returnType.tokenType : null, parameters);
            _IR.Add(irfunc);
            _functions.Add(identifier, irfunc);

            /*foreach (var parameter in irfunc.parameters)
            {
                _IR.Add(parameter);
            }*/

            _IR.Add(new IRNewStack());
            ParseScope(func.scope, _currentScope, ScopeType.FUNCTION);
            _IR.Add(new IRRestoreStack());
        }

        private void ParseFunctionCall(ASTFunctionCall call)
        {
            if(!_functions.ContainsKey(call.identifier.value) && !Builtins.BuiltinFunctions.Any(x => x.name == call.identifier.value))
            {
                ErrorHandler.Custom($"Function '{call.identifier.value}' does not exist!");
                return;
            }

            List<Variable> arguments = new();

            foreach (var argument in call.arguemnts)
            {
                Variable arg = ParseExpression(argument);

                string temp = CreateNewTempVar(arg.variableType, "0");
                TempVariable tempVar = _tempVariables[temp];

                tempVar.SetValue(arg.value);

                if (arg is TempVariable tmpVar)
                {
                    _IR.Add(new IRAssign(tempVar, tempVar.variableName, VariableType.IDENTIFIER));
                }
                if (arg is NamedVariable namedVar)
                {
                    _IR.Add(new IRAssign(tempVar, namedVar.variableName, VariableType.IDENTIFIER));
                }
                if (arg is LiteralVariable literalVar)
                {
                    tempVar.SetValue(literalVar.value);
                }

                arguments.Add(tempVar);
            }

            if (_functions.ContainsKey(call.identifier.value) && arguments.Count != _functions[call.identifier.value].parameters.Count)
            {
                ErrorHandler.Custom($"Function '{call.identifier.value}' takes {_functions[call.identifier.value].parameters.Count} arguments, you provided {arguments.Count}!");
                return;
            }

            _IR.Add(new IRFunctionCall(call.identifier.value, arguments));
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

        private void ParseIf(ASTIf ifstmt, Dictionary<string, Variable> scopeVariables)
        {
            int labelnum = _labels.Count;

            string conditionResultName = CreateNewTempVar(VariableType.BOOL, "0");

            IRLabel label = new($"IF_{labelnum}");

            ParseCondition(ifstmt.cond, _tempVariables[conditionResultName]);

            _IR.Add(new IRTest(conditionResultName));
            _IR.Add(new IRJumpZero(label.labelName));

            ParseScope(ifstmt.scope, _currentScope, ScopeType.IF);

            if(ifstmt.pred != null)
            {
                IRLabel endLabel = new($"IF_{labelnum}_END");
                _IR.Add(new IRJump(endLabel.labelName));
                _IR.Add(label);
                ParseIfPred(ifstmt.pred, endLabel);
                _IR.Add(endLabel);
            }
            else
            {
                _IR.Add(label);
            }
        }

        private void ParseWhile(ASTWhile whilestmt)
        {
            int labelnum = _labels.Count;

            string conditionResultName = CreateNewTempVar(VariableType.BOOL, "0");

            IRLabel startLabel = new($"WHILE_{labelnum}_START");
            IRLabel endLabel = new($"WHILE_{labelnum}_END");

            ParseCondition(whilestmt.cond, _tempVariables[conditionResultName]);

            _IR.Add(startLabel);
            _IR.Add(new IRTest(conditionResultName));
            _IR.Add(new IRJumpZero(endLabel.labelName));

            //TODO: NEED TO ADD JUMP TO THE START OF WHILE AT THE END OF SCOPE

            ParseScope(whilestmt.scope, _currentScope, ScopeType.LOOP);
            _IR.Add(new IRJump(startLabel.labelName));

            _IR.Add(endLabel);
        }

        private void ParseCondition(ASTCondition cond, Variable result) 
        {
            string leftNodeName = CreateNewTempVar(VariableType.INT, "0");
            Variable saveLocation = _tempVariables[leftNodeName];
            Variable var = ParseExpression(cond.leftNode);

            if (var is TempVariable tempVar)
            {
                _IR.Add(new IRAssign(saveLocation, tempVar.variableName, tempVar.variableType));
            }
            if (var is NamedVariable namedVar)
            {
                _IR.Add(new IRAssign(saveLocation, namedVar.variableName, namedVar.variableType));
            }
            if (var is LiteralVariable literalVar)
            {
                _IR.Add(new IRAssign(saveLocation, literalVar.value.ToString(), literalVar.variableType));
            }

            if (cond.rightNode != null)
            {
                string rightNodeName = CreateNewTempVar(VariableType.INT, "0");
                Variable rightNode = ParseExpression(cond.rightNode);

                if (rightNode is TempVariable tempVarr)
                {
                    _IR.Add(new IRAssign(rightNode, tempVarr.variableName, tempVarr.variableType));
                }
                if (rightNode is NamedVariable namedVarr)
                {
                    _IR.Add(new IRAssign(rightNode, namedVarr.variableName, namedVarr.variableType));
                }
                if (rightNode is LiteralVariable literalVarr)
                {
                    _IR.Add(new IRAssign(rightNode, literalVarr.value.ToString(), literalVarr.variableType));
                }

                _IR.Add(new IRCondition(result, _tempVariables[leftNodeName], cond.conditionType, _tempVariables[rightNodeName]));
            }
            else
            {
                _IR.Add(new IRCondition(result, _tempVariables[leftNodeName], ConditionType.EQUAL, new LiteralVariable("1", VariableType.INT)));
            }
        }

        private void ParseIfPred(ASTIfPred pred, IRLabel endLabel)
        {
            int labelNum = _labels.Count;

            if(pred is ASTElifPred elif)
            {
                string conditionResultName = CreateNewTempVar(VariableType.BOOL, "0");

                ParseCondition(elif.cond, _tempVariables[conditionResultName]);

                IRLabel label = new($"ELSE_{labelNum}_START");
                _IR.Add(new IRTest(conditionResultName));
                _IR.Add(new IRJumpZero(label.labelName));

                ParseScope(elif.scope, _currentScope, ScopeType.IF);

                _IR.Add(new IRJump(endLabel.labelName));

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
            else if (_expression is ASTIntLiteral intLiteral)
            {
                //_IR.Add(new IRAssign(saveLocation.variableName, intLiteral.value.ToString(), VariableType.INT));
                return new LiteralVariable(intLiteral.value.ToString(), VariableType.INT);
            }
            else if (_expression is ASTStringLiteral strLiteral)
            {
                return new LiteralVariable(strLiteral.value, VariableType.STRING);
            }
            else if (_expression is ASTCharLiteral charLiteral)
            {
                //_IR.Add(new IRAssign(saveLocation.variableName, charLiteral.value.ToString(), VariableType.CHAR));
                return new LiteralVariable(charLiteral.value.ToString(), VariableType.CHAR);
            }
            else if (_expression is ASTBoolLiteral boolLiteral)
            {
                //_IR.Add(new IRAssign(saveLocation.variableName, boolLiteral.value.ToString(), VariableType.BOOL));
                return new LiteralVariable(boolLiteral.value.ToString(), VariableType.BOOL);
            }          
            else if (_expression is ASTArithmeticOperation arithmeticOp)
            {
                /*
                  string tempVarName = CreateNewTempVar(VariableType.INT, "0");
                    TempVariable tempVar = _tempVariables[tempVarName];
                 */

                Variable leftVar = ParseExpression(arithmeticOp.leftNode);

                string tempVarName = "";
                TempVariable tempVar = null;

                if (leftVar is TempVariable _tempVar)
                {
                    tempVarName = CreateNewTempVar(VariableType.INT, "0");
                    tempVar = _tempVariables[tempVarName];
                    _IR.Add(new IRAssign(tempVar, _tempVar.variableName, VariableType.IDENTIFIER));
                }
                if (leftVar is NamedVariable namedVar)
                {
                    tempVarName = CreateNewTempVar(VariableType.INT, "0");
                    tempVar = _tempVariables[tempVarName];
                    _IR.Add(new IRAssign(tempVar, namedVar.variableName, VariableType.IDENTIFIER));
                }
                if (leftVar is LiteralVariable literalVar)
                {
                    //_IR.Add(new IRAssign(tempVar.variableName, literalVar.value.ToString(), literalVar.variableType));
                    //tempVar.SetValue(literalVar.value.ToString());
                }

                /*
                tempVarName = CreateNewTempVar(VariableType.INT, "0");
                tempVar = _tempVariables[tempVarName];
                */

                Variable rightVar = ParseExpression(arithmeticOp.rightNode);

                if (rightVar is TempVariable _tempVarr)
                {
                    tempVarName = CreateNewTempVar(VariableType.INT, "0");
                    tempVar = _tempVariables[tempVarName];
                    _IR.Add(new IRAssign(tempVar, _tempVarr.variableName, VariableType.IDENTIFIER));
                }
                if (rightVar is NamedVariable namedVarr)
                {
                    tempVarName = CreateNewTempVar(VariableType.INT, "0");
                    tempVar = _tempVariables[tempVarName];
                    _IR.Add(new IRAssign(tempVar, namedVarr.variableName, VariableType.IDENTIFIER));
                }
                if (rightVar is LiteralVariable literalVarr)
                {
                    //_IR.Add(new IRAssign(tempVar.variableName, literalVarr.value.ToString(), literalVarr.variableType));
                    //tempVar.SetValue(literalVarr.value.ToString());
                }

                /*tempVarName = CreateNewTempVar(VariableType.INT, "0");
                tempVar = _tempVariables[tempVarName];*/

                tempVarName = CreateNewTempVar(VariableType.INT, "0");
                tempVar = _tempVariables[tempVarName];

                _IR.Add(new IRArithmeticOp(tempVar, leftVar, rightVar, arithmeticOp.operation));

                //_IR.Add(new IRAssign(saveLocation.variableName, tempVarName, VariableType.INT));
                return tempVar;
            }

            return null;
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


        public class IRNewStack : IRNode
        {
            public IRNewStack()
            {
                Name = "NST";
            }

            public override string GetString()
            {
                return $"({Name})";
            }
        }

        public class IRRestoreStack : IRNode
        {
            public IRRestoreStack()
            {
                Name = "RST";
            }

            public override string GetString()
            {
                return $"({Name})";
            }
        }

        public class IRCondition : IRNode
        {
            public Variable resultVariable;

            public Variable leftNode;
            public ConditionType conditionType;
            public Variable rightNode;

            public IRCondition(Variable resultVariable, Variable leftNode, ConditionType conditionType, Variable rightNode)
            {
                Name = "COND";
                this.resultVariable = resultVariable;
                this.leftNode = leftNode;
                this.conditionType = conditionType;
                this.rightNode = rightNode;
            }

            public override string GetString()
            {
                return $"({Name}, {leftNode.variableName}, {conditionType}, {rightNode.variableName}, {resultVariable.variableName})";
            }
        }

        public class IRFunction : IRNode
        {
            public string name;
            public VariableType returnType = VariableType.VOID;
            public List<LocalVariable> parameters = new();

            public IRFunction(string name, TokenType? returnType, List<LocalVariable> parameters)
            {
                Name = "FUNC";
                this.name = name;
                if (returnType != null)
                {
                    switch (returnType)
                    {
                        case TokenType.TYPE_STRING: this.returnType = VariableType.STRING; break;
                        case TokenType.TYPE_INT: this.returnType = VariableType.INT; break;
                        case TokenType.TYPE_CHAR: this.returnType = VariableType.CHAR; break;
                        case TokenType.TYPE_BOOLEAN: this.returnType = VariableType.BOOL; break;
                        case TokenType.IDENTIFIER: this.returnType = VariableType.IDENTIFIER; break;
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
                string r = $"(CALL";
                foreach (var parameter in arguments)
                {
                    r += $", {parameter.variableName}";
                }

                r += ")";
                return r;
            }
        }

        public class IRTest : IRNode
        {
            public string variable;

            public IRTest(string variable)
            {
                Name = "TEST";

                this.variable = variable;
            }

            public override string GetString()
            {
                return $"({Name}, {variable})";
            }
        }

        public class IRJumpZero : IRNode
        {
            public string label;

            public IRJumpZero(string label)
            {
                Name = "JZ";

                this.label = label;
            }

            public override string GetString()
            {
                return $"({Name}, {label})";
            }
        }

        public class IRJump : IRNode
        {
            public string label;

            public IRJump(string label)
            {
                Name = "JMP";

                this.label = label;
            }

            public override string GetString()
            {
                return $"({Name}, {label})";
            }
        }

        public abstract class Variable : IRNode
        {
            public string variableName = "";
            public VariableType variableType;
            public string value { private set; get; }

            public void SetValue(string val)
            {
                switch (this.variableType)
                {
                    case VariableType.STRING:
                        this.value = $"\"{val}\"";
                        break;
                    case VariableType.INT:
                    case VariableType.CHAR:
                    case VariableType.BOOL:
                        this.value = val;
                        break;
                }
            }
        }

        public class LocalVariable : Variable
        {
            public LocalVariable(ASTVariableDeclaration declaration)
            {
                Name = "LOCAL";

                this.variableName = declaration.name.value;

                switch (declaration.type)
                {
                    case TokenType.TYPE_STRING: variableType = VariableType.STRING;
                        if (declaration.value == null) SetValue(@"\0");
                        break;
                    case TokenType.TYPE_INT: variableType = VariableType.INT;
                        if (declaration.value == null) SetValue("0");
                        break;
                    case TokenType.TYPE_CHAR: variableType = VariableType.CHAR;
                        if (declaration.value == null) SetValue("0");
                        break;
                    case TokenType.TYPE_BOOLEAN: variableType = VariableType.BOOL;
                        if (declaration.value == null) SetValue("0");
                        break;
                    case TokenType.IDENTIFIER: variableType = VariableType.IDENTIFIER;
                        if (declaration.value == null) SetValue("[]");
                        break;
                }
            }

            public override string GetString()
            {
                return $"({Name}, {variableName}, {variableType}, {value})";
            }
        }

        public class NamedVariable : Variable
        {
            public NamedVariable(ASTVariableDeclaration declaration)
            {
                Name = "VAR";

                this.variableName = declaration.name.value;

                switch (declaration.type)
                {
                    case TokenType.TYPE_STRING:
                        variableType = VariableType.STRING;
                        if (declaration.value == null) SetValue(@"\0");
                        break;
                    case TokenType.TYPE_INT:
                        variableType = VariableType.INT;
                        if (declaration.value == null) SetValue("0");
                        break;
                    case TokenType.TYPE_CHAR:
                        variableType = VariableType.CHAR;
                        if (declaration.value == null) SetValue("0");
                        break;
                    case TokenType.TYPE_BOOLEAN:
                        variableType = VariableType.BOOL;
                        if (declaration.value == null) SetValue("0");
                        break;
                    case TokenType.IDENTIFIER:
                        variableType = VariableType.IDENTIFIER;
                        if (declaration.value == null) SetValue("[]");
                        break;
                }
            }

            public override string GetString()
            {
                return $"({Name}, {variableName}, {variableType}, {value})";
            }
        }

        public class TempVariable : Variable
        {
            public TempVariable(string variableName, VariableType varType, string value)
            {
                Name = "TEMP";
                this.variableName = variableName;
                this.variableType = varType;
                SetValue(value);
            }

            public override string GetString()
            {
                return $"({Name}, {variableName}, {value})";
            }
        }

        public class LiteralVariable : Variable
        {
            public LiteralVariable(string value, VariableType type)
            {
                Name = "LIT";

                this.variableType = type;
                this.variableName = value.ToString();

                SetValue(value);
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
            public VariableType assignedType;
            public string value;

            public IRAssign(Variable identifier, string value, VariableType assignedType)
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
                }
            }

            public override string GetString()
            {
                return $"({Name}, {resultLocation.variableName} = {a.variableName}, {b.variableName})";
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
