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

        private readonly Dictionary<RegType, Register> _allRegs = new()
        {
            {RegType.rcx, new() { regType = RegType.rcx, isPreserved =  false}},
            {RegType.rsi, new() { regType = RegType.rsi, isPreserved =  false}},
            {RegType.rbp, new() { regType = RegType.rbp, isPreserved = true }},
            {RegType.rdx, new() { regType = RegType.rdx, isPreserved =  false}},
            {RegType.rsp, new() { regType = RegType.rsp, isPreserved = true }},
            {RegType.rbx, new() { regType = RegType.rbx, isPreserved =  true}},
            {RegType.r8, new()  { regType = RegType.r8,   isPreserved =  false}},
            {RegType.r9, new()  { regType = RegType.r9,   isPreserved =  false}},
            {RegType.r10, new() { regType = RegType.r10, isPreserved =  false}},
            {RegType.r11, new() { regType = RegType.r11, isPreserved =  false}},
            {RegType.r12, new() { regType = RegType.r12, isPreserved =  true}},
            {RegType.r13, new() { regType = RegType.r13, isPreserved =  true}},
            {RegType.r14, new() { regType = RegType.r14, isPreserved =  true}},
            {RegType.r15, new() { regType = RegType.r15, isPreserved = true }},
        };

        private readonly Dictionary<RegType, Register> _regs = new()
        {
            {RegType.rcx, new() { regType = RegType.rcx, isPreserved =  false}},
            {RegType.rdx, new() { regType = RegType.rdx, isPreserved =  false}},
            {RegType.rbx, new() { regType = RegType.rbx, isPreserved =  true}},
            {RegType.r8, new()  { regType = RegType.r8,  isPreserved =  false}},
            {RegType.r9, new()  { regType = RegType.r9,  isPreserved =  false}},
            {RegType.r10, new() { regType = RegType.r10, isPreserved =  false}},
            {RegType.r11, new() { regType = RegType.r11, isPreserved =  false}},
            {RegType.r12, new() { regType = RegType.r12, isPreserved =  true}},
            {RegType.r13, new() { regType = RegType.r13, isPreserved =  true}},
            {RegType.r14, new() { regType = RegType.r14, isPreserved =  true}},
            {RegType.r15, new() { regType = RegType.r15, isPreserved = true }},
        };

        public enum RegType
        {
            rcx, rdx, rbx, r8, r9, r10, r11, r12, r13, r14, r15, rbp, rsp, rax, rdi, rsi
        }

        public struct Register
        {
            public RegType regType;
            public bool isPreserved;
            public bool isAddress; //if false then it means that it contains a value, if true then an address
        }

        public Queue<Register> AvailableRegs = new();
        public Map<string, Register> OccupiedRegs = new();

        public List<string> Asm = new();

        public Dictionary<string, ReservedVariable> DataSection = new();

        public Scope CurrentScope;

        private IRFunction currentFunc;

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
                Register reg = AvailableRegs.DequeueItemWithCondition(i => i.isPreserved);
                OccupiedRegs.Add(name, reg);
                return reg;
            }
            else
            {
                Register reg = AvailableRegs.Dequeue();
                OccupiedRegs.Add(name, reg);
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
            Register r = _allRegs[destination.Reg];
            r.isAddress = false;
            _allRegs[destination.Reg] = r;
        }

        //register <- register
        public void Move(Address a, Address b)
        {
            RegAddress destination;
            RegAddress source;

            if (a is RegAddress) destination = (RegAddress)a; else throw new Exception();
            if (b is RegAddress) source = (RegAddress)b; else throw new Exception();

            if (_allRegs[source.Reg].isAddress)
            {
                AddAsm($"mov {destination.Reg}, [{source.Reg}]");
            }
            else
            {
                AddAsm($"mov {destination.Reg}, {source.Reg}");
            }

            Register r = _allRegs[destination.Reg];
            r.isAddress = _allRegs[source.Reg].isAddress;
            _allRegs[destination.Reg] = r;        
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
                    AutoMov(new RegAddress(reg.regType), source);
                    AutoMov(destination, new RegAddress(reg.regType));
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

        public string GenerateASM(List<IRNode> ir)
        {
            foreach (var reg in _regs.Values)
            {
                AvailableRegs.Enqueue(reg);
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

            if (StringLiterals.Forward.Count > 0)
            {
                foreach (var strlit in StringLiterals.Forward.GetDictionary())
                {
                    AddAsm($"{strlit.Key} db `{strlit.Value}`");
                }
            }

            foreach (var data in DataSection)
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

            return string.Join("", Asm);
        }

        public void AddAsm(string code, int tabsCount = 1)
        {
            string tabs = new string('\t', tabsCount);
            Asm.Add(tabs + code + '\n');
            //Console.WriteLine(tabs + code + '\n');
        }

        public void ReplaceLastAsm(string code, int tabsCount = 1)
        {
            string tabs = new string('\t', tabsCount);
            Asm[Asm.Count - 1] = tabs + code + '\n';
        }

        private bool VarExists(string name)
        {
            return DataSection.ContainsKey(name);
        }

        private void GenerateFunction(IRFunction func)
        {
            AddAsm($"{func.name}:", 0);

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
                            DataSection.Add(namedVar.variableName, new ReservedVariable()
                            {
                                Name = namedVar.variableName,
                                Word = words[1],
                                Var = namedVar
                            });
                            break;
                        case DataType.INT:
                            DataSection.Add(namedVar.variableName, new ReservedVariable()
                            {
                                Name = namedVar.variableName,
                                Word = words[8],
                                Var = namedVar
                            });
                            break;
                        case DataType.BOOL:
                            DataSection.Add(namedVar.variableName, new ReservedVariable()
                            {
                                Name = namedVar.variableName,
                                Word = words[1],
                                Var = namedVar
                            });
                            break;
                        case DataType.STRING:
                            DataSection.Add(namedVar.variableName, new ReservedVariable()
                            {
                                Name = namedVar.variableName,
                                Word = words[8],
                                Var = namedVar
                            });
                            break;
                        case DataType.ARRAY:
                            DataSection.Add(namedVar.variableName, new ReservedVariable()
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
                    CurrentScope.GenerateStackVar(namedVar);
                    if (!namedVar.isFuncArg)
                    {
                       
                        if (namedVar.variableType.DataType == DataType.IDENTIFIER)
                        {
                            Address destination = GetLocation(namedVar, GetLocationUseCase.MovedTo, false);
                            Address source = GetLocation(IRGenerator._allVariables.Values
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
                    AutoMov(new RegAddress(reg.regType), new ValueAddress(var.value));
                    break;
                case DataType.CHAR:
                    AutoMov(new RegAddress(reg.regType), new ValueAddress(var.value));
                    break;
                case DataType.BOOL:
                    AutoMov(new RegAddress(reg.regType), new ValueAddress(var.value));
                    break;
                case DataType.IDENTIFIER:
                    AutoMov(new RegAddress(reg.regType),GetLocation(IRGenerator._allVariables[var.value], GetLocationUseCase.None, false));
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
                    val = GetLocation(IRGenerator._allVariables.Values.Where(x => x.variableName == asign.value).First(), GetLocationUseCase.None, false);
                    if (val is RegAddress regAddress && regAddress.Reg == RegType.rbp)
                    {
                        Register reg = GetRegister("", false);
                        AutoMov(new RegAddress(reg.regType), val);
                        val = new RegAddress(reg.regType);
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
            if(_regs.ContainsKey(reg.regType))
            {
                Register r = _regs[reg.regType];
                r.isAddress = false;
                _regs[reg.regType] = r;
            }
            AvailableRegs.Enqueue(reg);
            OccupiedRegs.Remove(reg);
        }

        private void GenerateDestroyTemp(IRDestroyTemp dtp) 
        {
            if (!OccupiedRegs.Forward.Contains(dtp.temp)) return;
            Register reg = OccupiedRegs.Forward[dtp.temp];

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
                    aReg = AvailableRegs.Dequeue();
                    AutoMov(new RegAddress(aReg.regType), compareA);
                    compareA = new RegAddress(aReg.regType);
                    OccupiedRegs.Add(Guid.NewGuid().ToString(), aReg);
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
                    bReg = AvailableRegs.Dequeue();
                    AutoMov(new RegAddress(bReg.regType), compareB);
                    compareB = new RegAddress(bReg.regType);
                    OccupiedRegs.Add(Guid.NewGuid().ToString(), bReg);
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
             AutoMov(new RegAddress(RegType.rax), GetLocation(ret.ret, GetLocationUseCase.None, false));
        }

        private void GenerateScopeStart(IRScopeStart start)
        {
            CurrentScope = new Scope(start.scope.id);
            foreach (var par in currentFunc.parameters)
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
                if (OccupiedRegs.Forward.TryGet(temp.variableName, out Register val))
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

                    return new RegAddress(val.regType);
                }
                else
                {
                    GenerateTempVariable(temp);
                    return new RegAddress(OccupiedRegs.Forward[temp.variableName].regType);
                }
            }
            else if (var is NamedVariable namedVar)
            {
                if (namedVar.isGlobal)
                {
                    if (useCase == GetLocationUseCase.ComparedTo || useCase == GetLocationUseCase.MovedTo)
                    {
                        return new MemoryAddress(DataSection[namedVar.variableName].Name, DataSection[namedVar.variableName].Word.LongVarSize);
                    }

                    return new MemoryAddress(DataSection[namedVar.variableName].Name, "");
                }
                else
                {
                    if (!CurrentScope.stackvars.TryGetValue(namedVar.guid.ToString(), out StackVar val))
                    {
                        CurrentScope.GenerateStackVar(namedVar);
                    }

                    if (namedVar.isFuncArg)
                    {
                        if (useCase == GetLocationUseCase.ComparedTo || useCase == GetLocationUseCase.MovedTo)
                        {
                            return new MemoryAddress($"rbp+{16 + CurrentScope.stackvars[namedVar.guid.ToString()].offset * 8}", "qword");
                        }
                        else
                        {
                            return new MemoryAddress($"rbp+{16 + CurrentScope.stackvars[namedVar.guid.ToString()].offset * 8}", "");
                        }
                    }
                    else
                    {
                        int offset = CurrentScope.stackvars[namedVar.guid.ToString()].offset * 8;
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

                if (OccupiedRegs.Forward.TryGet(lit.variableName, out Register val))
                {
                    return new RegAddress(val.regType);
                }
                else
                {
                    GenerateTempVariable(lit);

                    return new RegAddress(OccupiedRegs.Forward[lit.variableName].regType);
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
    }
}
