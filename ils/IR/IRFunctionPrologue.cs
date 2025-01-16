using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils.IR
{
    public class IRFunctionPrologue : IRNode
    {
        public int localVariables;

        public IRFunctionPrologue()
        {
            Name = "PROLOGUE";
        }

        public override string GetString()
        {
            return $"({Name})";
        }
    }
}
