using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils
{
    public class Token
    {
        public TokenType tokenType;
        public int line;
        public string value;
        public Token(TokenType tokenType, int line, string value)
        {
            this.tokenType = tokenType;
            this.line = line;
            this.value = value;
        }
    }

    public class Tokenizer
    {
        private string _source;
        private int _index;
        List<Token> _tokens = new();

        public List<Token> Tokenize(string source)
        {
            _source = source; 
            
            string buffer = "";
            int lineCount = 1;

            while(CanPeek())
            {
                char nextChar = Peek();
                if(char.IsLetter(nextChar))
                {
                    buffer += Consume();

                    while(CanPeek() && char.IsLetterOrDigit(Peek())) 
                    {
                        buffer += Consume();
                    }

                    if(buffer == "int")
                    {
                        _tokens.Add(new(TokenType.TYPE_INT, lineCount, buffer));
                        buffer = "";
                    }
                    else if (buffer == "str")
                    {
                        _tokens.Add(new(TokenType.TYPE_STRING, lineCount, buffer));
                        buffer = "";
                    }
                    else if(buffer == "char")
                    {
                        _tokens.Add(new(TokenType.TYPE_CHAR, lineCount, buffer));
                        buffer = "";
                    }
                    else if(buffer == "bool")
                    {
                        _tokens.Add(new(TokenType.TYPE_BOOLEAN, lineCount, buffer));
                        buffer = "";
                    }
                    else if (buffer == "true")
                    {
                        _tokens.Add(new(TokenType.TRUE, lineCount, buffer));
                        buffer = "";
                    }
                    else if (buffer == "false")
                    {
                        _tokens.Add(new(TokenType.FALSE, lineCount, buffer));
                        buffer = "";
                    }
                    else if(Previous(TokenType.QUOTATION)) 
                    {
                        while(CanPeek() && char.IsLetterOrDigit(Peek()))
                        {
                            buffer += Consume();
                        }

                        _tokens.Add(new(TokenType.LITERAL_STR, lineCount, buffer));
                        buffer = "";
                    }
                    else if(Previous(TokenType.SINGLE_QUATATION))
                    {
                        while(CanPeek() && char.IsLetterOrDigit(Peek()))
                        {
                            buffer += Consume();
                        }

                        if(buffer.Length == 1)
                        {
                            _tokens.Add(new(TokenType.LITERAL_CHAR, lineCount, buffer));
                            buffer = "";
                        }
                        else
                        {
                            ErrorHandler.Expected("char", lineCount);
                        }
                    }
                    else
                    {
                        _tokens.Add(new(TokenType.IDENTIFIER, lineCount, buffer));
                        buffer = "";
                    }
                }
                else if(char.IsDigit(Peek()))
                {
                    buffer += Consume();
                    while(CanPeek() && char.IsDigit(Peek()))
                    {
                        buffer += Consume();
                    }
                    _tokens.Add(new(TokenType.LITERAL_INT, lineCount, buffer));
                    buffer = "";
                }
                else if(Expect("//"))
                {
                    Consume();
                    Consume();
                    while(CanPeek() && Peek() != '\n')
                    {
                        Consume();
                    }
                }
                else if(Expect("/*"))
                {
                    Consume();
                    Consume();
                    while(CanPeek())
                    {
                        if(Expect("*/"))
                        {
                            break;
                        }
                        Consume();
                    }
                    if(CanPeek())
                    {
                        Consume();
                    }
                    if(CanPeek())
                    {
                        Consume();
                    }
                }
                else if (Expect("="))
                {
                    Consume();
                    _tokens.Add(new(TokenType.ASSIGN, lineCount, buffer));
                }
                else if(Expect(":"))
                {
                    Consume();
                    _tokens.Add(new(TokenType.COLON, lineCount, buffer));
                }
                else if (Expect(";"))
                {
                    Consume();
                    _tokens.Add(new(TokenType.SEMICOLON, lineCount, buffer));
                }
                else if (Expect("\""))
                {
                    Consume();
                    _tokens.Add(new(TokenType.QUOTATION, lineCount, buffer));
                }
                else if (Expect("'"))
                {
                    Consume();
                    _tokens.Add(new(TokenType.SINGLE_QUATATION, lineCount, buffer));
                }
                else if (Expect("+"))
                {
                    Consume();
                    _tokens.Add(new(TokenType.PLUS, lineCount, buffer));
                }
                else if (Expect("-"))
                {
                    Consume();
                    _tokens.Add(new(TokenType.MINUS, lineCount, buffer));
                }
                else if (Expect("*"))
                {
                    Consume();
                    _tokens.Add(new(TokenType.STAR, lineCount, buffer));
                }
                else if (Expect("/"))
                {
                    Consume();
                    _tokens.Add(new(TokenType.SLASH, lineCount, buffer));
                }
                else if (Expect("\n"))
                {
                    Consume();
                    lineCount++;
                }
                else if(char.IsWhiteSpace(Peek()))
                {
                    Consume();
                }
                else
                {
                    ErrorHandler.Custom($"[{lineCount}] Invalid token: '{buffer}'!");
                }
            }
            _index = 0;
            return _tokens;
        }

        private char Peek(int offset = 0)
        {
            if(_index + offset >= _source.Length)
            {
                return '\0';
            }
            return _source[_index + offset];
        }

        private bool CanPeek()
        {
            return _index < _source.Length && _source[_index] != '\0';
        }

        private char Consume()
        {
            return _source[_index++];
        }

        private bool Previous(TokenType type)
        {
            return _tokens.Count > 0 && _tokens[_tokens.Count - 2].tokenType == type;
        }

        private bool Expect(string next)
        {
            return _index + next.Length < _source.Length && _source.Substring(_index, next.Length) == next;
        }
    }
}
