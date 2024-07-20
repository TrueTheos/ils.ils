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

                switch(arg.variableType)
                {
                    case DataType.INT: break;
                    default: ErrorHandler.Custom($"Function '{name}' rquires int as argument!"); break;
                }

                ILS.asmGen.Mov("rax", "60");

                if (arg is LiteralVariable)
                {
                    ILS.asmGen.Mov("rdi", arg.value);
                }
                else
                {
                    ILS.asmGen.Mov("rdi", ILS.asmGen.GetLocation(arg,  ASMGenerator.GetLocationUseCase.None, false));
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
                switch (var.variableType)
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
                        return GetFormat(IRGenerator._allVariables[var.value]);
                }

                return "broke";
            }

            public override void GenerateASM(List<Variable> arguments)
            {
                string format = "";

                Variable msg = arguments[0];

                format = GetFormat(msg);

                ILS.asmGen.Mov("rdi", format);

                ILS.asmGen.Mov("rsi", ILS.asmGen.GetLocation(msg, ASMGenerator.GetLocationUseCase.None, false));

                ILS.asmGen.Mov("rax", "0");

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

            /*private string GetFormat(Variable var)
            {
                switch (var.variableType)
                {
                    case DataType.STRING:
                        return "strFormat";
                    case DataType.INT:
                        return "intFormat";
                    case DataType.CHAR:
                        return "charFormat";
                    case DataType.BOOL:
                        return "intFormat";
                    case DataType.IDENTIFIER:
                        return GetFormat(IRGenerator._allVariables[var.value]);
                }

                return "broke";
            }*/

            public override void GenerateASM(List<Variable> arguments)
            {
                //string format = "";

                Variable msg = arguments[0];

                //format = GetFormat(msg);

                //ILS.asmGen.Mov("rdi", format);

                //ILS.asmGen.Mov("rsi", ILS.asmGen.GetLocation(msg, false, false));

                //ILS.asmGen.Mov("rax", "0");

                ILS.asmGen.Mov("rdi", ILS.asmGen.GetLocation(msg, ASMGenerator.GetLocationUseCase.None, false));
                ILS.asmGen.Mov("rax", "0");

                ILS.asmGen.AddAsm($"call {libcName}");
            }
        }
    }
}
