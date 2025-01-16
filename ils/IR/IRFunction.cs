using ils.IR.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils.IR
{
    public class IRFunction : IRNode
    {
        public string Name;
        public TypeSystem.Type ReturnType = TypeSystem.Types[DataType.VOID];
        public List<NamedVariable> Parameters = new();
        public List<IRNode> Nodes = new();
        public int UseCount;

        public bool WasUsed => UseCount > 0;

        public IRFunction(string name, TypeSystem.Type returnType, List<NamedVariable> parameters)
        {
            base.Name = "FUNC";
            this.Name = name;
            if (returnType != null) ReturnType = returnType;
            Parameters = parameters;
        }

        public override string GetString()
        {
            string r = $"({base.Name}, {Name}, {ReturnType.Name}";
            foreach (NamedVariable parameter in Parameters) r += $", {parameter.VarName}";

            r += ")";
            return r;
        }
    }
}
