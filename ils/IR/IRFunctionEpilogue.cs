using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils.IR
{
    public class IRFunctionEpilogue : IRNode
    {
        public IRFunctionEpilogue()
        {
            Name = "EPILOGUE";
        }

        public override string GetString()
        {
            return $"({Name})";
        }
    }
}
