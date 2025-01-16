using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils.IR
{
    public class IRJump : IRNode
    {
        public string label;
        public ConditionType conditionType;

        public IRJump(string label, ConditionType conditionType)
        {
            this.label = label;
            this.conditionType = conditionType;

            switch (this.conditionType)
            {
                case ConditionType.EQUAL:
                    Name = "JUMP_EQUAL";
                    break;
                case ConditionType.NOT_EQUAL:
                    Name = "JUMP_NOT_EQUAL";
                    break;
                case ConditionType.LESS:
                    Name = "JUMP_LESS";
                    break;
                case ConditionType.LESS_EQUAL:
                    Name = "JUMP_LESS_EQUAL";
                    break;
                case ConditionType.GREATER:
                    Name = "JUMP_GREATER";
                    break;
                case ConditionType.GREATER_EQUAL:
                    Name = "JUMP_GREATER_EQUAL";
                    break;
                case ConditionType.NONE:
                    Name = "JUMP";
                    break;
            }
        }

        public override string GetString()
        {
            return $"({Name}, {label})";
        }
    }
}
