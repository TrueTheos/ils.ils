using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ils.IRGenerator;
using System.Xml.Linq;

namespace ils.Variables
{
    public class FunctionReturnVariable : BaseVariable
    {
        public string funcName;
        public int index;
        public IRFunctionCall call;

        public FunctionReturnVariable(string funcName, TypeSystem.Type varType, int index, IRFunctionCall call)
        {
            Name = "FUNC_RETURN_VAR";
            this.funcName = funcName;
            variableName = funcName;
            variableType = varType;
            this.index = index;
            this.call = call;
            //SetValue(reg, variableType);

            AllVariables[guid.ToString()] = this;
        }

        public override string GetString()
        {
            return $"({Name}, {variableName})";
        }

        public override string GetValueAsString()
        {
            return funcName + index.ToString();
        }
    }
}
