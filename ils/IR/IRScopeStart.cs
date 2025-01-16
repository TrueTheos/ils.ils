using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils.IR
{
    public class IRScopeStart : IRNode
    {
        public Scope scope;

        public IRScopeStart(Scope scope)
        {
            Name = "START_SCOPE";
            this.scope = scope;
        }

        public override string GetString()
        {
            return $"({Name})";
        }
    }
}
