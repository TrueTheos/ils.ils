using ils.IR.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils.IR
{
    public class IRReturn : IRNode
    {
        public BaseVariable ret;

        public IRReturn(BaseVariable ret, int valuesOnStackToClear)
        {
            Name = "RETURN";

            this.ret = ret;
        }

        public override string GetString()
        {
            return $"({Name}, {ret.variableName})";
        }
    }
}
