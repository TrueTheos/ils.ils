using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ils.IRGenerator;
using System.Xml.Linq;

namespace ils.IR.Variables
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
            VarName = funcName;
            this.index = index;
            this.call = call;
            SetValue(new VarValue(varType, "rax"));
        }
    }
}
