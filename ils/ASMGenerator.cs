﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ils.ASMGenerator.Scope;
using static ils.Builtins;
using static ils.IRGenerator;

namespace ils
{
    public class ASMGenerator
    {
        private Dictionary<int, Word> _words = new()
        {
            { 1, new Word(){ shortName = Word.ShortName.db} },
            { 2, new Word(){ shortName = Word.ShortName.dw} },
            { 4, new Word(){ shortName = Word.ShortName.dd} },
            { 8, new Word(){ shortName = Word.ShortName.dq} }
        };

        private readonly List<Register> _regs =
        [
            new() { name = "rcx", isPreserved =  false},
            new() { name = "rdx", isPreserved =  false},
            new() { name = "r8",  isPreserved =  false},
            new() { name = "r9",  isPreserved =  false},
            new() { name = "r10", isPreserved =  false},
            new() { name = "r11", isPreserved =  false},
            new() { name = "rbx", isPreserved =  true},
            new() { name = "r12", isPreserved =  true},
            new() { name = "r13", isPreserved =  true},
            new() { name = "r14", isPreserved =  true},
            new() { name = "r15", isPreserved =  true},
        ];

        public struct Register
        {
            public string name;
            public bool isPreserved;
        }

        public Queue<Register> _availableRegs = new();
        public Map<string, Register> _usedRegs = new();

        public List<string> asm = new();

        public Dictionary<string, ReservedVariable> _dataSection = new();

        public Scope _currentScope;

        private IRFunction _currentFunc;

        public class Scope
        {
            public int id;

            public List<Register> vars = new();
            public Dictionary<string, StackVar> stackvars = new();

            public int stackSize = 0;

            public Scope(int id)
            {
                this.id = id;
            }

            public void GenerateStackVar(NamedVariable arg)
            {
                if(stackvars.ContainsKey(arg.guid.ToString()))
                {
                    return;
                }

                stackvars.Add(arg.guid.ToString(), new StackVar() { offset = stackSize, var = arg });
                stackSize++;
            }

            public struct StackVar
            {
                public int offset;
                public Variable var;
            }
        }

        /*private void Push(string v)
        {
            AddAsm($"push {v}");
            _stackSize++;
        }

        private void Pop(string v)
        {
            AddAsm($"pop {v}");
            _stackSize--;
        }*/


        private string lastMoveDestination = "";

        public void Mov(string destination, string source)
        {
            if (asm.Last().Contains("mov"))
            {
                if (!string.IsNullOrEmpty(lastMoveDestination) && lastMoveDestination == destination)
                {
                    ReplaceLastAsm($"mov {destination}, {source}");
                    lastMoveDestination = destination;
                    return;
                }
            }

            AddAsm($"mov {destination}, {source}");
            lastMoveDestination = destination;
        }

        private void FunctionPrologue(IRFunctionPrologue prologue)
        {
            AddAsm("push rbp");
            Mov("rbp", "rsp");
            if(prologue.localVariables != 0)
            {
                AddAsm($"sub rsp, {prologue.localVariables * 8}");
            }   
        }

        private void FunctionEpilogue()
        {
            Mov("rsp", "rbp");
            AddAsm("pop rbp");
        }

        public string GenerateASM(List<IRNode> ir)
        {
            foreach (var reg in _regs)
            {
                _availableRegs.Enqueue(reg);
            }

            AddAsm("global main", 0);

            foreach (IRNode node in ir)
            {
                GenerateIRNode(node);
            }

            AddAsm("section .data", 0);
            AddAsm("strFormat db \"%s\", 10, 0");
            AddAsm("intFormat db \"%d\", 10, 0");
            AddAsm("charFormat db \"%c\", 10, 0"); 

            foreach (var data in _dataSection)
            {
                AddAsm($"{data.Value.name}:", 0);
                AddAsm($"{data.Value.word.shortName} {data.Value.var.value}");
            }

            AddAsm("section .text", 0);
            AddAsm("extern printf");

            Console.WriteLine('\n' + string.Join("", asm));

            return string.Join("", asm);
        }

        public void AddAsm(string code, int tabsCount = 1)
        {
            string tabs = new string('\t', tabsCount);
            asm.Add(tabs + code + '\n');
            //Console.WriteLine(tabs + code + '\n');
        }

        public void ReplaceLastAsm(string code, int tabsCount = 1)
        {
            string tabs = new string('\t', tabsCount);
            asm[asm.Count - 1] = tabs + code + '\n';
        }

        private bool VarExists(string name)
        {
            return _dataSection.ContainsKey(name);
        }

        private void GenerateFunction(IRFunction func)
        {
            AddAsm($"{func.name}:", 0);

            _currentFunc = func;
        }

        private void GenerateFunctionCall(IRFunctionCall func)
        {
            BuiltinFunction bfunc = BuiltinFunctions.FirstOrDefault(x => x.name == func.name);
            if (bfunc != null)
            {
                if(bfunc is LibcFunction libc)
                {
                    libc.Generate(func.arguments);
                }
            }
            else
            {
                List<Variable> args = func.arguments;
                args.Reverse();
                foreach (var argument in args)
                {
                    AddAsm($"push {GetLocation(argument, false, false)}");
                }
                AddAsm($"call {func.name}");
            }
        }

        private void GenerateVariable(Variable var)
        {
            if (var is NamedVariable namedVar)
            {
                if (namedVar.isGlobal)
                {
                    if (VarExists(namedVar.variableName))
                    {
                        ErrorHandler.Custom($"Variable {namedVar.variableName} already exists!");
                        return;
                    }

                    switch (namedVar.variableType)
                    {
                        case DataType.CHAR:
                            _dataSection.Add(namedVar.variableName, new ReservedVariable()
                            {
                                name = namedVar.variableName,
                                word = _words[1],
                                var = namedVar
                            });
                            break;
                        case DataType.INT:
                            _dataSection.Add(namedVar.variableName, new ReservedVariable()
                            {
                                name = namedVar.variableName,
                                word = _words[4],
                                var = namedVar
                            });
                            break;
                        case DataType.BOOL:
                            _dataSection.Add(namedVar.variableName, new ReservedVariable()
                            {
                                name = namedVar.variableName,
                                word = _words[1],
                                var = namedVar
                            });
                            break;
                        case DataType.STRING:
                            //todo
                            break;
                    }
                }
                else
                {
                    _currentScope.GenerateStackVar(namedVar);
                    if (!namedVar.isFuncArg)
                    {
                        string val = namedVar.value;

                        if(namedVar.variableType == DataType.IDENTIFIER)
                        {
                            val = GetLocation(IRGenerator._allVariables.Values.Where(x => x.guid.ToString() == namedVar.value).First(), false, false);
                        }
                        Mov(GetLocation(namedVar, true, false), val);
                    }
                }
            }
            if(var is TempVariable tempVar) 
            {
                GenerateTempVariable(tempVar);
            }
        }  

        public void GenerateTempVariable(Variable var)
        {
            Register reg;
            if(var.needsPreservedReg)
            {
                reg = _availableRegs.DequeueItemWithCondition(i => i.isPreserved);
            }
            else
            {
                _availableRegs.TryDequeue(out reg);
            }
            _usedRegs.Add(var.variableName, reg);


            switch (var.variableType)
            {
                case DataType.STRING:
                    Console.WriteLine("TODO 179");
                    break;
                case DataType.INT:
                    Mov(reg.name, var.value);
                    break;
                case DataType.CHAR:
                    Mov(reg.name, var.value);
                    break;
                case DataType.BOOL:
                    Mov(reg.name, var.value);
                    break;
                case DataType.IDENTIFIER:
                    string val = GetLocation(IRGenerator._allVariables[var.value], false, false);                   
                    Mov(reg.name, val);
                    break;
            }
        }

        private void GenerateAssign(IRAssign asign)
        {
            string location = GetLocation(asign.identifier, true, false);
            string val = asign.value;

            switch (asign.assignedType)
            {
                case DataType.STRING:
                    //todo
                    break;
                case DataType.INT:
                    val = asign.value;
                    break;
                case DataType.CHAR:
                    //AddAsm($"mov {_words[1].longName} [{asign.identifier}], {asign.value}");
                    break;
                case DataType.BOOL:
                    // AddAsm($"mov {_words[1].longName} [{asign.identifier}], {asign.value}");
                    break;
                case DataType.IDENTIFIER:
                    val = GetLocation(IRGenerator._allVariables.Values.Where(x => x.variableName == asign.value).First(), false, false);
                    if (val.Contains("rbp"))
                    {
                        Register reg = _availableRegs.Dequeue();
                        _usedRegs.Add(Guid.NewGuid().ToString(), reg);
                        Mov(reg.name, val);
                        val = reg.name;
                        FreeReg(reg);
                    }
                    /*if (_dataSection.TryGetValue(asign.value, out ReservedVariable var))
                    { 
                        val = $"[{var.var.variableName}]"; 
                    }
                    else val = _usedRegs[asign.value].name;*/
                    break;
            }

            Mov(location, val);
        }

        private void GenerateArithmeticOP(IRArithmeticOp arop)
        {
            string location = GetLocation(arop.resultLocation, true, false);

            string a = GetLocation(arop.a, false, false);
            string b = GetLocation(arop.b, false, false);

            switch (arop.opType)
            {
                case ArithmeticOpType.ADD:
                    Mov(location, a);
                    AddAsm($"add {location}, {b}");
                    break;
                case ArithmeticOpType.MUL:
                    Mov(location, a);
                    AddAsm($"imul {location}, {b}");
                    break;
                case ArithmeticOpType.SUB:
                    Mov(location, a);
                    AddAsm($"sub {location}, {b}");
                    break;
                case ArithmeticOpType.DIV:
                    Mov("rax", a);
                    Mov("rbx", b);
                    AddAsm($"cqo");
                    AddAsm($"div rbx");
                    Mov(location, "rax");
                    break;
                case ArithmeticOpType.MOD:
                    Mov("rax", a);
                    Mov("rbx", b);
                    AddAsm($"cqo");
                    AddAsm($"div rbx");
                    Mov(location, "rdx"); //TODO to sie wyjebie na 100% bo rdx jest jako wolny rejestr uzywane
                    break;
            }
        }

        public void FreeReg(Register reg)
        {
            _availableRegs.Enqueue(reg);
            _usedRegs.Remove(reg);
        }

        private void GenerateDestroyTemp(IRDestroyTemp dtp) 
        {
            if (!_usedRegs.Forward.Contains(dtp.temp)) return;
            Register reg = _usedRegs.Forward[dtp.temp];

            FreeReg(reg);
        }

        private void GenerateCompare(IRCompare compare)
        {
            string sizeA = "";
            string sizeB = "";

            Register aReg;
            Register bReg;

            string compareA = GetLocation(compare.a, false, true);
            string compareB = GetLocation(compare.b, false, false);

            if (compare.a is NamedVariable namedA)
            {
                if(namedA.isGlobal)
                {
                    switch (compare.a.variableType)
                    {
                        case DataType.STRING:
                            sizeA = "byte ";
                            break;
                        case DataType.INT:
                            sizeA = "qword ";
                            break;
                        case DataType.CHAR:
                            sizeA = "byte ";
                            break;
                        case DataType.BOOL:
                            sizeA = "byte ";
                            break;
                        case DataType.IDENTIFIER:
                            sizeA = "qword ";
                            break;
                    }
                }
                else
                {
                    aReg = _availableRegs.Dequeue();
                    Mov(aReg.name, compareA);
                    compareA = aReg.name;
                    _usedRegs.Add(Guid.NewGuid().ToString(), aReg);
                    FreeReg(aReg);
                }
                            
            }
           
            if (compare.b is NamedVariable namedB)
            {
                if (namedB.isGlobal)
                {
                    switch (compare.b.variableType)
                    {
                        case DataType.STRING:
                            sizeB = "byte ";
                            break;
                        case DataType.INT:
                            sizeB = "qword ";
                            break;
                        case DataType.CHAR:
                            sizeB = "byte ";
                            break;
                        case DataType.BOOL:
                            sizeB = "byte ";
                            break;
                        case DataType.IDENTIFIER:
                            sizeB = "qword ";
                            break;
                    }
                }
                else
                {
                    bReg = _availableRegs.Dequeue();
                    Mov(bReg.name, compareB);
                    compareB = bReg.name;
                    _usedRegs.Add(Guid.NewGuid().ToString(), bReg);
                    FreeReg(bReg);
                }
            }
            AddAsm($"cmp {sizeA}{compareA}, {sizeB}{compareB}");
            
            
            //AddAsm($"cmp {sizeA}{GetLocation(compare.a, false, true)}, {sizeB}{GetLocation(compare.b, false, false)}");
        }

        private void GenerateJump(IRJump jump)
        {
            switch (jump.conditionType)
            {
                case ASTCondition.ConditionType.EQUAL:
                    AddAsm($"je .{jump.label}");
                    break;
                case ASTCondition.ConditionType.NOT_EQUAL:
                    AddAsm($"jne .{jump.label}");
                    break;
                case ASTCondition.ConditionType.LESS:
                    AddAsm($"jl .{jump.label}");
                    break;
                case ASTCondition.ConditionType.LESS_EQUAL:
                    AddAsm($"jle .{jump.label}");
                    break;
                case ASTCondition.ConditionType.GREATER:
                    AddAsm($"jg .{jump.label}");
                    break;
                case ASTCondition.ConditionType.GREATER_EQUAL:
                    AddAsm($"jge .{jump.label}");
                    break;
                case ASTCondition.ConditionType.NONE:
                    AddAsm($"jmp .{jump.label}");
                    break;
            }
        }

        private void GenerateReturn(IRReturn ret)
        {
            Mov("rax", GetLocation(ret.ret, false, false));
        }

        private void GenerateScopeStart(IRScopeStart start)
        {
            _currentScope = new Scope(start.scope.id);
            foreach (var par in _currentFunc.parameters)
            {
                GenerateVariable(par);
            }
        }

        private void GenerateScopeEnd(IRScopeEnd end)
        {
            /*if(end.valuesToClear != 0)
            {
                AddAsm($"ret {end.valuesToClear * 8}");
            }
            else
            {
                AddAsm($"ret");
            }*/

            AddAsm($"ret");
        }

        private void GenerateIRNode(IRNode node)
        {
            if (node is IRLabel label) { AddAsm($".{label.labelName}:"); }
            if (node is Variable variable) { GenerateVariable(variable); }
            if (node is IRAssign asign) { GenerateAssign(asign); }
            if (node is IRFunction func) { GenerateFunction(func); }
            if (node is IRFunctionPrologue prologue) { FunctionPrologue(prologue); }
            if (node is IRFunctionEpilogue epilogue) { FunctionEpilogue(); }
            if (node is IRArithmeticOp arOp) { GenerateArithmeticOP(arOp); }
            if (node is IRFunctionCall call) { GenerateFunctionCall(call); }
            if (node is IRDestroyTemp dtp) { GenerateDestroyTemp(dtp); }
            //if (node is IRCondition cond) { GenerateCondition(cond); }
            if(node is IRCompare comp) { GenerateCompare(comp); }
            if(node is IRJump jump) { GenerateJump(jump); }
            if(node is IRReturn ret) { GenerateReturn(ret); }
            if (node is IRScopeStart start) { GenerateScopeStart(start); }
            if (node is IRScopeEnd end) { GenerateScopeEnd(end); }
        }

        public string GetLocation(Variable var, bool isMovedTo, bool generateLiteral)
        {
            if(var is TempVariable temp)
            {
                Register val;
                if (_usedRegs.Forward.TryGet(temp.variableName, out val))
                {
                    return val.name;
                }
                else
                {
                    GenerateTempVariable(temp);
                    return _usedRegs.Forward[temp.variableName].name;
                }
            }
            else if(var is NamedVariable namedVar)
            {
                if(namedVar.isGlobal)
                {
                    return $"[{_dataSection[namedVar.variableName].name}]";
                }
                else
                {
                    if (!_currentScope.stackvars.TryGetValue(namedVar.guid.ToString(), out StackVar val))
                    {
                        _currentScope.GenerateStackVar(namedVar);  
                    }

                    if (namedVar.isFuncArg)
                    {
                        if (isMovedTo)
                        {
                            return $"qword [rbp+{16 + _currentScope.stackvars[namedVar.guid.ToString()].offset * 8}]";
                        }
                        else
                        {
                            return $"[rbp+{16 + _currentScope.stackvars[namedVar.guid.ToString()].offset * 8}]";
                        }
                    }
                    else
                    {
                        int offset = _currentScope.stackvars[namedVar.guid.ToString()].offset * 8;
                        if (offset == 0) offset = 8;
                        if (isMovedTo)
                        {
                            return $"qword [rbp-{offset}]";
                        }
                        else
                        {
                            return $"[rbp-{offset}]";
                        }
                    }
                }
            }         
            else if(var is LiteralVariable lit && generateLiteral)
            {
                if (_usedRegs.Forward.TryGet(lit.variableName, out Register val))
                {
                    return val.name;
                }
                else
                {
                    GenerateTempVariable(lit);
                    return _usedRegs.Forward[lit.variableName].name;
                }
            }
            else if(var is FunctionReturnVariable regvar)
            {
                GenerateFunctionCall(regvar.call);
                return "rax"; // regvar.funcName + regvar.index.ToString();
            }

            return var.value;
        }

        public struct Word
        {
            public enum ShortName { db, dw, dd, dq}
            public ShortName shortName;
        }

        public struct ReservedVariable
        {
            public string name;
            public Word word;
            public Variable var;
            public string initialValue;
        }
    }
}
