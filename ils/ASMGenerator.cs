using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using static ils.ASMGenerator;
using static ils.ASMGenerator.Scope;
using static ils.Builtins;
using static ils.IRGenerator;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ils
{
    public class ASMGenerator
    {
        private readonly Dictionary<int, Word> words = new()
        {
            { 1, new Word(){ ShortVarSize = Word.ShortVariableSize.db, LongVarSize = "byte"} },
            { 2, new Word(){ ShortVarSize = Word.ShortVariableSize.dw, LongVarSize = "word"} },
            { 4, new Word(){ ShortVarSize = Word.ShortVariableSize.dd, LongVarSize = "dword"} },
            { 8, new Word(){ ShortVarSize = Word.ShortVariableSize.dq, LongVarSize = "qword"} }
        };

        //ALL REGISTERS IN NASM
        private readonly Dictionary<RegType, Register> allRegs = new()
        {
            {RegType.rax, new() { Type = RegType.rax, IsPreserved =  false}},
            {RegType.rcx, new() { Type = RegType.rcx, IsPreserved =  false}},
            {RegType.rsi, new() { Type = RegType.rsi, IsPreserved =  false}},
            {RegType.rbp, new() { Type = RegType.rbp, IsPreserved =  true }},
            {RegType.rdx, new() { Type = RegType.rdx, IsPreserved =  false}},
            {RegType.rsp, new() { Type = RegType.rsp, IsPreserved =  true }},
            {RegType.rbx, new() { Type = RegType.rbx, IsPreserved =  true}},
            {RegType.r8,  new() { Type = RegType.r8,  IsPreserved =  false}},
            {RegType.r9,  new() { Type = RegType.r9,  IsPreserved =  false}},
            {RegType.r10, new() { Type = RegType.r10, IsPreserved =  false}},
            {RegType.r11, new() { Type = RegType.r11, IsPreserved =  false}},
            {RegType.r12, new() { Type = RegType.r12, IsPreserved =  true}},
            {RegType.r13, new() { Type = RegType.r13, IsPreserved =  true}},
            {RegType.r14, new() { Type = RegType.r14, IsPreserved =  true}},
            {RegType.r15, new() { Type = RegType.r15, IsPreserved =  true }},
        };

        //Registers that we can use and that are not reserved
        private readonly Dictionary<RegType, Register> notReservedRegs = new()
        {
            {RegType.rcx, new() { Type = RegType.rcx, IsPreserved =  false}},
            {RegType.rdx, new() { Type = RegType.rdx, IsPreserved =  false}},
            {RegType.rbx, new() { Type = RegType.rbx, IsPreserved =  true}},
            {RegType.r8,  new() { Type = RegType.r8,  IsPreserved =  false}},
            {RegType.r9,  new() { Type = RegType.r9,  IsPreserved =  false}},
            {RegType.r10, new() { Type = RegType.r10, IsPreserved =  false}},
            {RegType.r11, new() { Type = RegType.r11, IsPreserved =  false}},
            {RegType.r12, new() { Type = RegType.r12, IsPreserved =  true}},
            {RegType.r13, new() { Type = RegType.r13, IsPreserved =  true}},
            {RegType.r14, new() { Type = RegType.r14, IsPreserved =  true}},
            {RegType.r15, new() { Type = RegType.r15, IsPreserved =  true }},
        };

        private Queue<Register> freeRegs = new();
        private Map<string, Register> occupiedRegs = new();

        //private List<string> asm = new();

        private Dictionary<string, ReservedVariable> dataSection = new();

        private Scope currentScope;

        private IRFunction currentFunc;

        private StreamWriter streamWriter;

        public class Scope
        {
            public int ID;

            public List<Register> Vars = new();
            public Dictionary<string, StackVar> StackVars = new();

            private int stackSize = 0;

            public Scope(int id)
            {
                ID = id;
            }

            public void GenerateStackVar(NamedVariable arg)
            {
                if(StackVars.ContainsKey(arg.guid.ToString()))
                {
                    return;
                }

                StackVars.Add(arg.guid.ToString(), new StackVar() { Offset = stackSize, var = arg });
                stackSize++;
            }

            public struct StackVar
            {
                public int Offset;
                public Variable var;
            }
        }

        //private string lastMoveDestination = "";

        public enum MovType 
        {
            Load, // memory -> register 
            Move,  // register -> register
            Store // register -> memory
        }

        public abstract class Address
        {
            public enum AddressType { REG, MEMORY, VALUE }
            public AddressType Type;

            public Address(AddressType type)
            {
                Type = type;
            }
        }

        public class RegAddress : Address
        {
            public RegType Reg;
            public RegAddress(RegType reg) : base(AddressType.REG)
            {
                Reg = reg;
            }
        }

        public class MemoryAddress : Address
        {
            public string Address;
            public string Prefix;
            public MemoryAddress(string address, string prefix) : base(AddressType.MEMORY)
            {
                Address = address;
                Prefix = prefix;
            }
        }

        public class ValueAddress : Address
        {
            public string Value;
            public ValueAddress(string value) : base(AddressType.VALUE)
            {
                Value = value;
            }
        }

        public Register GetRegister(string name, bool needsPreservedReg)
        {
            if(string.IsNullOrEmpty(name))
            {
                name = Guid.NewGuid().ToString();
            }

            if(needsPreservedReg)
            {
                Register reg = freeRegs.DequeueItemWithCondition(i => i.IsPreserved);
                occupiedRegs.Add(name, reg);
                return reg;
            }
            else
            {
                Register reg = freeRegs.Dequeue();
                occupiedRegs.Add(name, reg);
                return reg;
            }           
        }

        //register <- memory
        public void Load(Address a, Address b)
        {
            RegAddress destination;
            MemoryAddress source;

            if (a is RegAddress) destination = (RegAddress)a; else throw new Exception();
            if (b is MemoryAddress) source = (MemoryAddress)b; else throw new Exception();
            string preffix = source.Prefix;
            if (!string.IsNullOrEmpty(preffix)) preffix += " ";

            AddAsm($"mov {destination.Reg}, {preffix}[{source.Address}]");
            Register r = allRegs[destination.Reg];
            r.IsAddress = false;
            allRegs[destination.Reg] = r;
        }

        //register <- register
        public void Move(Address a, Address b)
        {
            RegAddress destination;
            RegAddress source;

            if (a is RegAddress) destination = (RegAddress)a; else throw new Exception();
            if (b is RegAddress) source = (RegAddress)b; else throw new Exception();

            if (allRegs[source.Reg].IsAddress)
            {
                AddAsm($"mov {destination.Reg}, [{source.Reg}]");
            }
            else
            {
                AddAsm($"mov {destination.Reg}, {source.Reg}");
            }

            Register r = allRegs[destination.Reg];
            r.IsAddress = allRegs[source.Reg].IsAddress;
            allRegs[destination.Reg] = r;        
        }

        //memory <- register
        public void Store(Address a, Address b)
        {
            MemoryAddress destination;
            RegAddress source;

            if (a is MemoryAddress) destination = (MemoryAddress)a; else throw new Exception();
            if (b is RegAddress) source = (RegAddress)b; else throw new Exception();
            string preffix = destination.Prefix;
            if (!string.IsNullOrEmpty(preffix)) preffix += " ";
            AddAsm($"mov {preffix}[{destination.Address}], {source.Reg}");
        }

        public void MoveValue(Address destination, Address a)
        {
            ValueAddress source;

            if (a is ValueAddress) source = (ValueAddress)a; else throw new Exception();

            if (destination is RegAddress regAddress)
            {
                AddAsm($"mov {regAddress.Reg}, {source.Value}");
            }
            else if(destination is MemoryAddress memAddress)
            {
                string preffix = memAddress.Prefix;
                if (!string.IsNullOrEmpty(preffix)) preffix += " ";
                AddAsm($"mov {preffix}[{memAddress.Address}], {source.Value}");
            }
        }

        public void AutoMov(Address destination, Address source, bool needsAddress = false)
        {
            if(source.Type == Address.AddressType.VALUE)
            {
                MoveValue(destination, source);
            }
            else if(destination.Type == Address.AddressType.MEMORY)
            {
                if(source.Type == Address.AddressType.MEMORY)
                {
                    Register reg = GetRegister("", false);
                    AutoMov(new RegAddress(reg.Type), source);
                    AutoMov(destination, new RegAddress(reg.Type));
                    FreeReg(reg);
                    return;
                }
                else if(source.Type == Address.AddressType.REG)
                {
                    Store(destination, source);
                }
            }
            else if(destination.Type == Address.AddressType.REG)
            {
                if (source.Type == Address.AddressType.MEMORY)
                {
                    Load(destination, source);
                }
                else if (source.Type == Address.AddressType.REG)
                {
                    Move(destination, source);
                }
            }
        }

        private void FunctionPrologue(IRFunctionPrologue prologue)
        {
            AddAsm("push rbp");
            AutoMov
                (
                    new RegAddress(RegType.rbp),
                    new RegAddress(RegType.rsp)
                );
            if(prologue.localVariables != 0)
            {
                AddAsm($"sub rsp, {prologue.localVariables * 8}");
            }   
        }

        private void FunctionEpilogue()
        {
            AutoMov
                (
                    new RegAddress(RegType.rsp),
                    new RegAddress(RegType.rbp)
                );
            AddAsm("pop rbp");
        }

        public void GenerateASM(List<IRNode> ir, StreamWriter writer)
        {
            streamWriter = writer;
            foreach (var reg in notReservedRegs.Values)
            {
                freeRegs.Enqueue(reg);
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

            AddAsm("strFormatNl db \"%s\", 10, 0");
            AddAsm("intFormatNl db \"%d\", 10, 0");
            AddAsm("charFormatNl db \"%c\", 10, 0");

            if (_StringLiterals.Forward.Count > 0)
            {
                foreach (var strlit in _StringLiterals.Forward.GetDictionary())
                {
                    AddAsm($"{strlit.Key} db `{strlit.Value}`");
                }
            }

            foreach (var data in dataSection)
            {
                AddAsm($"{data.Value.Name} {data.Value.Word.ShortVarSize} {data.Value.Var.value}");
                if (data.Value.Var.variableType.DataType == DataType.ARRAY)
                {
                    //ArrayVariable array = (ArrayVariable)data.Value.var;
                    //AddAsm($"{data.Value.name}_length dw {array.arrayLength}");
                }
            }

            AddAsm("section .text", 0);

            foreach (LibcFunction libcFunc in BuiltinFunctions.Where(x => x is LibcFunction libc && !string.IsNullOrEmpty(libc.libcName)))
            {
                AddAsm($"extern {libcFunc.libcName}");
            }

            //Console.WriteLine('\n' + string.Join("", asm));

            streamWriter.Close();
        }

        public void AddAsm(string code, int tabsCount = 1)
        {
            string tabs = new string('\t', tabsCount);
            //asm.Add(tabs + code + '\n');
            streamWriter.WriteLine(tabs + code + '\n');
            //Console.WriteLine(tabs + code + '\n');
        }

        /*public void ReplaceLastAsm(string code, int tabsCount = 1)
        {
            string tabs = new string('\t', tabsCount);
            asm[asm.Count - 1] = tabs + code + '\n';
        }*/

        private bool VarExists(string name)
        {
            return dataSection.ContainsKey(name);
        }

        private void GenerateFunction(IRFunction func)
        {
            AddAsm($"{func.Name}:", 0);

            currentFunc = func;
            /*foreach (var node in func.nodes)
            {
                if (node is IRFunction) continue;
                GenerateIRNode(node);
            }      */
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
                    AddAsm($"push {GetValueFromAddress(GetLocation(argument, GetLocationUseCase.None, false))}");
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

                    switch (namedVar.variableType.DataType)
                    {
                        case DataType.CHAR:
                            dataSection.Add(namedVar.variableName, new ReservedVariable()
                            {
                                Name = namedVar.variableName,
                                Word = words[1],
                                Var = namedVar
                            });
                            break;
                        case DataType.INT:
                            dataSection.Add(namedVar.variableName, new ReservedVariable()
                            {
                                Name = namedVar.variableName,
                                Word = words[8],
                                Var = namedVar
                            });
                            break;
                        case DataType.BOOL:
                            dataSection.Add(namedVar.variableName, new ReservedVariable()
                            {
                                Name = namedVar.variableName,
                                Word = words[1],
                                Var = namedVar
                            });
                            break;
                        case DataType.STRING:
                            dataSection.Add(namedVar.variableName, new ReservedVariable()
                            {
                                Name = namedVar.variableName,
                                Word = words[8],
                                Var = namedVar
                            });
                            break;
                        case DataType.ARRAY:
                            dataSection.Add(namedVar.variableName, new ReservedVariable()
                            {
                                Name = namedVar.variableName,
                                Word = words[8],
                                Var = namedVar
                            });
                            break;
                    }
                }
                else
                {
                    currentScope.GenerateStackVar(namedVar);
                    if (!namedVar.isFuncArg)
                    {
                       
                        if (namedVar.variableType.DataType == DataType.IDENTIFIER)
                        {
                            Address destination = GetLocation(namedVar, GetLocationUseCase.MovedTo, false);
                            Address source = GetLocation(IRGenerator._AllVariables.Values
                                    .Where(x => x.guid.ToString() == namedVar.value).First(),
                                    GetLocationUseCase.None, false);
                            
                            AutoMov(destination, source);
                        }
                        else if(namedVar.indexedVar != null)
                        {
                            AutoMov(
                                GetLocation(namedVar, GetLocationUseCase.MovedTo, false),
                                GetLocation(namedVar.indexedVar, GetLocationUseCase.None, false)
                            );
                        }
                        else
                        {
                            AutoMov(GetLocation(namedVar, GetLocationUseCase.MovedTo, false),
                                new ValueAddress(namedVar.value));
                        }
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

            reg = GetRegister(var.variableName, var.needsPreservedReg);          

            switch (var.variableType.DataType)
            {
                case DataType.STRING:
                    Console.WriteLine("TODO 179");
                    break;
                case DataType.INT:
                    AutoMov(new RegAddress(reg.Type), new ValueAddress(var.value));
                    break;
                case DataType.CHAR:
                    AutoMov(new RegAddress(reg.Type), new ValueAddress(var.value));
                    break;
                case DataType.BOOL:
                    AutoMov(new RegAddress(reg.Type), new ValueAddress(var.value));
                    break;
                case DataType.IDENTIFIER:
                    AutoMov(new RegAddress(reg.Type),GetLocation(IRGenerator._AllVariables[var.value], GetLocationUseCase.None, false));
                    break;
                case DataType.ARRAY:
                    ErrorHandler.Custom("Tego nie wolno");
                    break;
            }
        }

        private void GenerateAssign(IRAssign asign)
        {
            Address location = GetLocation(asign.identifier, GetLocationUseCase.MovedTo, false);
            Address val = new ValueAddress(asign.value);

            if (asign.indexedArray != null)
            {
                AutoMov(
                    location,
                    GetLocation(asign.indexedArray, GetLocationUseCase.None, false)
                );

                return;
            } //TODO DODAC TUTAJ ARRAY INDEXY

            switch (asign.assignedType.DataType)
            {
                case DataType.STRING:
                    //todo
                    break;
                case DataType.INT:
                    val = new ValueAddress(asign.value);
                    break;
                case DataType.CHAR:
                    //AddAsm($"mov {_words[1].longName} [{asign.identifier}], {asign.value}");
                    break;
                case DataType.BOOL:
                    // AddAsm($"mov {_words[1].longName} [{asign.identifier}], {asign.value}");
                    break;
                case DataType.IDENTIFIER:
                    val = GetLocation(IRGenerator._AllVariables.Values.Where(x => x.variableName == asign.value).First(), GetLocationUseCase.None, false);
                    if (val is RegAddress regAddress && regAddress.Reg == RegType.rbp)
                    {
                        Register reg = GetRegister("", false);
                        AutoMov(new RegAddress(reg.Type), val);
                        val = new RegAddress(reg.Type);
                        FreeReg(reg);
                    }
                    /*if (_dataSection.TryGetValue(asign.value, out ReservedVariable var))
                    { 
                        val = $"[{var.var.variableName}]"; 
                    }
                    else val = _usedRegs[asign.value].name;*/
                    break;
                case DataType.ARRAY:
                    ErrorHandler.Custom("Tego tez nie wolno");
                    break;
            }

            AutoMov(location, val);
        }

        private void GenerateArithmeticOP(IRArithmeticOp arop)
        {
            Address location = GetLocation(arop.resultLocation, GetLocationUseCase.MovedTo, false);

            Address a = GetLocation(arop.a, GetLocationUseCase.None, false);
            Address b = GetLocation(arop.b, GetLocationUseCase.None, false);

            string locationString = GetValueFromAddress(location);
            string bString = GetValueFromAddress(b);

            switch (arop.opType)
            {
                case ArithmeticOpType.ADD:
                    AutoMov(location, a);
                    AddAsm($"add {locationString}, {bString}");
                    break;
                case ArithmeticOpType.MUL:
                    AutoMov(location, a);
                    AddAsm($"imul {locationString}, {bString}");
                    break;
                case ArithmeticOpType.SUB:
                    AutoMov(location, a);
                    AddAsm($"sub {locationString}, {bString}");
                    break;
                case ArithmeticOpType.DIV:
                    AutoMov(new RegAddress(RegType.rax), a);
                    AutoMov(new RegAddress(RegType.rbx), b);
                    AddAsm($"cqo");
                    AddAsm($"div rbx");
                    AutoMov(location, new RegAddress(RegType.rax));
                    break;
                case ArithmeticOpType.MOD:
                    AutoMov(new RegAddress(RegType.rax), a);
                    AutoMov(new RegAddress(RegType.rbx), b);
                    AddAsm($"cqo");
                    AddAsm($"div rbx");
                    AutoMov(location, new RegAddress(RegType.rdx)); //TODO to sie wyjebie na 100% bo rdx jest jako wolny rejestr uzywane
                    break;
            }
        }

        public void FreeReg(Register reg)
        {
            if(notReservedRegs.ContainsKey(reg.Type))
            {
                Register r = notReservedRegs[reg.Type];
                r.IsAddress = false;
                notReservedRegs[reg.Type] = r;
            }
            freeRegs.Enqueue(reg);
            occupiedRegs.Remove(reg);
        }

        private void GenerateDestroyTemp(IRDestroyTemp dtp) 
        {
            if (!occupiedRegs.Forward.Contains(dtp.temp)) return;
            Register reg = occupiedRegs.Forward[dtp.temp];

            FreeReg(reg);
        }

        private void GenerateCompare(IRCompare compare)
        {
            string sizeA = "";
            string sizeB = "";

            Register aReg;
            Register bReg;

            Address compareA = GetLocation(compare.a, GetLocationUseCase.ComparedTo, true);
            Address compareB = GetLocation(compare.b, GetLocationUseCase.None, false);

            if (compare.a is NamedVariable namedA)
            {
                if(namedA.isGlobal)
                {
                    /*switch (compare.a.variableType)
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
                    }*/
                }
                else
                {
                    aReg = freeRegs.Dequeue();
                    AutoMov(new RegAddress(aReg.Type), compareA);
                    compareA = new RegAddress(aReg.Type);
                    occupiedRegs.Add(Guid.NewGuid().ToString(), aReg);
                    FreeReg(aReg);
                }
                            
            }
           
            if (compare.b is NamedVariable namedB)
            {
                if (namedB.isGlobal)
                {
                    switch (compare.b.variableType.DataType)
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
                        case DataType.ARRAY:
                            ErrorHandler.Custom("tego oczywsicie tez nie wolno");
                            break;
                    }
                }
                else
                {
                    bReg = freeRegs.Dequeue();
                    AutoMov(new RegAddress(bReg.Type), compareB);
                    compareB = new RegAddress(bReg.Type);
                    occupiedRegs.Add(Guid.NewGuid().ToString(), bReg);
                    FreeReg(bReg);
                }
            }
            AddAsm($"cmp {sizeA}{GetValueFromAddress(compareA)}, {sizeB}{GetValueFromAddress(compareB)}");
            
            //AddAsm($"cmp {sizeA}{GetLocation(compare.a, false, true)}, {sizeB}{GetLocation(compare.b, false, false)}");
        }

        private void GenerateJump(IRJump jump)
        {
            switch (jump.conditionType)
            {
                case ConditionType.EQUAL:
                    AddAsm($"je .{jump.label}");
                    break;
                case ConditionType.NOT_EQUAL:
                    AddAsm($"jne .{jump.label}");
                    break;
                case ConditionType.LESS:
                    AddAsm($"jl .{jump.label}");
                    break;
                case ConditionType.LESS_EQUAL:
                    AddAsm($"jle .{jump.label}");
                    break;
                case ConditionType.GREATER:
                    AddAsm($"jg .{jump.label}");
                    break;
                case ConditionType.GREATER_EQUAL:
                    AddAsm($"jge .{jump.label}");
                    break;
                case ConditionType.NONE:
                    AddAsm($"jmp .{jump.label}");
                    break;
            }
        }

        private void GenerateReturn(IRReturn ret)
        {
             AutoMov(new RegAddress(RegType.rax), GetLocation(ret.ret, GetLocationUseCase.None, false));
        }

        private void GenerateScopeStart(IRScopeStart start)
        {
            currentScope = new Scope(start.scope.id);
            foreach (var par in currentFunc.Parameters)
            {
                GenerateVariable(par);
            }
        }

        private void GenerateScopeEnd(IRScopeEnd end)
        {
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

        public enum GetLocationUseCase {None, MovedTo, ComparedTo}

        public string GetValueFromAddress(Address address)
        {
            if(address is RegAddress regAddress)
            {
                return regAddress.Reg.ToString();
            }
            else if(address is MemoryAddress memAddress)
            {
                string prefix = memAddress.Prefix;
                if (!string.IsNullOrEmpty(prefix)) prefix += " ";
                return $"{prefix}[{memAddress.Address}]";
            }
            else if(address is ValueAddress valAddress)
            {
                return valAddress.Value;
            }

            Console.WriteLine("ROZJEBALEM SIE TUTAJ");
            return "";
        }

        public Address GetLocation(Variable var, GetLocationUseCase useCase, bool generateLiteral)
        {
            if (var is TempVariable temp)
            {
                if (occupiedRegs.Forward.TryGet(temp.variableName, out Register val))
                {
                    /*if (useCase == GetLocationUseCase.Pointer)
                    {
                        return new Address()
                        {
                            Type = Address.AddressType.memory,
                            address = val.regType.ToString()
                        };
                    }
                    else
                    {
                        return new Address() { Type = Address.AddressType.reg, reg = val.regType };
                    }*/

                    return new RegAddress(val.Type);
                }
                else
                {
                    GenerateTempVariable(temp);
                    return new RegAddress(occupiedRegs.Forward[temp.variableName].Type);
                }
            }
            else if (var is NamedVariable namedVar)
            {
                if (namedVar.isGlobal)
                {
                    if (useCase == GetLocationUseCase.ComparedTo || useCase == GetLocationUseCase.MovedTo)
                    {
                        return new MemoryAddress(dataSection[namedVar.variableName].Name, dataSection[namedVar.variableName].Word.LongVarSize);
                    }

                    return new MemoryAddress(dataSection[namedVar.variableName].Name, "");
                }
                else
                {
                    if (!currentScope.StackVars.TryGetValue(namedVar.guid.ToString(), out StackVar val))
                    {
                        currentScope.GenerateStackVar(namedVar);
                    }

                    if (namedVar.isFuncArg)
                    {
                        if (useCase == GetLocationUseCase.ComparedTo || useCase == GetLocationUseCase.MovedTo)
                        {
                            return new MemoryAddress($"rbp+{16 + currentScope.StackVars[namedVar.guid.ToString()].Offset * 8}", "qword");
                        }
                        else
                        {
                            return new MemoryAddress($"rbp+{16 + currentScope.StackVars[namedVar.guid.ToString()].Offset * 8}", "");
                        }
                    }
                    else
                    {
                        int offset = currentScope.StackVars[namedVar.guid.ToString()].Offset * 8;
                        if (offset == 0) offset = 8;
                        if (useCase == GetLocationUseCase.ComparedTo || useCase == GetLocationUseCase.MovedTo)
                        {
                            return new MemoryAddress($"rbp-{offset}", "qword");
                        }
                        else
                        {
                            return new MemoryAddress($"rbp-{offset}", "");
                        }
                    }
                }
            }
            else if (var is ArrayIndexedVariable indexed)
            {
                Address indexAddress = GetLocation(indexed.Index, GetLocationUseCase.None, true);
                //Address arrayAddress = GetLocation(indexed, GetLocationUseCase.None, true);
                // Register register = _availableRegs.Dequeue();
                //_usedRegs.Add(Guid.NewGuid().ToString(), register);
                //Address result = new Address() { Type = Address.AddressType.reg, reg = register.regType };
                switch (indexAddress)
                {
                    case RegAddress regAddress:
                        AddAsm($"mov rsi, [{indexed.Array.variableName} + {regAddress.Reg} * 8]");
                        break;
                    case MemoryAddress memAddress:
                        AddAsm($"mov rsi, [{indexed.Array.variableName} + {memAddress.Address}]");
                        break;
                    case ValueAddress valAddress:
                        AddAsm($"mov rsi, [{indexed.Array.variableName} + {valAddress.Value}]");
                        break;
                }

                return new RegAddress(RegType.rsi);
            }
            else if (var is LiteralVariable lit && generateLiteral)
            {
                if (lit.variableType.DataType == DataType.STRING)
                {
                    return new ValueAddress(lit.value);
                }

                if (occupiedRegs.Forward.TryGet(lit.variableName, out Register val))
                {
                    return new RegAddress(val.Type);
                }
                else
                {
                    GenerateTempVariable(lit);

                    return new RegAddress(occupiedRegs.Forward[lit.variableName].Type);
                }
            }
            else if (var is FunctionReturnVariable regvar)
            {
                GenerateFunctionCall(regvar.call);
                //return "rax"; // regvar.funcName + regvar.index.ToString();

                return new RegAddress(RegType.rax);
            }

            return new ValueAddress(var.value);
        }

        public struct Word
        {
            public enum ShortVariableSize { db, dw, dd, dq}
            public ShortVariableSize ShortVarSize;
            public string LongVarSize;
        }

        public struct ReservedVariable
        {
            public string Name;
            public Word Word;
            public Variable Var;
            public string InitialValue;
        }

        public struct Register
        {
            public RegType Type;
            public bool IsPreserved;
            public bool IsAddress; //if false then it means that it contains a value, if true then an address
        }
    }
}
