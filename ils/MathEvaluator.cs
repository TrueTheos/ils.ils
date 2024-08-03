using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ils
{
    public static class MathEvaluator
    {
        private static string _expression;
        private static int _index;

        public static int Evaluate(string expression)
        {
            _expression = expression;
            _index = 0;
            return ParseExpression();
        }

        public static int Evaluate(ASTArithmeticOperation expression)
        {
            string converted = ConvertToExpressionString(expression);
            return Evaluate(converted);
        }

        public static string ConvertToExpressionString(ASTExpression node)
        {
            if (node is ASTIntLiteral intLiteral)
            {
                return intLiteral.value.ToString();
            }
            else if (node is ASTArithmeticOperation operation)
            {
                string left = ConvertToExpressionString(operation.LeftNode);
                string right = ConvertToExpressionString(operation.RightNode);
                string op = operation.Operation switch
                {
                    ArithmeticOpType.ADD => " + ",
                    ArithmeticOpType.SUB => " - ",
                    ArithmeticOpType.DIV => " / ",
                    ArithmeticOpType.MUL => " * ",
                    _ => throw new NotImplementedException()
                };
                return "(" + left + op + right + ")";
            }
            else
            {
                throw new InvalidOperationException("Unknown AST node type");
            }
        }

        private static int ParseExpression()
        {
            int value = ParseTerm();
            while (_index < _expression.Length)
            {
                char op = _expression[_index];
                if (op != '+' && op != '-')
                    break;

                _index++;
                int nextTerm = ParseTerm();
                if (op == '+')
                    value += nextTerm;
                else
                    value -= nextTerm;
            }
            return value;
        }

        private static int ParseTerm()
        {
            int value = ParseFactor();
            while (_index < _expression.Length)
            {
                char op = _expression[_index];
                if (op != '*' && op != '/' && op != '%')
                    break;

                _index++;
                int nextFactor = ParseFactor();
                if (op == '*')
                    value *= nextFactor;
                else if (op == '/')
                    value /= nextFactor;
                else
                    value %= nextFactor;
            }
            return value;
        }

        private static int ParseFactor()
        {
            SkipWhitespace();
            int value;
            if (_expression[_index] == '(')
            {
                _index++; // Skip '('
                value = ParseExpression();
                _index++; // Skip ')'
            }
            else
            {
                int start = _index;
                while (_index < _expression.Length && char.IsDigit(_expression[_index]))
                {
                    _index++;
                }
                value = int.Parse(_expression[start.._index]);
            }
            SkipWhitespace();
            return value;
        }

        private static void SkipWhitespace()
        {
            while (_index < _expression.Length && char.IsWhiteSpace(_expression[_index]))
            {
                _index++;
            }
        }
    }
}
