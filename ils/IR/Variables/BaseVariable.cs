using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ils.IRGenerator;

namespace ils.IR.Variables
{
    public abstract class BaseVariable : IRNode
    {
        public VarID ID { private set; get; }
        public string VarName { protected set; get; }
        public VarValue VarVal { protected set; get; }
        public string Value => VarVal.Value;
        public TypeSystem.Type DataType => VarVal.Type;

        public bool NeedsPreservedReg = false;

        public BaseVariable()
        {
            ID = NewId();
            IRGenerator.AllVariables.Add(ID, this);
        }

        public void SetValue(VarValue val)
        {
            VarVal = val;
        }

        public void AssignVariable(BaseVariable val)
        {
            if (val is TempVariable || val is NamedVariable) SetValue(new VarValue(TypeSystem.Types[ils.DataType.IDENTIFIER], val.ID.ID));
            if (val is LiteralVariable || val is FunctionReturnVariable || val is ArrayVariable) SetValue(val.VarVal);
        }

        public override string GetString()
        {
            return $"({Name}, {VarName}, {ID.ID}, {DataType.Name}, {Value})";
        }
    }
}
