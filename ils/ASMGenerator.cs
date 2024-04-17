using System;
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
        public Dictionary<string, Register> _usedRegs = new();

        public string asm = "";

        public Dictionary<string, ReservedVariable> _dataSection = new();

        public Scope _currentScope;

        public class Scope
        {
            public int id;

            public List<Register> vars = new();
            public Dictionary<string, StackVar> stackvars = new();

            public int stackSize = 1;

            public Scope(int id)
            {
                this.id = id;
            }

            public void GenerateStackVar(NamedVariable arg)
            {
                stackvars.Add(arg.variableName, new StackVar() { offset = stackSize, var = arg });
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


        public void Mov(string destination, string source)
        {
            
            AddAsm($"mov {destination}, {source}");
        }

        private void FunctionPrologue()
        {
            AddAsm("push rbp");
            Mov("rbp", "rsp");
        }

        private void FunctionEpilogue()
        {
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

            Console.WriteLine('\n' + asm);

            return asm;
        }

        public void AddAsm(string code, int tabsCount = 1)
        {
            string tabs = new string('\t', tabsCount);
            asm += tabs + code + '\n';
        }

        private bool VarExists(string name)
        {
            return _dataSection.ContainsKey(name);
        }

        private void GenerateFunction(IRFunction func)
        {
            AddAsm($"{func.name}:", 0);

            foreach(var par in func.parameters)
            {
                GenerateVariable(par);
            }
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
                    AddAsm($"push {GetLocation(argument)}");
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
                    if (namedVar.isFuncArg)
                    {
                        _currentScope.GenerateStackVar(namedVar);

                    }
                    else
                    {
                        GenerateTempVariable(namedVar);
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
            _usedRegs[var.variableName] = reg;


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
                    string val = GetLocation(IRGenerator._allVariables[var.value]);                   
                    Mov(reg.name, val);
                    break;
            }
        }

        private void GenerateAssign(IRAssign asign)
        {
            string location = GetLocation(asign.identifier);
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
                    //AddAsm($"mov {_words[1].longName} [{asign.identifier}], {asign.value}");
                    break;
                case DataType.IDENTIFIER:
                    if (_dataSection.TryGetValue(asign.value, out ReservedVariable var))
                    { 
                        val = $"[{var.var.variableName}]"; 
                    }
                    else val = _usedRegs[asign.value].name;
                    break;
            }

            Mov(location, val);
        }

        private void GenerateArithmeticOP(IRArithmeticOp arop)
        {
            string location = _usedRegs[arop.resultLocation.variableName].name;

            string a = GetLocation(arop.a);
            string b = GetLocation(arop.b);

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

        private void GenerateDestroyTemp(IRDestroyTemp dtp) 
        {
            Register reg = _usedRegs[dtp.temp];

            _availableRegs.Enqueue(reg);
            _usedRegs.Remove(dtp.temp);
        }

        private void GenerateCompare(IRCompare compare)
        {
            string sizeA = "";

            if (compare.a is NamedVariable)
            {
                switch (compare.a.variableType)
                {
                    case DataType.STRING:
                        sizeA = "byte ";
                        break;
                    case DataType.INT:
                        sizeA = "dword ";
                        break;
                    case DataType.CHAR:
                        sizeA = "byte ";
                        break;
                    case DataType.BOOL:
                        sizeA = "byte ";
                        break;
                    case DataType.IDENTIFIER:
                        sizeA = "dword ";
                        break;
                }
            }
            

            string sizeB = "";

            if (compare.b is NamedVariable)
            {
                switch (compare.b.variableType)
                {
                    case DataType.STRING:
                        sizeB = "byte ";
                        break;
                    case DataType.INT:
                        sizeB = "dword ";
                        break;
                    case DataType.CHAR:
                        sizeB = "byte ";
                        break;
                    case DataType.BOOL:
                        sizeB = "byte ";
                        break;
                    case DataType.IDENTIFIER:
                        sizeB = "dword ";
                        break;
                }
            }

            AddAsm($"cmp {sizeA}{GetLocation(compare.a, true)}, {sizeB}{GetLocation(compare.b)}");
        }

        private void GenerateJump(IRJump jump)
        {
            switch (jump.conditionType)
            {
                case ASTCondition.ConditionType.EQUAL:
                    AddAsm($"jne .{jump.label}");
                    break;
                case ASTCondition.ConditionType.NOT_EQUAL:
                    AddAsm($"je .{jump.label}");
                    break;
                case ASTCondition.ConditionType.LESS:
                    AddAsm($"jge .{jump.label}");
                    break;
                case ASTCondition.ConditionType.LESS_EQUAL:
                    AddAsm($"jg .{jump.label}");
                    break;
                case ASTCondition.ConditionType.GREATER:
                    AddAsm($"jle .{jump.label}");
                    break;
                case ASTCondition.ConditionType.GREATER_EQUAL:
                    AddAsm($"jl .{jump.label}");
                    break;
                case ASTCondition.ConditionType.NONE:
                    AddAsm($"jmp .{jump.label}");
                    break;
            }
        }

        private void GenerateReturn(IRReturn ret)
        {
            Mov("rax", GetLocation(ret.ret));
            AddAsm($"ret {ret.valuesOnStackToClear * 8}");
        }

        private void GenerateScopeStart(IRScopeStart start)
        {
            _currentScope = new Scope(start.scope.id);
        }

        private void GenerateScopeEnd(IRScopeEnd end) { }

        private void GenerateIRNode(IRNode node)
        {
            if (node is IRLabel label) { AddAsm($".{label.labelName}:"); }
            if (node is Variable variable) { GenerateVariable(variable); }
            if (node is IRAssign asign) { GenerateAssign(asign); }
            if (node is IRFunction func) { GenerateFunction(func); }
            if (node is IRFunctionPrologue newStack) { FunctionPrologue(); }
            if (node is IRFunctionEpilogue restoreStack) { FunctionEpilogue(); }
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

        public string GetLocation(Variable var, bool generateLiteral = false)
        {
            if(var is TempVariable temp)
            {
                if(_usedRegs.TryGetValue(temp.variableName, out Register val))
                {
                    return val.name;
                }
                else
                {
                    GenerateTempVariable(temp);
                    return _usedRegs[temp.variableName].name;
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
                    if(namedVar.isFuncArg)
                    {
                        if (_currentScope.stackvars.TryGetValue(namedVar.variableName, out StackVar stackVar))
                        {
                            return $"[rsp+{8 + _currentScope.stackvars[namedVar.variableName].offset * 8}]";
                        }
                        else
                        {
                            _currentScope.GenerateStackVar(namedVar);
                            return $"[rsp+{8 + _currentScope.stackvars[namedVar.variableName].offset * 8}]";
                        }
                    }
                    else if (_usedRegs.TryGetValue(namedVar.variableName, out Register val))
                    {
                        return val.name;
                    }
                    else
                    {
                        GenerateTempVariable(namedVar);
                        return _usedRegs[namedVar.variableName].name;
                    }
                }
            }         
            else if(var is LiteralVariable lit && generateLiteral)
            {
                if (_usedRegs.TryGetValue(lit.variableName, out Register val))
                {
                    return val.name;
                }
                else
                {
                    GenerateTempVariable(lit);
                    return _usedRegs[lit.variableName].name;
                }
            }
            else if(var is RegVariable regvar)
            {
                return regvar.reg;
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
