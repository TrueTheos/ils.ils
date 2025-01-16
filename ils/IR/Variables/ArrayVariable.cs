using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ils.IR.Variables
{
    public class ArrayVariable : BaseVariable
    {
        public int Length;

        public ArrayVariable(VarValue value, int arrayLength)
        {
            Name = "ARRAY_VAR";
            SetValue(value);

            Length = arrayLength;
        }

        public override string GetString()
        {
            return $"({VarName}, {VarVal})";
        }
    }
}
