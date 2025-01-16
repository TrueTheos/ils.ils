using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ils.TypeSystem;
using System.Xml.Linq;

namespace ils.IR.Variables
{
    public class NamedVariable : BaseVariable
    {
        public bool isGlobal = false;
        public bool isFuncArg = false;

        public NamedVariable(ASTVariableDeclaration declaration, bool isGlobal, bool isFuncArg)
        {
            Name = "NAMED_VAR";

            VarName = declaration.Name.Value;
            this.isGlobal = isGlobal;
            this.isFuncArg = isFuncArg;

            if (declaration.Value is not ASTArrayIndex index)
            {
                string val = "";
                TypeSystem.Type type = Types[declaration.Type.DataType];

                switch (declaration.Type.DataType)
                {
                    case ils.DataType.STRING:
                        if (declaration.Value == null) val = @"\0";
                        break;
                    case ils.DataType.INT:
                        if (declaration.Value == null) val = "0";
                        break;
                    case ils.DataType.CHAR:
                        if (declaration.Value == null) val = "0";
                        break;
                    case ils.DataType.BOOL:
                        if (declaration.Value == null) val = "0";
                        break;
                    case ils.DataType.IDENTIFIER:
                        if (declaration.Value == null) val = "[]";
                        break;
                    case ils.DataType.ARRAY:
                        if (declaration.Type is ArrayType arrayType && declaration.Value == null) val = $"{arrayType.length * 4}";
                        break;
                }

                SetValue(new VarValue(type, val));
            }
        }
    }
}
