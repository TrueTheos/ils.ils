using ils.IR.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils.IR
{
    public class IRCompare : IRNode
    {
        public BaseVariable a;
        public BaseVariable b;

        public IRCompare(BaseVariable a, BaseVariable b)
        {
            Name = "COMPARE";

            this.a = a;
            this.b = b;
        }

        public override string GetString()
        {
            return $"({Name}, {a.variableName}, {b.variableName})";
        }
    }
}
