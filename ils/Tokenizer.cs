using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils
{
    public class Token
    {
        public readonly TokenType TokenType;
        public readonly int Line;
        public readonly string Value;
        public readonly int StartIndex, EndIndex;
        public Token(TokenType tokenType, int line, string value, int startIndex, int endIndex)
        {
            this.TokenType = tokenType;
            this.Line = line;
            this.Value = value;
            this.StartIndex = startIndex;
            this.EndIndex = endIndex;
        }
    }

    public class Tokenizer
    {
        private readonly Dictionary<string, TokenType> keywords = new()
        {
            ["int"] = TokenType.TYPE_INT,
            ["str"] = TokenType.TYPE_STRING,
            ["char"] = TokenType.TYPE_CHAR,
            ["bool"] = TokenType.TYPE_BOOLEAN,
            ["true"] = TokenType.TRUE,
            ["false"] = TokenType.FALSE,
            ["if"] = TokenType.IF,
            ["elif"] = TokenType.ELIF,
            ["else"] = TokenType.ELSE,
            ["while"] = TokenType.WHILE,
            ["break"] = TokenType.BREAK,
            ["fun"] = TokenType.FUNCTION,
            ["return"] = TokenType.RETURN
        };

        private string source;
        private int index;
        private List<Token> tokens = new();
        private int lineCount = 1;
        private int startIndex, endIndex;

        private void AddToken(TokenType tokenType, string value)
        {
            tokens.Add(new Token(tokenType, lineCount, value, startIndex, endIndex));
            startIndex = endIndex;
        }

        public List<Token> Tokenize(string src)
        {
            source = src;
            tokens.Clear();
            index = 0;
            lineCount = 1;

            while (CanPeek())
            {
                char c = Peek();
                if (IsValidStartChar(c) && (!char.IsSymbol(Peek(1)) || Peek(1) == '_'))
                {
                    TokenizeIdentifierOrKeyword();
                }
                else if (char.IsDigit(c))
                {
                    TokenizeNumber();
                }
                else if (c is '"' or '\'')
                {
                    TokenizeStringOrCharLiteral();
                }
                else
                {
                    TokenizeOperatorOrPunctuation();
                }
            }
            index = 0;
            return tokens;
        }

        private void TokenizeIdentifierOrKeyword()
        {
            string buffer = "";
            if (IsValidStartChar(Peek())) buffer = Consume().ToString();
            buffer += ConsumeWhile(c => char.IsLetterOrDigit(c) || c == '_');
            if (keywords.TryGetValue(buffer, out TokenType type))
            {
                AddToken(type, buffer);
            }
            else
            {
                AddToken(TokenType.IDENTIFIER, buffer);
            }
        }

        private void TokenizeNumber()
        {
            string buffer = ConsumeWhile(char.IsDigit);
            AddToken(TokenType.LITERAL_INT, buffer);
        }

        private void TokenizeStringOrCharLiteral()
        {
            char quote = Consume();
            string buffer = ConsumeWhile(c => c != quote);
            if (CanPeek() && Peek() == quote)
            {
                Consume(); 
                AddToken(quote == '"' ? TokenType.LITERAL_STR : TokenType.LITERAL_CHAR, buffer);
            }
            else
            {
                ErrorHandler.Throw(new ExpectedError($"{quote}", null, lineCount));
            }
        }

        private void TokenizeOperatorOrPunctuation()
        {
            switch (Peek())
            {
                case '+': AddTokenAndAdvance(TokenType.PLUS); break;
                case '-':
                    if (Peek(1) == '>')
                    {
                        AddTokenAndAdvance(TokenType.RETURN_TYPE, 2);
                    }
                    else
                    {
                        AddTokenAndAdvance(TokenType.MINUS);
                    }
                    break;
                case '*': AddTokenAndAdvance(TokenType.STAR); break;
                case '/':
                    if (Peek(1) == '/')
                    {
                        ConsumeLineComment();
                    }
                    else if (Peek(1) == '*')
                    {
                        ConsumeBlockComment();
                    }
                    else
                    {
                        AddTokenAndAdvance(TokenType.SLASH);
                    }
                    break;
                case '&':
                    if (Peek(1) == '&')
                    {
                        AddTokenAndAdvance(TokenType.AND);
                    }
                    else
                    {
                        AddTokenAndAdvance(TokenType.BITWISE_AND);
                    }
                    break;
                case '|':
                    if (Peek(1) == '|')
                    {
                        AddTokenAndAdvance(TokenType.OR);
                    }
                    else
                    {
                        AddTokenAndAdvance(TokenType.BITWISE_OR);
                    }
                    break;
                case '%': AddTokenAndAdvance(TokenType.PERCENT); break;
                case '=':
                    if (Peek(1) == '=')
                    {
                        AddTokenAndAdvance(TokenType.EQUALS, 2);
                    }
                    else
                    {
                        AddTokenAndAdvance(TokenType.ASSIGN);
                    }
                    break;
                case '!':
                    if (Peek(1) == '=')
                    {
                        AddTokenAndAdvance(TokenType.NOT_EQUAL, 2);
                    }
                    else
                    {
                        AddTokenAndAdvance(TokenType.NOT);
                    }
                    break;
                case '>':
                    if (Peek(1) == '=')
                    {
                        AddTokenAndAdvance(TokenType.GREATER_EQUAL, 2);
                    }
                    else
                    {
                        AddTokenAndAdvance(TokenType.GREATER);
                    }
                    break;
                case '<':
                    if (Peek(1) == '=')
                    {
                        AddTokenAndAdvance(TokenType.LESS_EQUAL, 2);
                    }
                    else
                    {
                        AddTokenAndAdvance(TokenType.LESS);
                    }
                    break;
                case '(': AddTokenAndAdvance(TokenType.OPEN_PARENTHESIS); break;
                case ')': AddTokenAndAdvance(TokenType.CLOSE_PARENTHESIS); break;
                case '{': AddTokenAndAdvance(TokenType.OPEN_CURLY); break;
                case '}': AddTokenAndAdvance(TokenType.CLOSE_CURLY); break;
                case '[': AddTokenAndAdvance(TokenType.OPEN_SQUARE); break;
                case ']': AddTokenAndAdvance(TokenType.CLOSE_SQUARE); break;
                case ':': AddTokenAndAdvance(TokenType.COLON); break;
                case ';': AddTokenAndAdvance(TokenType.SEMICOLON); break;
                case ',': AddTokenAndAdvance(TokenType.COMMA); break;
                case '"': AddTokenAndAdvance(TokenType.QUOTATION); break;
                case '\'': AddTokenAndAdvance(TokenType.SINGLE_QUATATION); break;
                case '\n':
                    Consume();
                    lineCount++;
                    break;
                case var c when char.IsWhiteSpace(c):
                    Consume();
                    break;
                default:
                    ErrorHandler.Custom($"[{lineCount}] Invalid token: '{Peek()}'!");
                    Consume(); // Skip the invalid character
                    break;
            }
        }

        private void ConsumeLineComment()
        {
            Consume();
            Consume();

            while (CanPeek() && Peek() != '\n')
            {
                Consume();
            }
        }

        private void ConsumeBlockComment()
        {
            // Consume the opening /*
            Consume();
            Consume();

            while (CanPeek())
            {
                if (Peek() == '*' && Peek(1) == '/')
                {
                    // Found the closing */
                    Consume(); // Consume *
                    Consume(); // Consume /
                    return;
                }
                else if (Peek() == '\n')
                {
                    lineCount++;
                }
                Consume();
            }

            // If we get here, the comment was never closed
            ErrorHandler.Throw(new UnexpectedEndOfInputError("Unclosed block comment", lineCount));
        }

        private void AddTokenAndAdvance(TokenType type, int advance = 1)
        {
            AddToken(type, source.Substring(index, advance));
            index += advance;
        }

        private string ConsumeWhile(Func<char, bool> predicate)
        {
            int start = index;
            while (CanPeek() && predicate(Peek()))
            {
                Consume();
            }
            return source[start..index];
        }


        private char Peek(int offset = 0)
        {
            if(index + offset >= source.Length)
            {
                return '\0';
            }
            return source[index + offset];
        }

        private bool CanPeek()
        {
            return index < source.Length && source[index] != '\0';
        }

        private char Consume()
        {
            endIndex++;
            
            return source[index++];
        }

        private bool Previous(TokenType type)
        {
            return tokens.Count > 1 && tokens[tokens.Count - 1].TokenType == type;
        }

        private bool Expect(string next)
        {
            return index + next.Length < source.Length && source.Substring(index, next.Length) == next;
        }

        private bool IsValidStartChar(char c)
        {
            if (char.IsDigit(c)) return false;
            if(char.IsLetter(c)) return true;
            if (c == '_' || c == '@') return true;

            return false;
        }
    }
}
