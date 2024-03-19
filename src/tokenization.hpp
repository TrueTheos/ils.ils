#pragma once

#include <string>
#include <vector>

enum class TokenType {
    EXIT,
    INT_LITERAL,
    STR_LITERAL,
    QUOTATION,
    SEMICOLON,
    OPEN_PARENTHESIS,
    CLOSE_PARENTHESIS,
    IDENTIFIER,
    LET,
    ASSIGN,
    PLUS,
    STAR,
    MINUS,
    FSLASH,
    OPEN_CURLY,
    CLOSE_CURLY,
    IF_,
    ELIF,
    ELSE_,
    PRINT,
};

inline std::string to_string(const TokenType type)
{
    switch (type) {
    case TokenType::PRINT:
        return "`PRINT`";
    case TokenType::EXIT:
        return "`EXIT`";
    case TokenType::INT_LITERAL:
        return "int literal";
    case TokenType::STR_LITERAL:
        return "str literal";
    case TokenType::QUOTATION:
        return "`\"`";
    case TokenType::SEMICOLON:
        return "`;`";
    case TokenType::OPEN_PARENTHESIS:
        return "`(`";
    case TokenType::CLOSE_PARENTHESIS:
        return "`)`";
    case TokenType::IDENTIFIER:
        return "identifier";
    case TokenType::LET:
        return "`let`";
    case TokenType::ASSIGN:
        return "`=`";
    case TokenType::PLUS:
        return "`+`";
    case TokenType::STAR:
        return "`*`";
    case TokenType::MINUS:
        return "`-`";
    case TokenType::FSLASH:
        return "`/`";
    case TokenType::OPEN_CURLY:
        return "`{`";
    case TokenType::CLOSE_CURLY:
        return "`}`";
    case TokenType::IF_:
        return "`if`";
    case TokenType::ELIF:
        return "`elif`";
    case TokenType::ELSE_:
        return "`else`";
    }
    assert(false);
}

inline std::optional<int> bin_prec(const TokenType type)
{
    switch (type) 
    {
        case TokenType::MINUS:
        case TokenType::PLUS:
            return 0;
        case TokenType::FSLASH:
        case TokenType::STAR:
            return 1;
        default:
            return {};
    }
}

struct Token 
{
    TokenType type;
    int line;
    std::optional<std::string> value {};
};

class Tokenizer 
{
    public:
        explicit Tokenizer(std::string src)
            : _src(std::move(src))
        {
        }

        std::vector<Token> tokenize()
        {
            std::vector<Token> tokens;
            std::string buf;
            int line_count = 1;
            while (peek().has_value()) {
                if (std::isalpha(peek().value())) {
                    buf.push_back(consume());
                    //maybe here if the last token was a " we can ignore checking for numbers etc
                    while (peek().has_value() && std::isalnum(peek().value())) {
                        buf.push_back(consume());
                    }
                    if (buf == "exit") {
                        tokens.push_back({ TokenType::EXIT, line_count });
                        buf.clear();
                    }
                    else if (buf == "print") {
                        tokens.push_back({ TokenType::PRINT, line_count });
                        buf.clear();
                    }
                    else if (buf == "let") {
                        tokens.push_back({ TokenType::LET, line_count });
                        buf.clear();
                    }
                    else if (buf == "if") {
                        tokens.push_back({ TokenType::IF_, line_count });
                        buf.clear();
                    }
                    else if (buf == "elif") {
                        tokens.push_back({ TokenType::ELIF, line_count });
                        buf.clear();
                    }
                    else if (buf == "else") {
                        tokens.push_back({ TokenType::ELSE_, line_count });
                        buf.clear();
                    }
                    else if(tokens.back().type == TokenType::QUOTATION)
                    {
                        tokens.push_back({TokenType::STR_LITERAL});
                        buf.clear();
                    }
                    else {
                        tokens.push_back({ TokenType::IDENTIFIER, line_count, buf });
                        buf.clear();
                    }
                }
                else if (std::isdigit(peek().value())) {
                    buf.push_back(consume());
                    while (peek().has_value() && std::isdigit(peek().value())) {
                        buf.push_back(consume());
                    }
                    tokens.push_back({ TokenType::INT_LITERAL, line_count, buf });
                    buf.clear();
                }
                else if (expect("//")) {
                    consume();
                    consume();
                    while (peek().has_value() && peek().value() != '\n') {
                        consume();
                    }
                }
                else if (expect("/*")) {
                    consume();
                    consume();
                    while (peek().has_value()) {
                        if (expect("*/")) {
                            break;
                        }
                        consume();
                    }
                    if (peek().has_value()) {
                        consume();
                    }
                    if (peek().has_value()) {
                        consume();
                    }
                }
                else if (peek().value() == '(') {
                    consume();
                    tokens.push_back({ TokenType::OPEN_PARENTHESIS, line_count });
                }
                else if (peek().value() == ')') {
                    consume();
                    tokens.push_back({ TokenType::CLOSE_PARENTHESIS, line_count });
                }
                else if (peek().value() == ';') {
                    consume();
                    tokens.push_back({ TokenType::SEMICOLON, line_count });
                }
                else if (peek().value() == '=') {
                    consume();
                    tokens.push_back({ TokenType::ASSIGN, line_count });
                }
                else if (peek().value() == '+') {
                    consume();
                    tokens.push_back({ TokenType::PLUS, line_count });
                }
                else if (peek().value() == '*') {
                    consume();
                    tokens.push_back({ TokenType::STAR, line_count });
                }
                else if (peek().value() == '-') {
                    consume();
                    tokens.push_back({ TokenType::MINUS, line_count });
                }
                else if (peek().value() == '/') {
                    consume();
                    tokens.push_back({ TokenType::FSLASH, line_count });
                }
                else if (peek().value() == '{') {
                    consume();
                    tokens.push_back({ TokenType::OPEN_CURLY, line_count });
                }
                else if (peek().value() == '}') {
                    consume();
                    tokens.push_back({ TokenType::CLOSE_CURLY, line_count });
                }
                else if (peek().value() == '"')
                {
                    consume();
                    tokens.push_back({TokenType::QUOTATION, line_count});
                }
                else if (peek().value() == '\n') {
                    consume();
                    line_count++;
                }
                else if (std::isspace(peek().value())) {
                    consume();
                }
                else {
                    std::cerr << "Invalid token" << std::endl;
                    exit(EXIT_FAILURE);
                }
            }
            _index = 0;
            return tokens;
        }

    private:
        [[nodiscard]] std::optional<char> peek(const size_t offset = 0) const
        {
            if (_index + offset >= _src.length()) {
                return {};
            }
            return _src.at(_index + offset);
        }

        char consume()
        {
            return _src.at(_index++);
        }

        bool expect(const std::string& next)
        {
            return _index + next.length() < _src.length() && _src.substr(_index,  next.length()) == next;
        }

        const std::string _src;
        size_t _index = 0;
};
