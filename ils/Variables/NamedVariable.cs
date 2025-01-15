using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ils.TypeSystem;
using System.Xml.Linq;

namespace ils.Variables
{
    public class NamedVariable : BaseVariable
    {
        public bool isGlobal = false;
        public bool isFuncArg = false;

        public NamedVariable(ASTVariableDeclaration declaration, bool isGlobal, bool isFuncArg)
        {
            Name = "NAMED_VAR";

            variableName = declaration.Name.Value;
            this.isGlobal = isGlobal;
            this.isFuncArg = isFuncArg;

            variableType = declaration.Type;

            if (declaration.Value is not ASTArrayIndex index)
            {
                switch (declaration.Type.DataType)
                {
                    case DataType.STRING:
                        if (declaration.Value == null) SetValue(@"\0", variableType);
                        break;
                    case DataType.INT:
                        if (declaration.Value == null) SetValue("0", variableType);
                        break;
                    case DataType.CHAR:
                        if (declaration.Value == null) SetValue("0", variableType);
                        break;
                    case DataType.BOOL:
                        if (declaration.Value == null) SetValue("0", variableType);
                        break;
                    case DataType.IDENTIFIER:
                        if (declaration.Value == null) SetValue("[]", variableType);
                        break;
                    case DataType.ARRAY:
                        if (declaration.Type is ArrayType arrayType)
                            if (declaration.Value == null)
                                SetValue($"{arrayType.length * 4}", variableType);
                        break;
                }
            }

            IRGenerator.AllVariables.Add(guid.ToString(), this);
        }

        public override string GetString()
        {
            return $"({Name}, {variableName}, {variableType.Name}, {value})";
        }

        public override string GetValueAsString()
        {
            return guid.ToString();
        }
    }
}
