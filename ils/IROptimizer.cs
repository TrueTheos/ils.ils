using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ils.IRGenerator;

namespace ils
{
    public class IROptimizer
    {
        private List<IRNode> _baseIR;

        private List<IRNode> _optimizedIR;

        public List<IRNode> GetOptimizedIR(List<IRNode> ir)
        {
            _baseIR = ir;

            _optimizedIR = _baseIR;

            return _optimizedIR;
        }
    }
}
