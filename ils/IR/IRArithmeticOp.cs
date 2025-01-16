using ils.IR.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils.IR
{
    public class IRArithmeticOp : IRNode
    {
        public BaseVariable resultLocation;
        public BaseVariable a;
        public BaseVariable b;
        public ArithmeticOpType opType;

        public IRArithmeticOp(BaseVariable resultLocation, BaseVariable a, BaseVariable b, ArithmeticOpType opType)
        {
            this.resultLocation = resultLocation;
            this.a = a;
            this.b = b;
            this.opType = opType;

            switch (opType)
            {
                case ArithmeticOpType.ADD:
                    Name = "ADD";
                    break;
                case ArithmeticOpType.MUL:
                    Name = "MUL";
                    break;
                case ArithmeticOpType.SUB:
                    Name = "SUB";
                    break;
                case ArithmeticOpType.DIV:
                    Name = "DIV";
                    break;
                case ArithmeticOpType.MOD:
                    Name = "MOD";
                    break;
            }
        }

        public override string GetString()
        {
            return $"({Name}, {resultLocation.guid} = {a.guid}, {b.guid})";
        }
    }
}
