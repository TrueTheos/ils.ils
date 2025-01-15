using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ils.Variables
{
    public class LiteralVariable : BaseVariable
    {
        public LiteralVariable(string value, TypeSystem.Type type)
        {
            Name = "LIT_VAR";

            variableType = type;
            //this.variableName = $"LIT_{literalVarsCount}_{value}";

            if (type.DataType == DataType.STRING)
            {
                if (!IRGenerator.StringLiterals.Reverse.Contains(value))
                {
                    string name = "STR_" + IRGenerator.StringLiterals.Forward.Count.ToString();
                    IRGenerator.StringLiterals.Add(name, value);
                    SetValue(name, variableType);
                }
                else
                {
                    SetValue(value, variableType);
                }
            }
            else
            {
                variableName = value.ToString();
                SetValue(value, variableType);
            }

            IRGenerator.AllVariables.Add(guid.ToString(), this);
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
