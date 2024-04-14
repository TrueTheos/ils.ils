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
            new PrintFunc(),
            new ExitFunc()
        };

        public abstract class BuiltinFunction
        {
            public string name;
            public List<VariableType?> arguments;
            
            public BuiltinFunction(string _name, List<VariableType?> _arguments)
            {
                name = _name;
                arguments = _arguments;
            }
        }

        public abstract class LibcFunction : BuiltinFunction 
        {
            public string libcName;

            public LibcFunction(string _name, List<VariableType?> _arguments, string _libcName) : base(_name, _arguments)
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
                    "exit",
                    [null],
                    ""
                )
            { }

            public override void GenerateASM(List<Variable> arguments)
            {
                Variable arg = arguments[0];

                switch(arg.variableType)
                {
                    case VariableType.INT: break;
                    default: ErrorHandler.Custom($"Function '{name}' rquires int as argument!"); break;
                }

                ASMGenerator.Mov("rax", "60");

                if (arg is LiteralVariable lit)
                {
                    ASMGenerator.Mov("rdi", arg.value);
                }
                else
                {
                    ASMGenerator.Mov("rdi", ASMGenerator.GetLocation(arg));
                }


                ASMGenerator.AddAsm("syscall");
            }
        }

        public class PrintFunc : LibcFunction
        {
            public PrintFunc() : base
                (
                    "println",
                    [null],
                    "printf"
                )
            { }

            public override void GenerateASM(List<Variable> arguments)
            {
                string format = "";

                Variable msg = arguments[0];

                switch (msg.variableType)
                {
                    case VariableType.STRING:
                        format = "strFormat";
                        break;
                    case VariableType.INT:
                        format = "intFormat";
                        break;
                    case VariableType.CHAR:
                        format = "charFormat";
                        break;
                    case VariableType.BOOL:
                        format = "intFormat";
                        break;
                }

                ASMGenerator.Mov("rdi", format);

                ASMGenerator.Mov("rsi", ASMGenerator.GetLocation(msg));

                ASMGenerator.Mov("rax", "0");

                ASMGenerator.AddAsm($"call {libcName}");
            }
        }
    }
}
