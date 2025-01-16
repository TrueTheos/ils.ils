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

        public ArrayVariable(TypeSystem.Type varType, string value, int arrayLength)
        {
            Name = "ARRAY_VAR";
            variableType = varType;
            SetValue(value, variableType);

            IRGenerator.AllVariables.Add(guid.ToString(), this);
            Length = arrayLength;
        }

        public override string GetString()
        {
            return $"({Name}, {value})";
        }

        public override string GetValueAsString()
        {
            return value;
        }
    }
}
