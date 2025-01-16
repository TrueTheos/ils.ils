using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils.IR
{
    public abstract class IRNode
    {
        protected string Name;

        public abstract string GetString();
    }
}
