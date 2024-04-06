using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static ils.ASTCondition;
using static ils.IRGenerator;
using static ils.IRGenerator.NamedVariable;

namespace ils
{
    public class IRGenerator
    {
        public enum VariableType { STRING, INT, CHAR, BOOL, IDENTIFIER }
        protected static Dictionary<string, IRLabel> _labels = new();
        protected static Dictionary<string, NamedVariable> _variables = new();

        protected static Dictionary<string, TempVariable> _tempVariables = new();

        public List<IRNode> _IR = new();

        private Dictionary<ASTScope, ScopeLabels> _scopeLabels = new();

        public List<IRNode> Generate(ASTScope mainScope)
        {
            ParseScope(mainScope, 0, null);

            Console.WriteLine("\n");
            foreach (IRNode irNode in _IR)
            {
                Console.WriteLine(irNode.ToString);
            }

            return _IR;
        }

        private string CreateNewTempVar(VariableType varType)
        {
            string varName = $"TEMP_{_tempVariables.Keys.Count}";
            TempVariable tempVar = new TempVariable(varName);
            tempVar.variableType = varType;
            _tempVariables.Add(varName, tempVar);
            _IR.Add(tempVar);
            return varName;
        }

        private void ParseScope(ASTScope _scope, int scopeIndex, Dictionary<string, Variable> scopeVariables)
        {
            IRLabel scopeStart = new($"SCOPE_{scopeIndex}_START");
            _IR.Add(scopeStart);
            IRLabel scopeEnd = new($"SCOPE_{scopeIndex}_END");
            Dictionary<string, Variable> localVariables = new();

            _scopeLabels.Add(_scope, new ScopeLabels() { startLabel = scopeStart, endLabel = scopeEnd });

            if (scopeVariables != null) 
            {
                foreach (var variable in scopeVariables)
                {
                    localVariables.Add(variable.Key, variable.Value);
                }
            }

            foreach (ASTStatement statement in _scope.statements)
            {
                if(statement is ASTVariableDeclaration varDeclaration)
                {
                    if (localVariables.ContainsKey(varDeclaration.name.value))
                    {
                        ErrorHandler.Custom($"[{varDeclaration.name.line}] Variable '{varDeclaration.name.value}' already exists!'");
                    }
                    else
                    {
                        NamedVariable newVar = new NamedVariable(varDeclaration);
                        localVariables.Add(varDeclaration.name.value, newVar);
                        _IR.Add(newVar);

                        if(varDeclaration.value != null)
                        {
                            ParseExpression(varDeclaration.value, newVar);
                        }
                    }
                }
                else if(statement is ASTScope scope)
                {
                    ParseScope(scope, scopeIndex + 1, localVariables);
                }
                else if(statement is ASTAssign assign)
                {
                    if (assign.value is ASTIdentifier identifier)
                    {
                        _IR.Add(new IRAssign(assign.identifier.value, identifier.name, VariableType.IDENTIFIER));
                    }
                    else if (assign.value is ASTIntLiteral intLiteral)
                    {
                        _IR.Add(new IRAssign(assign.identifier.value, intLiteral.value.ToString(), VariableType.INT));
                    }
                    else if (assign.value is ASTStringLiteral strLiteral)
                    {
                        //not implemented yet
                    }
                    else if (assign.value is ASTCharLiteral charLiteral)
                    {
                        _IR.Add(new IRAssign(assign.identifier.value, charLiteral.value.ToString(), VariableType.CHAR));
                    }
                    else if (assign.value is ASTBoolLiteral boolLiteral)
                    {
                        _IR.Add(new IRAssign(assign.identifier.value, boolLiteral.value.ToString(), VariableType.BOOL));
                    }
                    else
                    {
                        ParseExpression(assign.value, (NamedVariable)localVariables[assign.identifier.value]);
                    }
                }
                else if(statement is ASTIf ifstmt)
                {
                    ParseIf(ifstmt, localVariables);
                }
                else if(statement is ASTWhile whilestmt)
                {
                    ParseWhile(whilestmt, scopeVariables);
                }
                else if(statement is ASTBreak breakstmt)
                {
                    if(_scope.scopeType == ScopeType.LOOP)
                    {
                        _IR.Add(new IRJump(scopeEnd.labelName));
                    }
                    else
                    {
                        ASTScope parentScope = GetParentScopeOfType(ScopeType.LOOP, _scope);

                        if(parentScope != null)
                        {
                            _IR.Add(new IRJump(_scopeLabels[parentScope].endLabel.labelName));
                        }
                        else
                        {
                            _IR.Add(new IRJump(scopeEnd.labelName));
                        }
                    }
                }
            }

            _IR.Add(scopeEnd);
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

            string conditionResultName = CreateNewTempVar(VariableType.BOOL);

            IRLabel label = new($"IF_{labelnum}");

            ParseCondition(ifstmt.cond, _tempVariables[conditionResultName]);

            _IR.Add(new IRTest(conditionResultName));
            _IR.Add(new IRJumpZero(label.labelName));

            ParseScope(ifstmt.scope, labelnum, scopeVariables);

            if(ifstmt.pred != null)
            {
                IRLabel endLabel = new($"IF_{labelnum}_END");
                _IR.Add(new IRJump(endLabel.labelName));
                _IR.Add(label);
                ParseIfPred(ifstmt.pred, endLabel, scopeVariables);
                _IR.Add(endLabel);
            }
            else
            {
                _IR.Add(label);
            }
        }

        private void ParseWhile(ASTWhile whilestmt, Dictionary<string, Variable> scopeVariables)
        {
            int labelnum = _labels.Count;

            string conditionResultName = CreateNewTempVar(VariableType.BOOL);

            IRLabel startLabel = new($"WHILE_{labelnum}_START");
            IRLabel endLabel = new($"WHILE_{labelnum}_END");

            ParseCondition(whilestmt.cond, _tempVariables[conditionResultName]);

            _IR.Add(startLabel);
            _IR.Add(new IRTest(conditionResultName));
            _IR.Add(new IRJumpZero(endLabel.labelName));

            //TODO: NEED TO ADD JUMP TO THE START OF WHILE AT THE END OF SCOPE

            ParseScope(whilestmt.scope, labelnum, scopeVariables);
            _IR.Add(new IRJump(startLabel.labelName));

            _IR.Add(endLabel);
        }

        private void ParseCondition(ASTCondition cond, Variable result) 
        {
            string leftNodeName = CreateNewTempVar(VariableType.INT);
            ParseExpression(cond.leftNode, _tempVariables[leftNodeName]);

            if (cond.rightNode != null)
            {
                string rightNodeName = CreateNewTempVar(VariableType.INT);
                ParseExpression(cond.rightNode, _tempVariables[rightNodeName]);

                _IR.Add(new IRCondition(result, _tempVariables[leftNodeName], cond.conditionType, _tempVariables[rightNodeName]));
            }
            else
            {
                _IR.Add(new IRCondition(result, _tempVariables[leftNodeName], ConditionType.EQUAL, new LiteralVariable("1")));
            }
        }

        private void ParseIfPred(ASTIfPred pred, IRLabel endLabel,Dictionary<string, Variable> scopeVariables)
        {
            int labelNum = _labels.Count;

            if(pred is ASTElifPred elif)
            {
                string conditionResultName = CreateNewTempVar(VariableType.BOOL);

                ParseCondition(elif.cond, _tempVariables[conditionResultName]);

                IRLabel label = new($"ELSE_{labelNum}_START");
                _IR.Add(new IRTest(conditionResultName));
                _IR.Add(new IRJumpZero(label.labelName));

                ParseScope(elif.scope, labelNum, scopeVariables);

                _IR.Add(new IRJump(endLabel.labelName));

                if (elif.pred != null)
                {
                    _IR.Add(label);
                    ParseIfPred(elif.pred, endLabel, scopeVariables);
                }
            }
            else if(pred is ASTElsePred elsepred)
            {
                ParseScope(elsepred.scope, labelNum, scopeVariables);
            }  
        }

        private Variable ParseExpression(ASTExpression _expression, Variable saveLocation)
        {
            if (_expression is ASTIdentifier identifier)
            {
                string tempVarName = CreateNewTempVar(VariableType.IDENTIFIER);
                TempVariable tempVar = _tempVariables[tempVarName];
                _IR.Add(new IRAssign(tempVarName, identifier.name, VariableType.IDENTIFIER));
                return tempVar;
            }
            else if (_expression is ASTIntLiteral intLiteral)
            {
                _IR.Add(new IRAssign(saveLocation.variableName, intLiteral.value.ToString(), VariableType.INT));
                return new LiteralVariable(intLiteral.value.ToString());
            }
            else if (_expression is ASTStringLiteral strLiteral)
            {
                Console.WriteLine("str not implemented");
            }
            else if (_expression is ASTCharLiteral charLiteral)
            {
                _IR.Add(new IRAssign(saveLocation.variableName, charLiteral.value.ToString(), VariableType.CHAR));
                return new LiteralVariable(charLiteral.value.ToString());
            }
            else if (_expression is ASTBoolLiteral boolLiteral)
            {
                _IR.Add(new IRAssign(saveLocation.variableName, boolLiteral.value.ToString(), VariableType.BOOL));
                return new LiteralVariable(boolLiteral.value.ToString());
            }          
            else if (_expression is ASTArithmeticOperation arithmeticOp)
            {
                string tempVarName = CreateNewTempVar(VariableType.INT);
                TempVariable tempVar = _tempVariables[tempVarName];

                Variable leftVar = ParseExpression(arithmeticOp.leftNode, tempVar);

                tempVarName = CreateNewTempVar(VariableType.INT);
                tempVar = _tempVariables[tempVarName];

                Variable righVar = ParseExpression(arithmeticOp.rightNode, tempVar);

                switch (arithmeticOp.operation)
                {
                    case ArithmeticOpType.ADD:
                        _IR.Add(new IRAddOp(tempVar, leftVar, righVar));
                        break;
                    case ArithmeticOpType.SUB:
                        break;
                    case ArithmeticOpType.MUL:
                        break;
                    case ArithmeticOpType.DIV:
                        break;
                }

                _IR.Add(new IRAssign(saveLocation.variableName, tempVarName, VariableType.INT));
                return tempVar;
            }

            return null;
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

            public override string ToString => $"({Name}, {leftNode.variableName}, {conditionType}, {rightNode.variableName}, {resultVariable.variableName})";
        }

        public class IRTest : IRNode
        {
            public string variable;

            public IRTest(string variable)
            {
                Name = "TEST";

                this.variable = variable;
            }

            public override string ToString => $"({Name}, {variable})";
        }

        public class IRJumpZero : IRNode
        {
            public string label;

            public IRJumpZero(string label)
            {
                Name = "JZ";

                this.label = label;
            }

            public override string ToString => $"({Name}, {label})";
        }

        public class IRJump : IRNode
        {
            public string label;

            public IRJump(string label)
            {
                Name = "JMP";

                this.label = label;
            }

            public override string ToString => $"({Name}, {label})";
        }

        public abstract class Variable : IRNode
        {
            public string variableName = "";
            public VariableType variableType;
        }
        public class NamedVariable : Variable
        {
            public NamedVariable(ASTVariableDeclaration declaration)
            {
                Name = "VAR";

                this.variableName = declaration.name.value;

                switch (declaration.type)
                {
                    case TokenType.TYPE_STRING: variableType = VariableType.STRING; break;
                    case TokenType.TYPE_INT: variableType = VariableType.INT; break;
                    case TokenType.TYPE_CHAR: variableType = VariableType.CHAR; break;
                    case TokenType.TYPE_BOOLEAN: variableType = VariableType.BOOL; break;
                    case TokenType.IDENTIFIER: variableType = VariableType.IDENTIFIER; break;
                }
            }

            public override string ToString => $"({Name}, {variableName}, {variableType})";
        }

        public class TempVariable : Variable
        {
            public TempVariable(string variableName)
            {
                Name = "TEMP";
                this.variableName = variableName;
            }

            public override string ToString => $"({Name}, {variableName})";
        }

        public class LiteralVariable : Variable
        {
            public string value;

            public LiteralVariable(string value)
            {
                Name = "LIT";

                this.variableName = value.ToString();
                this.value = value;
            }

            public override string ToString => $"({Name}, {value})";
        }

        public abstract class IRNode 
        {
            protected string Name;

            public abstract string ToString { get; }
        }

        public class IRAssign : IRNode
        {
            public string identifier;
            public VariableType assignedType;
            public string value;

            public IRAssign(string identifier, string value, VariableType assignedType)
            {
                Name = "ASN";

                this.identifier = identifier;
                this.value = value;
                this.assignedType = assignedType;
            }

            public override string ToString => $"({Name}, {identifier}, {value})";
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

            public override string ToString => $"({Name}, {labelName})";
        }

        public abstract class IRArithmeticOp : IRNode 
        {
            public Variable resultLocation;
            public Variable a;
            public Variable b;
            public override string ToString => $"({Name}, {a.variableName}, {b.variableName}, {resultLocation.variableName})";
        }

        public class IRAddOp : IRArithmeticOp
        {
            public IRAddOp(Variable result, Variable a, Variable b)
            {
                Name = "ADD";

                this.resultLocation = result;
                this.a = a;
                this.b = b;
            }
        }

        public class IRSubOp : IRArithmeticOp
        {
            public IRSubOp(Variable result, Variable a, Variable b)
            {
                Name = "SUB";

                this.resultLocation = result;
                this.a = a;
                this.b = b;
            }        
        }

        public class IRMulOp : IRArithmeticOp
        {
            public IRMulOp(Variable result, Variable a, Variable b)
            {
                Name = "MUL";

                this.resultLocation = result;
                this.a = a;
                this.b = b;
            }
        }

        public class IRDivOp : IRArithmeticOp
        {
            public IRDivOp(Variable result, Variable a, Variable b)
            {
                Name = "DIV";

                this.resultLocation = result;
                this.a = a;
                this.b = b;
            }
        }
    }
}
