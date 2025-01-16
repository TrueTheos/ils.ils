using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils.IR
{
    public class IRScopeEnd : IRNode
    {
        public Scope scope;
        public int valuesToClear;

        public IRScopeEnd(Scope scope)
        {
            Name = "END_SCOPE";
            this.scope = scope;
        }

        public override string GetString()
        {
            return $"({Name})";
        }
    }
}
