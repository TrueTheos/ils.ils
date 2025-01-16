using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ils.IR.Variables
{
    public class TempVariable : BaseVariable
    {
        public TempVariable(string variableName, VarValue value)
        {
            Name = "TEMP_VAR";
            this.VarName = variableName;
            SetValue(value);
        }
    }
}
