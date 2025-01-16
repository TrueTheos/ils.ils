using ils.IR.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils.IR
{
    public class IRAssign : IRNode
    {
        public BaseVariable identifier;
        public TypeSystem.Type assignedType;
        public string value;
        public ArrayIndexedVariable indexedArray; //todo fix

        public IRAssign(BaseVariable identifier, string value, TypeSystem.Type assignedType)
        {
            Name = "ASSIGN";

            this.identifier = identifier;
            this.value = value;
            this.assignedType = assignedType;
        }

        public override string GetString()
        {
            return $"({Name}, {identifier.guid}, {value})";
        }
    }
}
