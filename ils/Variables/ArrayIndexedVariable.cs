using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ils.Variables
{
    public class ArrayIndexedVariable : BaseVariable
    {
        public BaseVariable Array;
        public BaseVariable Index;

        public ArrayIndexedVariable(BaseVariable index, BaseVariable array)
        {
            Name = "ARRAY_INDEXED_VAR";
            Index = index;
            Array = array;
        }

        public override string GetString()
        {
            return $"({Name}, {Index})";
        }

        public override string GetValueAsString()
        {
            return "";
        }
    }
}
