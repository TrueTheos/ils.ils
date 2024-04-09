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
            new PrintFunc()
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

            public abstract List<string> GenerateASM(List<Variable> args);

            public void Generate(List<Variable> args)
            {
                if (args.Count != arguments.Count)
                {
                    ErrorHandler.Custom($"Function '{name}' takes {arguments} arguments!");
                }
            }
        }

        public class PrintFunc : LibcFunction
        {
            public PrintFunc() : base
                (
                    "print",
                    [null],
                    "printf"
                )
            { }

            public override List<string> GenerateASM(List<Variable> arguments)
            {
                List<string> r = new();

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

                if(msg is LiteralVariable literal)
                {
                    r.Add($"mov eax, {literal.value}");
                }
                else
                {
                    r.Add($"mov eax, [{msg.variableName}]");
                }

                r.Add("push eax");
                r.Add($"push {format}");
                r.Add($"call {libcName}");

                return r;
            }
        }
    }
}
