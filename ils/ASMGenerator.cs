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
            { 1, new Word(){ byteCount = 1, shortName = Word.ShortName.db, longName = Word.LongName.@byte, reserve = "resb" } },
            { 2, new Word(){ byteCount = 2, shortName = Word.ShortName.dw, longName = Word.LongName.word, reserve = "resw" } },
            { 4, new Word(){ byteCount = 4, shortName = Word.ShortName.dd, longName = Word.LongName.dword, reserve = "resd"} },
            { 8, new Word(){ byteCount = 8, shortName = Word.ShortName.dq, longName = Word.LongName.qword, reserve = "resq" } }
        };

        private int _stackSize;

        private readonly List<string> _scratchRegs = ["rsi", "rdx", "rcx", "r8", "r9", "r10", "r11"];

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

        private void NewStack()
        {
            AddAsm("push rbp");
            Mov("rbp", "rsp");
        }

        private void RestoreStack()
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
            AddAsm("strFormat db \"%s\", 0");
            AddAsm("intFormat db \"%d\", 0");
            AddAsm("charFormat db \"%c\", 0");
            AddAsm("extern printf");

            foreach (var data in _dataSection)
            {
                AddAsm($"{data.Value.name}:", 0);
                AddAsm($"{data.Value.word.shortName} {data.Value.var.value}");
            }

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

            switch (var.variableType)
            {
                case VariableType.STRING:
                    break;
                case VariableType.INT:
                    Mov(reg, var.value);
                    break;
                case VariableType.CHAR:
                    break;
                case VariableType.BOOL:
                    break;
                case VariableType.IDENTIFIER:
                    string val = "";
                    if (_dataSection.TryGetValue(var.value, out ReservedVariable res))
                    {
                        val = $"[{res.var.variableName}]";
                    }
                    else val = _usedRegs[var.value];
                    Mov(reg, val);
                    break;
            }
        }

        public static void Mov(string a, string b)
        {
            AddAsm($"mov {a}, {b}");
        }

        private void GenerateAssign(IRAssign asign)
        {
            string location = GetLocation(asign.identifier);
            string val = "";

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

        private void GenerateIRNode(IRNode node)
        {
            if (node is IRLabel label) { AddAsm($".{label.labelName}:"); }
            if (node is Variable variable) { GenerateVariable(variable); }
            if (node is IRAssign asign) { GenerateAssign(asign); }
            if (node is IRFunction func) { GenerateFunction(func); }
            if (node is IRNewStack newStack) { NewStack(); }
            if (node is IRRestoreStack restoreStack) { RestoreStack(); }
            if (node is IRArithmeticOp arOp) { GenerateArithmeticOP(arOp); }
            if (node is IRFunctionCall call) { GenerateFunctionCall(call); }
            if (node is IRDestroyTemp dtp) { GenerateDestroyTemp(dtp); }
        }

        public static string GetLocation(Variable var)
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

            return var.value;
        }

        public struct Word
        {
            public enum ShortName { db, dw, dd, dq}
            public ShortName shortName;
            public enum LongName { @byte, word, dword, qword}
            public LongName longName;
            public string reserve;
            public int byteCount;
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
