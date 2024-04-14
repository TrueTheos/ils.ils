using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        private int _stackSize;

        private readonly List<string> _scratchRegs = ["rdx", "rcx", "r8", "r9", "r10", "r11"];

        public static Queue<string> _availableRegs = new();
        public static Dictionary<string, string> _usedRegs = new();

        public static string asm = "";

        public static Dictionary<string, ReservedVariable> _dataSection = new();

        private void Push(string v)
        {
            AddAsm($"push {v}");
            _stackSize++;
        }

        private void Pop(string v)
        {
            AddAsm($"pop {v}");
            _stackSize--;
        }


        public static void Mov(string destination, string source)
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
            foreach (string reg in _scratchRegs)
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

        public static void AddAsm(string code, int tabsCount = 1)
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
                foreach (var argument in func.arguments)
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
                if (VarExists(namedVar.variableName))
                {
                    ErrorHandler.Custom($"Variable {namedVar.variableName} already exists!");
                    return;
                }

                switch (namedVar.variableType)
                {
                    case VariableType.CHAR:
                        _dataSection.Add(namedVar.variableName, new ReservedVariable() { name = namedVar.variableName, word = _words[1],
                            var = namedVar });
                        break;
                    case VariableType.INT:
                        _dataSection.Add(namedVar.variableName, new ReservedVariable() { name = namedVar.variableName, word = _words[4],
                            var = namedVar });
                        break;
                    case VariableType.BOOL:
                        _dataSection.Add(namedVar.variableName, new ReservedVariable() { name = namedVar.variableName, word = _words[1],
                            var = namedVar });
                        break;
                    case VariableType.STRING:
                        //todo
                        break;
                }
            }
            if(var is TempVariable tempVar) 
            {
                GenerateTempVariable(tempVar);
            }
            if(var is LocalVariable localVar)
            {
                GenerateTempVariable(localVar);
            }
        }

        public static void GenerateTempVariable(Variable var)
        {
            string reg = _availableRegs.Dequeue();
            _usedRegs.Add(var.variableName, reg);

            //if(var is )

            switch (var.variableType)
            {
                case VariableType.STRING:
                    Console.WriteLine("TODO 179");
                    break;
                case VariableType.INT:
                    Mov(reg, var.value);
                    break;
                case VariableType.CHAR:
                    Mov(reg, var.value);
                    break;
                case VariableType.BOOL:
                    Mov(reg, var.value);
                    break;
                case VariableType.IDENTIFIER:
                    string val = "";
                    if (_dataSection.TryGetValue(var.value, out ReservedVariable res))
                    {
                        val = $"[{res.var.variableName}]";
                    }
                    else
                    {
                        if (_usedRegs.TryGetValue(var.value, out val)) { }
                        else val = var.value;
                    }
                    Mov(reg, val);
                    break;
            }
        }

        private void GenerateAssign(IRAssign asign)
        {
            string location = GetLocation(asign.identifier);
            string val = asign.value;

            switch (asign.assignedType)
            {
                case VariableType.STRING:
                    //todo
                    break;
                case VariableType.INT:
                    val = asign.value;
                    break;
                case VariableType.CHAR:
                    //AddAsm($"mov {_words[1].longName} [{asign.identifier}], {asign.value}");
                    break;
                case VariableType.BOOL:
                    //AddAsm($"mov {_words[1].longName} [{asign.identifier}], {asign.value}");
                    break;
                case VariableType.IDENTIFIER:
                    if (_dataSection.TryGetValue(asign.value, out ReservedVariable var))
                    { 
                        val = $"[{var.var.variableName}]"; 
                    }
                    else val = _usedRegs[asign.value];
                    break;
            }

            Mov(location, val);
        }

        private void GenerateArithmeticOP(IRArithmeticOp arop)
        {
            string location = _usedRegs[arop.resultLocation.variableName];

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
            }
        }

        private void GenerateDestroyTemp(IRDestroyTemp dtp) 
        {
            string reg = _usedRegs[dtp.temp];

            _availableRegs.Enqueue(reg);
            _usedRegs.Remove(dtp.temp);
        }

        private void GenerateCondition(IRCondition condition) 
        {
            
        }

        private void GenerateCompare(IRCompare compare)
        {
            string sizeA = "";

            if (compare.a is NamedVariable)
            {
                switch (compare.a.variableType)
                {
                    case VariableType.STRING:
                        sizeA = "byte ";
                        break;
                    case VariableType.INT:
                        sizeA = "dword ";
                        break;
                    case VariableType.CHAR:
                        sizeA = "byte ";
                        break;
                    case VariableType.BOOL:
                        sizeA = "byte ";
                        break;
                    case VariableType.IDENTIFIER:
                        sizeA = "dword ";
                        break;
                }
            }
            

            string sizeB = "";

            if (compare.b is NamedVariable)
            {
                switch (compare.b.variableType)
                {
                    case VariableType.STRING:
                        sizeB = "byte ";
                        break;
                    case VariableType.INT:
                        sizeB = "dword ";
                        break;
                    case VariableType.CHAR:
                        sizeB = "byte ";
                        break;
                    case VariableType.BOOL:
                        sizeB = "byte ";
                        break;
                    case VariableType.IDENTIFIER:
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
            if (node is IRCondition cond) { GenerateCondition(cond); }
            if(node is IRCompare comp) { GenerateCompare(comp); }
            if(node is IRJump jump) { GenerateJump(jump); }
            if(node is IRReturn ret) { GenerateReturn(ret); }
        }

        public static string GetLocation(Variable var, bool generateLiteral = false)
        {
            if(var is TempVariable temp)
            {
                if(_usedRegs.TryGetValue(temp.variableName, out string val))
                {
                    return val;
                }
                else
                {
                    GenerateTempVariable(temp);
                    return _usedRegs[temp.variableName];
                }
            }
            else if(var is NamedVariable namedVar)
            {
                return $"[{_dataSection[namedVar.variableName].name}]";
            }
            else if(var is LocalVariable local)
            {
                if (_usedRegs.TryGetValue(local.variableName, out string val))
                {
                    return val;
                }
                else
                {
                    GenerateTempVariable(local);
                    return _usedRegs[local.variableName];
                }
            }
            else if(var is LiteralVariable lit && generateLiteral)
            {
                if (_usedRegs.TryGetValue(lit.variableName, out string val))
                {
                    return val;
                }
                else
                {
                    GenerateTempVariable(lit);
                    return _usedRegs[lit.variableName];
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
