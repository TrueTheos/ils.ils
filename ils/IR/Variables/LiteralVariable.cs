using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ils.IR.Variables
{
    public class LiteralVariable : BaseVariable
    {
        public LiteralVariable(VarValue value)
        {
            Name = "LIT_VAR";
            //this.variableName = $"LIT_{literalVarsCount}_{value}";

            if (value.Type.DataType == ils.DataType.STRING)
            {
                if (!IRGenerator.StringLiterals.Reverse.Contains(value.Value))
                {
                    string name = "STR_" + IRGenerator.StringLiterals.Forward.Count.ToString();
                    IRGenerator.StringLiterals.Add(name, value.Value);
                    SetValue(new VarValue(TypeSystem.Types[ils.DataType.STRING], name));
                }
                else
                {
                    SetValue(value);
                }
            }
            else
            {
                VarName = value.Value;
                SetValue(value);
            }
        }
    }
}
