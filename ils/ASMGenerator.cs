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
        private Dictionary<int, Word> _words = new()
        {
            { 1, new Word(){ shortName = Word.ShortName.db, longName = "byte"} },
            { 2, new Word(){ shortName = Word.ShortName.dw, longName = "word"} },
            { 4, new Word(){ shortName = Word.ShortName.dd, longName = "dword"} },
            { 8, new Word(){ shortName = Word.ShortName.dq, longName = "qword"} }
        };

        private readonly Dictionary<RegType, Register> _regs = new()
        {
            {RegType.rcx, new() { regType = RegType.rcx, isPreserved =  false}},
            {RegType.rdx, new() { regType = RegType.rdx, isPreserved =  false}},
            {RegType.rbx, new() { regType = RegType.rbx, isPreserved =  true}},
            {RegType.r8, new() { regType = RegType.r8,  isPreserved =  false}},
            {RegType.r9, new() { regType = RegType.r9,  isPreserved =  false}},
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

        public enum MovType 
        {
            Load, // memory -> register 
            Move,  // register -> register
            Store // register -> memory
        }

        public struct Address
        {
            public enum Type { reg, memory, value }
            public Type type;

            public string prefix;
            public RegType reg;
            public string address;
            public string value;
        }

        //register <- memory
        public void Load(Address destination, Address source)
        {
            string preffix = source.prefix;
            if (!string.IsNullOrEmpty(preffix)) preffix += " ";

            AddAsm($"mov {destination.reg}, {preffix}[{source.address}]");
        }

        //register <- register
        public void Move(Address destination, Address source)
        {
            AddAsm($"mov {destination.reg}, {source.reg}");
        }

        //memory <- register
        public void Store(Address destination, Address source)
        {
            string preffix = destination.prefix;
            if (!string.IsNullOrEmpty(preffix)) preffix += " ";
            AddAsm($"mov {preffix}[{destination.address}], {source.reg}");
        }

        public void MoveValue(Address destination, Address source)
        {
            if(destination.type == Address.Type.reg)
            {
                AddAsm($"mov {destination.reg}, {source.value}");
            }
            else if(destination.type == Address.Type.memory)
            {
                string preffix = destination.prefix;
                if (!string.IsNullOrEmpty(preffix)) preffix += " ";
                AddAsm($"mov {preffix}[{destination.address}], {source.value}");
            }
        }

        public void AutoMov(Address destination, Address source)
        {
            if(source.type == Address.Type.value)
            {
                MoveValue(destination, source);
            }
            else if(destination.type == Address.Type.memory)
            {
                if(source.type == Address.Type.memory)
                {
                    Console.WriteLine("tak nie wolno xd");
                    return;
                }
                else if(source.type == Address.Type.reg)
                {
                    Store(destination, source);
                }
            }
            else if(destination.type == Address.Type.reg)
            {
                if (source.type == Address.Type.memory)
                {
                    Load(destination, source);
                }
                else if (source.type == Address.Type.reg)
                {
                    Move(destination, source);
                }
            }
        }

        /*public void Mov(string destination, string source)
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
        }*/

        private void FunctionPrologue(IRFunctionPrologue prologue)
        {
            AddAsm("push rbp");
            AutoMov
                (
                    new Address() { type = Address.Type.reg, reg = RegType.rbp },
                    new Address() { type = Address.Type.reg, reg = RegType.rsp }
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
                    new Address() { type = Address.Type.reg, reg = RegType.rsp },
                    new Address() { type = Address.Type.reg, reg = RegType.rbp }
                );
            AddAsm("pop rbp");
        }

        public string GenerateASM(List<IRNode> ir)
        {
            foreach (var reg in _regs.Values)
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

            AddAsm("strFormatNl db \"%s\", 10, 0");
            AddAsm("intFormatNl db \"%d\", 10, 0");
            AddAsm("charFormatNl db \"%c\", 10, 0");

            if (stringLiterals.Forward.Count > 0)
            {
                foreach (var strlit in stringLiterals.Forward.GetDictionary())
                {
                    AddAsm($"{strlit.Key} db `{strlit.Value}`");
                }
            }

            foreach (var data in _dataSection)
            {
                AddAsm($"{data.Value.name} {data.Value.word.shortName} {data.Value.var.value}");
            }

            AddAsm("section .text", 0);

            foreach (LibcFunction libcFunc in BuiltinFunctions.Where(x => x is LibcFunction libc && !string.IsNullOrEmpty(libc.libcName)))
            {
                AddAsm($"extern {libcFunc.libcName}");
            }    

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
                                word = _words[8],
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
                            _dataSection.Add(namedVar.variableName, new ReservedVariable()
                            {
                                name = namedVar.variableName,
                                word = _words[8],
                                var = namedVar
                            });
                            break;
                    }
                }
                else
                {
                    _currentScope.GenerateStackVar(namedVar);
                    if (!namedVar.isFuncArg)
                    {
                        string val = namedVar.value;

                        if (namedVar.variableType == DataType.IDENTIFIER)
                        {
                            AutoMov(
                                GetLocation(namedVar, GetLocationUseCase.MovedTo, false),
                                GetLocation(IRGenerator._allVariables.Values.Where(x => x.guid.ToString() == namedVar.value).First(), GetLocationUseCase.None, false));
                        }
                        else
                        {
                            AutoMov(GetLocation(namedVar, GetLocationUseCase.MovedTo, false),
                                new Address() { type = Address.Type.value, value = val });
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
                    AutoMov(
                        new Address() { type = Address.Type.reg, reg = reg.regType },
                        new Address() { type = Address.Type.value, value = var.value }
                        );
                    break;
                case DataType.CHAR:
                    AutoMov(
                       new Address() { type = Address.Type.reg, reg = reg.regType },
                       new Address() { type = Address.Type.value, value = var.value }
                       );
                    break;
                case DataType.BOOL:
                    AutoMov(
                       new Address() { type = Address.Type.reg, reg = reg.regType },
                       new Address() { type = Address.Type.value, value = var.value }
                       );
                    break;
                case DataType.IDENTIFIER:
                    AutoMov(
                       new Address() { type = Address.Type.reg, reg = reg.regType },
                       GetLocation(IRGenerator._allVariables[var.value], GetLocationUseCase.None, false)
                       );
                    break;
            }
        }

        private void GenerateAssign(IRAssign asign)
        {
            Address location = GetLocation(asign.identifier, GetLocationUseCase.MovedTo, false);
            Address val = new Address() { type = Address.Type.value, value = asign.value } ;
            //todo tutaj albo memory albo value, idk

            switch (asign.assignedType)
            {
                case DataType.STRING:
                    //todo
                    break;
                case DataType.INT:
                    val = new Address() { type = Address.Type.value, value = asign.value };
                    break;
                case DataType.CHAR:
                    //AddAsm($"mov {_words[1].longName} [{asign.identifier}], {asign.value}");
                    break;
                case DataType.BOOL:
                    // AddAsm($"mov {_words[1].longName} [{asign.identifier}], {asign.value}");
                    break;
                case DataType.IDENTIFIER:
                    val = GetLocation(IRGenerator._allVariables.Values.Where(x => x.variableName == asign.value).First(), GetLocationUseCase.None, false);
                    if (val.type == Address.Type.reg && val.reg == RegType.rbp)
                    {
                        Register reg = _availableRegs.Dequeue();
                        _usedRegs.Add(Guid.NewGuid().ToString(), reg);
                        AutoMov(new Address() { type = Address.Type.reg, reg = reg.regType }, val);
                        val = new Address() { type = Address.Type.reg, reg = reg.regType };
                        FreeReg(reg);
                    }
                    /*if (_dataSection.TryGetValue(asign.value, out ReservedVariable var))
                    { 
                        val = $"[{var.var.variableName}]"; 
                    }
                    else val = _usedRegs[asign.value].name;*/
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
                    AutoMov(new Address() { type = Address.Type.reg, reg = RegType.rax }, a);
                    AutoMov(new Address() { type = Address.Type.reg, reg = RegType.rbx }, b);
                    AddAsm($"cqo");
                    AddAsm($"div rbx");
                    AutoMov(location, new Address() { type = Address.Type.reg, reg = RegType.rax });
                    break;
                case ArithmeticOpType.MOD:
                    AutoMov(new Address() { type = Address.Type.reg, reg = RegType.rax }, a);
                    AutoMov(new Address() { type = Address.Type.reg, reg = RegType.rbx }, b);
                    AddAsm($"cqo");
                    AddAsm($"div rbx");
                    AutoMov(location, new Address() { type = Address.Type.reg, reg = RegType.rdx }); //TODO to sie wyjebie na 100% bo rdx jest jako wolny rejestr uzywane
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
                    aReg = _availableRegs.Dequeue();
                    AutoMov(new Address() { type = Address.Type.reg, reg = aReg.regType }, compareA);
                    compareA = new Address() { type = Address.Type.reg, reg = aReg.regType};
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
                    AutoMov(new Address() { type = Address.Type.reg, reg = bReg.regType }, compareB);
                    compareB = new Address() { type = Address.Type.reg, reg = bReg.regType };
                    _usedRegs.Add(Guid.NewGuid().ToString(), bReg);
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
             AutoMov(new Address() { type = Address.Type.reg, reg = RegType.rax }, GetLocation(ret.ret, GetLocationUseCase.None, false));
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

        public enum GetLocationUseCase {None, MovedTo, ComparedTo}

        public string GetValueFromAddress(Address address)
        {
            if(address.type == Address.Type.reg)
            {
                return address.reg.ToString();
            }
            else if(address.type == Address.Type.memory)
            {
                string prefix = address.prefix;
                if (!string.IsNullOrEmpty(prefix)) prefix += " ";
                return $"{prefix}[{address.address}]";
            }
            else if(address.type == Address.Type.value)
            {
                return address.value;
            }

            Console.WriteLine("ROZJEBALEM SIE TUTAJ");
            return "";
        }

        public Address GetLocation(Variable var, GetLocationUseCase useCase, bool generateLiteral)
        {
            if (var is TempVariable temp)
            {
                Register val;
                if (_usedRegs.Forward.TryGet(temp.variableName, out val))
                {
                    return new Address() { type = Address.Type.reg, reg = val.regType };
                }
                else
                {
                    GenerateTempVariable(temp);
                    return new Address() { type = Address.Type.reg, reg = _usedRegs.Forward[temp.variableName].regType };
                }
            }
            else if (var is NamedVariable namedVar)
            {
                if (namedVar.isGlobal)
                {
                    if (useCase == GetLocationUseCase.ComparedTo || useCase == GetLocationUseCase.MovedTo)
                    {
                        return new Address()
                        {
                            type = Address.Type.memory,
                            address = _dataSection[namedVar.variableName].name,
                            prefix = _dataSection[namedVar.variableName].word.longName
                        };
                    }

                    return new Address()
                    {
                        type = Address.Type.memory,
                        address = _dataSection[namedVar.variableName].name
                    };
                }
                else
                {
                    if (!_currentScope.stackvars.TryGetValue(namedVar.guid.ToString(), out StackVar val))
                    {
                        _currentScope.GenerateStackVar(namedVar);
                    }

                    if (namedVar.isFuncArg)
                    {
                        if (useCase == GetLocationUseCase.ComparedTo || useCase == GetLocationUseCase.MovedTo)
                        {
                            return new Address()
                            {
                                type = Address.Type.memory,
                                address = $"rbp+{16 + _currentScope.stackvars[namedVar.guid.ToString()].offset * 8}",
                                prefix = "qword"
                            };
                        }
                        else
                        {
                            return new Address()
                            {
                                type = Address.Type.memory,
                                address = $"rbp+{16 + _currentScope.stackvars[namedVar.guid.ToString()].offset * 8}",
                            };
                        }
                    }
                    else
                    {
                        int offset = _currentScope.stackvars[namedVar.guid.ToString()].offset * 8;
                        if (offset == 0) offset = 8;
                        if (useCase == GetLocationUseCase.ComparedTo || useCase == GetLocationUseCase.MovedTo)
                        {
                            return new Address()
                            {
                                type = Address.Type.memory,
                                address = $"rbp-{offset}",
                                prefix = "qword"
                            };
                        }
                        else
                        {
                            return new Address()
                            {
                                type = Address.Type.memory,
                                address = $"rbp-{offset}",
                            };
                        }
                    }
                }
            }
            else if (var is LiteralVariable lit && generateLiteral)
            {
                if (lit.variableType == DataType.STRING)
                {
                    return new Address()
                    {
                        type = Address.Type.value,
                        value = lit.value,
                    };
                }

                if (_usedRegs.Forward.TryGet(lit.variableName, out Register val))
                {
                    return new Address()
                    {
                        type = Address.Type.reg,
                        reg = val.regType
                    };
                }
                else
                {
                    GenerateTempVariable(lit);

                    return new Address()
                    {
                        type = Address.Type.reg,
                        reg = _usedRegs.Forward[lit.variableName].regType
                    };
                }
            }
            else if (var is FunctionReturnVariable regvar)
            {
                GenerateFunctionCall(regvar.call);
                //return "rax"; // regvar.funcName + regvar.index.ToString();

                return new Address()
                {
                    type = Address.Type.reg,
                    reg = RegType.rax
                };
            }

            return new Address()
            {
                type = Address.Type.value,
                value = var.value
            };
        }

        public struct Word
        {
            public enum ShortName { db, dw, dd, dq}
            public ShortName shortName;
            public string longName;
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
