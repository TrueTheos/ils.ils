using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils.IR
{
    public class IRDestroyTemp : IRNode
    {
        public string temp;

        public IRDestroyTemp(string temp)
        {
            Name = "DESTROY_TEMP";
            this.temp = temp;
        }

        public override string GetString()
        {
            return $"({Name}, {temp})";
        }
    }
}
