using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils
{
    public enum TokenType
    {
        IDENTIFIER,

        ASSIGN, // =
        SEMICOLON, // ;
        COLON, // :
        QUOTATION, // "
        SINGLE_QUATATION, // '

        PLUS, // +
        MINUS, // -
        STAR, // *
        SLASH, // /

        OPEN_PARENTHESIS, // (
        CLOSE_PARENTHESIS, // )
        OPEN_CURLY, // {
        CLOSE_CURLY, // }

        LITERAL_STR,
        LITERAL_INT,
        LITERAL_CHAR,

        TYPE_INT,
        TYPE_STRING,
        TYPE_BOOLEAN,
        TYPE_CHAR,

        TRUE,
        FALSE,
    }
}
