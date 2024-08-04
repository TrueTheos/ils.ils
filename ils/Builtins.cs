using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ils.IRGenerator;

namespace ils
{
    public static class Builtins
    {
        public static List<BuiltinFunction> BuiltinFunctions = new()
        {
            new PrintlnFunc(),
            new ExitFunc(),
            new PrintFunc()
        };

        public static bool IsBuiltIn(string name)
        {
            return BuiltinFunctions.Any(x => x.name == name);
        }

        public abstract class BuiltinFunction
        {
            public string name;
            public List<DataType?> arguments;
            
            public BuiltinFunction(string _name, List<DataType?> _arguments)
            {
                name = _name;
                arguments = _arguments;
            }
        }

        public abstract class LibcFunction : BuiltinFunction 
        {
            public string libcName;

            public LibcFunction(string _name, List<DataType?> _arguments, string _libcName) : base(_name, _arguments)
            {
                libcName = _libcName; 
            }

            public abstract void GenerateASM(List<Variable> args);

            public void Generate(List<Variable> args)
            {
                if (args.Count != arguments.Count)
                {
                    ErrorHandler.Custom($"Function '{name}' takes {arguments} arguments!");
                }
                else
                {
                    GenerateASM(args);
                }
            }
        }

        public class ExitFunc : LibcFunction
        {
            public ExitFunc() : base
                (
                    "@exit",
                    [null],
                    ""
                )
            { }

            public override void GenerateASM(List<Variable> arguments)
            {
                Variable arg = arguments[0];

                switch(arg.variableType.DataType)
                {
                    case DataType.INT: break;
                    default: ErrorHandler.Custom($"Function '{name}' requires int as argument!"); break;
                }

                ILS.asmGen.AutoMov(new ASMGenerator.RegAddress(RegType.rax), new ASMGenerator.ValueAddress("60"));

                if (arg is LiteralVariable)
                {
                    ILS.asmGen.AutoMov(new ASMGenerator.RegAddress(RegType.rdi), new ASMGenerator.ValueAddress(arg.value));
                }
                else
                {
                    ILS.asmGen.AutoMov(new ASMGenerator.RegAddress(RegType.rdi), ILS.asmGen.GetLocation(arg, ASMGenerator.GetLocationUseCase.None, false));
                }

                ILS.asmGen.AddAsm("syscall");
            }
        }

        public class PrintlnFunc : LibcFunction
        {
            public PrintlnFunc() : base
                (
                    "@println",
                    [null],
                    "printf"
                )
            { }

            private string GetFormat(Variable var)
            {
                switch (var.variableType.DataType)
                {
                    case DataType.STRING:
                        return "strFormatNl";
                    case DataType.INT:
                        return "intFormatNl";
                    case DataType.CHAR:
                        return "charFormatNl";
                    case DataType.BOOL:
                        return "intFormatNl";
                    case DataType.IDENTIFIER:
                        return GetFormat(IRGenerator._AllVariables[var.value]);
                    case DataType.ARRAY:
                        ErrorHandler.Custom("Can't print arrays!");
                        return null;
                }

                return "broke";
            }

            public override void GenerateASM(List<Variable> arguments)
            {
                Variable msg = arguments[0];

                ILS.asmGen.AutoMov(new ASMGenerator.RegAddress(RegType.rdi), new ASMGenerator.ValueAddress(GetFormat(msg)));

                ILS.asmGen.AutoMov(
                    new ASMGenerator.RegAddress(RegType.rsi),
                    ILS.asmGen.GetLocation(msg, ASMGenerator.GetLocationUseCase.None, false),
                    needsAddress: false);

                ILS.asmGen.AutoMov(new ASMGenerator.RegAddress(RegType.rax), new ASMGenerator.ValueAddress("0"));

                ILS.asmGen.AddAsm($"call {libcName}");
            }
        }

        public class PrintFunc : LibcFunction
        {
            public PrintFunc() : base
                (
                    "@print",
                    [null],
                    "puts"
                )
            { }

            public override void GenerateASM(List<Variable> arguments)
            {
                Variable msg = arguments[0];

                ILS.asmGen.AutoMov(new ASMGenerator.RegAddress(RegType.rdi), ILS.asmGen.GetLocation(msg, ASMGenerator.GetLocationUseCase.None, false));
                ILS.asmGen.AutoMov(new ASMGenerator.RegAddress(RegType.rax),new ASMGenerator.ValueAddress("0"));
                ILS.asmGen.AddAsm($"call {libcName}");
            }
        }
    }
}
