﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static ils.IRGenerator.NamedVariable;

namespace ils
{
    public class IRGenerator
    {
        protected static Dictionary<string, IRLabel> _labels = new();
        protected static Dictionary<string, NamedVariable> _variables = new();

        protected static Dictionary<string, TempVariable> _tempVariables = new();

        public List<IRNode> _IR = new();

        public void Generate(ASTScope mainScope)
        {
            ParseScope(mainScope, 0);

            Console.WriteLine("\n");
            foreach (IRNode irNode in _IR)
            {
                Console.WriteLine(irNode.ToString());
            }
        }

        private string CreateNewTempVar()
        {
            string varName = $"TEMP_{_tempVariables.Keys.Count}";
            TempVariable tempVar = new TempVariable(varName);
            _tempVariables.Add(varName, tempVar);
            _IR.Add(tempVar);
            return varName;
        }

        private void ParseScope(ASTScope _scope, int scopeIndex, Dictionary<string, Variable> scopeVariables = null)
        {
            IRLabel scopeStart = new($"SCOPE_{scopeIndex}_START");
            _IR.Add(scopeStart);
            Dictionary<string, Variable> localVariables = new();

            if(scopeVariables != null) 
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
                        _IR.Add(new IRAssign(assign.identifier.value, identifier.name));
                    }
                    else if (assign.value is ASTIntLiteral intLiteral)
                    {
                        _IR.Add(new IRAssign(assign.identifier.value, intLiteral.value.ToString()));
                    }
                    else if (assign.value is ASTStringLiteral strLiteral)
                    {
                        //not implemented yet
                    }
                    else if (assign.value is ASTCharLiteral charLiteral)
                    {
                        _IR.Add(new IRAssign(assign.identifier.value, (charLiteral.value - '0').ToString()));
                    }
                    else if (assign.value is ASTBoolLiteral boolLiteral)
                    {
                        _IR.Add(new IRAssign(assign.identifier.value, boolLiteral.value ? "1" : "0"));
                    }
                    else
                    {
                        ParseExpression(assign.value, (NamedVariable)localVariables[assign.identifier.value]);
                    }
                }
            }

            IRLabel scopeEnd = new($"SCOPE_{scopeIndex}_END");
            _IR.Add(scopeEnd);

        }

        private Variable ParseExpression(ASTExpression _expression, Variable saveLocation)
        {
            List<IRNode> result = new();

            string tempVarName = CreateNewTempVar();
            TempVariable tempVar = _tempVariables[tempVarName];

            if (_expression is ASTIdentifier identifier)
            {
                _IR.Add(new IRAssign(tempVarName, identifier.name));
            }
            else if (_expression is ASTIntLiteral intLiteral)
            {
                _IR.Add(new IRAssign(tempVarName, intLiteral.value.ToString()));
            }
            else if (_expression is ASTStringLiteral strLiteral)
            {
                //not implemented yet
            }
            else if (_expression is ASTCharLiteral charLiteral)
            {
                _IR.Add(new IRAssign(tempVarName, (charLiteral.value - '0').ToString()));
            }
            else if (_expression is ASTBoolLiteral boolLiteral)
            {
                _IR.Add(new IRAssign(tempVarName, boolLiteral.value ? "1" : "0"));
            }          
            else if (_expression is ASTArithmeticOperation arithmeticOp)
            {
                switch(arithmeticOp.operation)
                {
                    case ArithmeticOpType.ADD:
                        _IR.Add(new IRAddOp(tempVar, ParseExpression(arithmeticOp.leftNode, tempVar), ParseExpression(arithmeticOp.leftNode, tempVar)));
                        break;
                    case ArithmeticOpType.SUB:
                        break;
                    case ArithmeticOpType.MUL:
                        break;
                    case ArithmeticOpType.DIV:
                        break;
                }
            }

            _IR.Add(new IRAssign(saveLocation.variableName, tempVarName));

            return tempVar;
        }

        public abstract class Variable : IRNode
        {
            public string variableName = "";
        }
        public class NamedVariable : Variable
        {
            public enum VariableType { STRING, INT, CHAR, BOOL }
            public VariableType variableType;

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
                }
            }

            public override string ToString()
            {
                return $"({Name}, {variableName}, {variableType})";
            }
        }

        public class TempVariable : Variable
        {
            public TempVariable(string variableName)
            {
                Name = "TEMP";
                this.variableName = variableName;
            }

            public override string ToString()
            {
                return $"({Name}, {variableName})";
            }
        }

        public class LiteralVariable : Variable
        {
            public string value;

            public LiteralVariable(string value)
            {
                Name = "LIT";

                this.value = value;
            }

            public override string ToString()
            {
                return $"({Name}, {value})";
            }
        }

        public abstract class IRNode 
        {
            public string Name;

            public abstract string ToString();
        }

        public class IRAssign : IRNode
        {
            string identifier;
            string value;

            public IRAssign(string identifier, string value)
            {
                Name = "ASS";  

                this.identifier = identifier;
                this.value = value;                
            }

            public override string ToString()
            {
                return $"({Name}, {identifier}, {value})";
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

            public override string ToString()
            {
                return $"({Name}, {labelName})";
            }
        }

        public abstract class IRArithmeticOp : IRNode 
        {
            public Variable resultLocation;
            public Variable a;
            public Variable b;
            public override string ToString()
            {
                return  $"({Name}, {a.variableName}, {b.variableName}, {resultLocation.variableName})";
            }
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
