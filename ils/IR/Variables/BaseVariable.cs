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
        public string variableName = "";
        public TypeSystem.Type variableType;
        public string value { private set; get; }
        public ArrayIndexedVariable indexedVar; //todo THATS STUPID FIX ME

        public IRNode lastUse = null;

        public bool needsPreservedReg = false;

        public string guid;

        public BaseVariable()
        {
            guid = NewId();
            IRGenerator.AllVariables.Add(guid, this);
        }

        public void SetValue(string val, TypeSystem.Type valType)
        {
            variableType = valType;

            value = val;
        }

        public abstract string GetValueAsString();

        public void AssignVariable(BaseVariable val)
        {
            if (val is TempVariable) SetValue(val.GetValueAsString(), TypeSystem.Types[DataType.IDENTIFIER]);
            if (val is NamedVariable) SetValue(val.GetValueAsString(), TypeSystem.Types[DataType.IDENTIFIER]);
            if (val is LiteralVariable literalVar) SetValue(val.GetValueAsString(), literalVar.variableType);
            if (val is FunctionReturnVariable) SetValue("rax", val.variableType);
            if (val is ArrayVariable arrayVar) SetValue(val.GetValueAsString(), arrayVar.variableType);
        }

        public void UpdateDestroyAfter(IRNode node)
        {
            lastUse = node;
        }
    }
}
