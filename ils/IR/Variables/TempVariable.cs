﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ils.IR.Variables
{
    public class TempVariable : BaseVariable
    {
        public TempVariable(string variableName, TypeSystem.Type varType, string value)
        {
            Name = "TEMP_VAR";
            this.variableName = variableName;
            variableType = varType;
            SetValue(value, variableType);
        }

        public override string GetString()
        {
            return $"({Name}, {guid}, {value})";
        }

        public override string GetValueAsString()
        {
            return guid.ToString();
        }
    }

}
