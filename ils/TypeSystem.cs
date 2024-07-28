using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils
{
    public enum DataType { STRING, INT, CHAR, BOOL, IDENTIFIER, VOID, ARRAY }

    public static class TypeSystem
    {
        public static Dictionary<DataType, Type> Types = new Dictionary<DataType, Type>()
        {
            {DataType.INT, new IntType() },
            {DataType.CHAR, new CharType() },
            {DataType.BOOL, new BoolType() },
            {DataType.STRING, new StringType() },
            {DataType.VOID, new VoidType() },
            {DataType.IDENTIFIER, new IdentifierType() },
        };

        public static Type GetTypeFromToken(Token token)
        {
            switch (token.tokenType)
            {
                case TokenType.TYPE_INT: return Types[DataType.INT];
                case TokenType.TYPE_STRING: return Types[DataType.STRING];
                case TokenType.TYPE_CHAR: return Types[DataType.CHAR];
                case TokenType.TYPE_BOOLEAN: return Types[DataType.BOOL];
            }

            ErrorHandler.Custom($"Wrong type {token.tokenType}");
            return null;
        }

        public abstract class Type
        {
            public abstract string Name { get; }
            public abstract DataType DataType { get; }

        }

        public class VoidType : Type
        {
            public override string Name => "void";

            public override DataType DataType => DataType.VOID;
        }

        public class IdentifierType : Type
        {
            public override string Name => "identifier";

            public override DataType DataType => DataType.IDENTIFIER;
        }

        public abstract class PrimitiveType : Type
        {
            public override DataType DataType { get; }

            public bool CanAssignLiteral(ASTExpression expression, int line)
            {
                if(expression is not ASTLiteral literal)
                {
                    ErrorHandler.Throw(new ExpectedError(DataType.ToString(), expression.ToString(), line));
                    return false;
                }

                if(literal.variableType != null && literal.variableType.DataType == DataType)
                {
                    return true;
                }
                else
                {
                    ErrorHandler.Throw(new ExpectedError(DataType.ToString(), literal.variableType.DataType.ToString(), line));
                    return false;
                }
            }
        };

        public class IntType : PrimitiveType
        {
            public override string Name => "int";
            public override DataType DataType => DataType.INT;

        }

        public class BoolType : PrimitiveType
        {
            public override string Name => "bool";
            public override DataType DataType => DataType.BOOL;
        }

        public class CharType : PrimitiveType
        {
            public override string Name => "char";
            public override DataType DataType => DataType.CHAR;
        }

        public class StringType : PrimitiveType
        {
            public override string Name => "str ";
            public override DataType DataType => DataType.STRING;
        }

        public class ArrayType : Type
        {
            public Type elementType;
            public int size = -1;

            public override string Name => "[]";

            public override DataType DataType => DataType.ARRAY;
        }
    }
}
