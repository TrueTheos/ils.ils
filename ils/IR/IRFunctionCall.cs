using ils.IR.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils.IR
{
    public class IRFunctionCall : IRNode
    {
        public string name;
        public List<BaseVariable> arguments = new();

        public IRFunctionCall(string name, List<BaseVariable> arguments)
        {
            Name = "FUNC_CALL";

            this.name = name;
            this.arguments = arguments;
        }

        public override string GetString()
        {
            string r = $"(CALL, {name}";
            foreach (BaseVariable parameter in arguments) r += $", {parameter.Value}";

            r += ")";
            return r;
        }
    }
}
