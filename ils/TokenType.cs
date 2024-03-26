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
