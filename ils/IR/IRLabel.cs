using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils.IR
{
    public class IRLabel : IRNode
    {
        public string labelName;

        public IRLabel(string name)
        {
            Name = "LABEL";

            labelName = name;

            IRGenerator.AddLabel(this);
        }

        public override string GetString()
        {
            return $"({Name}, {labelName})";
        }
    }
}
