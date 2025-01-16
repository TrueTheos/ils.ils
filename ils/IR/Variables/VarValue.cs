using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils.IR.Variables
{
    public struct VarValue
    {
        public TypeSystem.Type Type { private set; get; }
        public string Value { private set; get; }
        public ArrayIndexedVariable IndexedVariable { private set; get; }

        public VarValue(TypeSystem.Type type, string value, ArrayIndexedVariable indexedVariable = null)
        {
            Type = type;
            Value = value;
            IndexedVariable = indexedVariable;
        }
    }
}
