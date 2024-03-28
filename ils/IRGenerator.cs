using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils
{
    public class IRGenerator
    {
        public void Generate(List<ASTStatement> statements)
        {

        }

        public abstract class IRNode 
        {
            public string Name;
        }

        public class IRAssign : IRNode
        {
            public IRAssign()
            {
                Name = "ASS";
            }
        }
    }
}
