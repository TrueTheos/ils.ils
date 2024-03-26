#pragma once

#include <cassert>
#include <variant>

#include "arena.hpp"
#include "tokenization.hpp"

struct NodeTermIntLit {
    Token int_lit;
};

struct NodeTermStrLit {
    Token str_lit;
};

struct NodeTermCharLit {
    Token char_lit;
};

struct NodeTermBoolLit {
    Token bool_lit;
};

struct NodeTermIdent {
    Token ident;
};

struct NodeExpr;

struct NodeTermParen {
    NodeExpr* expr;
};

struct NodeBinExprAdd {
    NodeExpr* lhs;
    NodeExpr* rhs;
};

struct NodeBinExprMulti {
    NodeExpr* lhs;
    NodeExpr* rhs;
};

struct NodeBinExprSub {
    NodeExpr* lhs;
    NodeExpr* rhs;
};

struct NodeBinExprDiv {
    NodeExpr* lhs;
    NodeExpr* rhs;
};

struct NodeBinExpr {
    std::variant<NodeBinExprAdd*, NodeBinExprMulti*, NodeBinExprSub*, NodeBinExprDiv*> var;
};

struct NodeTerm {
    std::variant<NodeTermIntLit*, NodeTermStrLit*, NodeTermIdent*, NodeTermCharLit*, NodeTermBoolLit*, NodeTermParen*> var;
};

struct NodeExpr {
    std::variant<NodeTerm*, NodeBinExpr*> var; 
};

struct NodeStmtExit {
    NodeExpr* expr;
};

struct NodeStmtPrint {
    std::variant<NodeExpr*> var;    
};

struct NodeVarDeclare {
    Token ident;
    Token type;
    std::optional<NodeExpr*> var {};
};

struct NodeStmt;

struct NodeScope {
    std::vector<NodeStmt*> stmts;
};

struct NodeIfPred;

struct NodeIfPredElif {
    NodeExpr* expr {};
    NodeScope* scope {};
    std::optional<NodeIfPred*> pred;
};

struct NodeIfPredElse {
    NodeScope* scope;
};

struct NodeIfPred {
    std::variant<NodeIfPredElif*, NodeIfPredElse*> var;
};

struct NodeStmtIf {
    NodeExpr* expr {};
    NodeScope* scope {};
    std::optional<NodeIfPred*> pred;
};

struct NodeStmtAssign {
    Token ident;
    NodeExpr* expr {};
};

struct NodeStmt {
    std::variant<NodeStmtExit*, NodeVarDeclare*, NodeScope*, NodeStmtIf*, NodeStmtAssign*, NodeStmtPrint*> var;
};

struct NodeProg {
    std::vector<NodeStmt*> stmts;
};

class Parser 
{
    public:
        explicit Parser(std::vector<Token> tokens)
            : m_tokens(std::move(tokens))
            , m_allocator(1024 * 1024 * 4) // 4 mb
        {
        }

        void error_expected(const std::string& msg) const
        {
            std::cerr << "[" << peek(-1).value().line << "] " << "Expected " << msg << std::endl;
            exit(EXIT_FAILURE);
        }

        std::optional<NodeTerm*> parse_term() // NOLINT(*-no-recursion)
        {
            if (auto int_lit = try_consume(TokenType::INT_LITERAL)) 
            {
                auto term_int_lit = m_allocator.emplace<NodeTermIntLit>(int_lit.value());
                auto term = m_allocator.emplace<NodeTerm>(term_int_lit);
                return term;
            }
            if(try_consume(TokenType::QUOTATION))
            {
                if(auto str_lit = try_consume(TokenType::STR_LITERAL))
                {
                    if(try_consume(TokenType::QUOTATION))
                    {
                        auto term_str_lit = m_allocator.emplace<NodeTermStrLit>(str_lit.value());
                        auto term = m_allocator.emplace<NodeTerm>(term_str_lit);
                        return term;
                    }
                }
            }
            if(try_consume(TokenType::SINGLE_QUOTATION))
            {
                if(auto char_lit = try_consume(TokenType::CHAR_LITERAL))
                {
                    if(try_consume(TokenType::SINGLE_QUOTATION))
                    {
                        auto term_char_lit = m_allocator.emplace<NodeTermCharLit>(char_lit.value());
                        auto term = m_allocator.emplace<NodeTerm>(term_char_lit);
                        return term;
                    }
                }
            }
            if(auto bool_lit = try_consume(TokenType::TRUE))
            {
                auto term_bool_lit = m_allocator.emplace<NodeTermBoolLit>(bool_lit.value());
                auto term = m_allocator.emplace<NodeTerm>(term_bool_lit);
                return term;
            }
            else if(auto bool_lit = try_consume(TokenType::FALSE))
            {
                auto term_bool_lit = m_allocator.emplace<NodeTermBoolLit>(bool_lit.value());
                auto term = m_allocator.emplace<NodeTerm>(term_bool_lit);
                return term;
            }
            if (auto ident = try_consume(TokenType::IDENTIFIER)) 
            {
                auto expr_ident = m_allocator.emplace<NodeTermIdent>(ident.value());
                auto term = m_allocator.emplace<NodeTerm>(expr_ident);
                return term;
            }     
            if (const auto open_paren = try_consume(TokenType::OPEN_PARENTHESIS)) 
            {
                auto expr = parse_expr();
                if (!expr.has_value()) {
                    error_expected("expression");
                }
                try_consume_err(TokenType::CLOSE_PARENTHESIS);
                auto term_paren = m_allocator.emplace<NodeTermParen>(expr.value());
                auto term = m_allocator.emplace<NodeTerm>(term_paren);
                return term;
            }
            return {};
        }

        std::optional<NodeExpr*> parse_expr(const int min_prec = 0) // NOLINT(*-no-recursion)
        {
            std::optional<NodeTerm*> term_lhs = parse_term();
            if (!term_lhs.has_value()) {
                return {};
            }
            auto expr_lhs = m_allocator.emplace<NodeExpr>(term_lhs.value());

            while (true) {
                std::optional<Token> curr_tok = peek();
                std::optional<int> prec;
                if (curr_tok.has_value()) {
                    prec = bin_prec(curr_tok->type);
                    if (!prec.has_value() || prec < min_prec) {
                        break;
                    }
                }
                else {
                    break;
                }
                const auto [type, line, value] = consume();
                const int next_min_prec = prec.value() + 1;
                auto expr_rhs = parse_expr(next_min_prec);
                if (!expr_rhs.has_value()) {
                    error_expected("expression");
                }
                auto expr = m_allocator.emplace<NodeBinExpr>();
                auto expr_lhs2 = m_allocator.emplace<NodeExpr>();
                if (type == TokenType::PLUS) {
                    expr_lhs2->var = expr_lhs->var;
                    auto add = m_allocator.emplace<NodeBinExprAdd>(expr_lhs2, expr_rhs.value());
                    expr->var = add;
                }
                else if (type == TokenType::STAR) {
                    expr_lhs2->var = expr_lhs->var;
                    auto multi = m_allocator.emplace<NodeBinExprMulti>(expr_lhs2, expr_rhs.value());
                    expr->var = multi;
                }
                else if (type == TokenType::MINUS) {
                    expr_lhs2->var = expr_lhs->var;
                    auto sub = m_allocator.emplace<NodeBinExprSub>(expr_lhs2, expr_rhs.value());
                    expr->var = sub;
                }
                else if (type == TokenType::FSLASH) {
                    expr_lhs2->var = expr_lhs->var;
                    auto div = m_allocator.emplace<NodeBinExprDiv>(expr_lhs2, expr_rhs.value());
                    expr->var = div;
                }
                else {
                    assert(false); // Unreachable;
                }
                expr_lhs->var = expr;
            }
            return expr_lhs;
        }

        std::optional<NodeScope*> parse_scope() // NOLINT(*-no-recursion)
        {
            if (!try_consume(TokenType::OPEN_CURLY).has_value()) {
                return {};
            }
            auto scope = m_allocator.emplace<NodeScope>();
            while (auto stmt = parse_stmt()) {
                scope->stmts.push_back(stmt.value());
            }
            try_consume_err(TokenType::CLOSE_CURLY);
            return scope;
        }

        
        std::optional<NodeIfPred*> parse_if_pred() // NOLINT(*-no-recursion)
        {
            if (try_consume(TokenType::ELIF)) {
                try_consume_err(TokenType::OPEN_PARENTHESIS);
                const auto elif = m_allocator.alloc<NodeIfPredElif>();
                if (const auto expr = parse_expr()) {
                    elif->expr = expr.value();
                }
                else {
                    error_expected("expression");
                }
                try_consume_err(TokenType::CLOSE_PARENTHESIS);
                if (const auto scope = parse_scope()) {
                    elif->scope = scope.value();
                }
                else {
                    error_expected("scope");
                }
                elif->pred = parse_if_pred();
                auto pred = m_allocator.emplace<NodeIfPred>(elif);
                return pred;
            }
            if (try_consume(TokenType::ELSE_)) {
                auto else_ = m_allocator.alloc<NodeIfPredElse>();
                if (const auto scope = parse_scope()) {
                    else_->scope = scope.value();
                }
                else {
                    error_expected("scope");
                }
                auto pred = m_allocator.emplace<NodeIfPred>(else_);
                return pred;
            }
            return {};
        }

        std::optional<NodeStmt*> parse_stmt() // NOLINT(*-no-recursion)
        {
            if (expect(TokenType::EXIT) && expect(TokenType::OPEN_PARENTHESIS, 1)) {
                consume();
                consume();
                auto stmt_exit = m_allocator.emplace<NodeStmtExit>();
                if (const auto node_expr = parse_expr()) {
                    stmt_exit->expr = node_expr.value();
                }
                else {
                    error_expected("expression");
                }
                try_consume_err(TokenType::CLOSE_PARENTHESIS);
                try_consume_err(TokenType::SEMICOLON);
                auto stmt = m_allocator.emplace<NodeStmt>();
                stmt->var = stmt_exit;
                return stmt;
            }
            if (expect(TokenType::PRINT) && expect(TokenType::OPEN_PARENTHESIS, 1)) {
                consume();
                consume();
                auto stmt_print = m_allocator.emplace<NodeStmtPrint>();

                if(expect(TokenType::QUOTATION))
                {
                    consume(); 

                    if (const auto node_strlit = parse_expr()) {
                        stmt_print->var = node_strlit.value();
                    }
                    else {
                        error_expected("str_literal");
                    }
                }
                else
                {
                    if (const auto node_expr = parse_expr()) {
                        stmt_print->var = node_expr.value();
                    }
                    else {
                        error_expected("expression");
                    }
                }
                try_consume_err(TokenType::CLOSE_PARENTHESIS);
                try_consume_err(TokenType::SEMICOLON);
                auto stmt = m_allocator.emplace<NodeStmt>();
                stmt->var = stmt_print;
                return stmt;
            }
            if(expect(TokenType::IDENTIFIER))
            {
                if(expect(TokenType::COLON, 1))
                {
                    Token ident = consume();
                    consume();

                    Token varType = consume();

                    switch (varType.type)
                    {
                        case TokenType::STR:
                            break;
                        case TokenType::INT:
                            break;
                        case TokenType::BOOL:
                            break;
                        case TokenType::CHAR:
                            break;
                    
                        default:
                            error_expected("variable type");
                    }
                    
                    NodeExpr* var;

                    if(try_consume(TokenType::ASSIGN))
                    {                        
                        if (const auto expr = parse_expr()) 
                        {
                            var = expr.value();
                        }
                        else 
                        {
                            switch (varType.type)
                            {
                                case TokenType::STR:
                                    error_expected("string");
                                    break;
                                case TokenType::INT:
                                    error_expected("int literal or expression");
                                    break;
                                case TokenType::BOOL:
                                    error_expected("bool");
                                    break;
                                case TokenType::CHAR:
                                    error_expected("char");
                                    break;
                                default:
                                    error_expected("value");
                            }
                        }

                        try_consume_err(TokenType::SEMICOLON);
                    }
                    else
                    {
                        try_consume_err(TokenType::SEMICOLON);
                    }

                    auto vardec = m_allocator.emplace<NodeVarDeclare>(ident, varType, var);
                    auto stmt = m_allocator.emplace<NodeStmt>(vardec);
                    return stmt;                   
                }

                if (expect(TokenType::ASSIGN, 1)) 
                {
                    const auto assign = m_allocator.alloc<NodeStmtAssign>();
                    assign->ident = consume();
                    consume();
                    if (const auto expr = parse_expr()) {
                        assign->expr = expr.value();
                    }
                    else {
                        error_expected("expression");
                    }
                    try_consume_err(TokenType::SEMICOLON);
                    auto stmt = m_allocator.emplace<NodeStmt>(assign);
                    return stmt;
                }
            }     

        
            if (expect(TokenType::OPEN_CURLY)) {
                if (auto scope = parse_scope()) {
                    auto stmt = m_allocator.emplace<NodeStmt>(scope.value());
                    return stmt;
                }
                error_expected("scope");
            }
            if (auto if_ = try_consume(TokenType::IF_)) {
                try_consume_err(TokenType::OPEN_PARENTHESIS);
                auto stmt_if = m_allocator.emplace<NodeStmtIf>();
                if (const auto expr = parse_expr()) {
                    stmt_if->expr = expr.value();
                }
                else {
                    error_expected("expression");
                }
                try_consume_err(TokenType::CLOSE_PARENTHESIS);
                if (const auto scope = parse_scope()) {
                    stmt_if->scope = scope.value();
                }
                else {
                    error_expected("scope");
                }
                stmt_if->pred = parse_if_pred();
                auto stmt = m_allocator.emplace<NodeStmt>(stmt_if);
                return stmt;
            }
            return {};
        }

        std::optional<NodeProg> parse_prog()
        {
            NodeProg prog;
            while (peek().has_value()) {
                if (auto stmt = parse_stmt()) {
                    prog.stmts.push_back(stmt.value());
                }
                else {
                    error_expected("statement");
                }
            }
            return prog;
        }

    private:
        [[nodiscard]] std::optional<Token> peek(const int offset = 0) const
        {
            if (m_index + offset >= m_tokens.size()) {
                return {};
            }
            return m_tokens.at(m_index + offset);
        }

        Token consume()
        {
            return m_tokens.at(m_index++);
        }

        [[nodiscard]] bool expect(TokenType type, int offset = 0) const
        {
            return peek(offset).has_value() && peek(offset).value().type == type;
        }

        Token try_consume_err(const TokenType type)
        {
            if (expect(type)) {
                return consume();
            }
            error_expected(to_string(type));
            return {};
        }

        std::optional<Token> try_consume(const TokenType type)
        {
            if (expect(type)) {
                return consume();
            }
            return {};
        }

        const std::vector<Token> m_tokens;
        size_t m_index = 0;
        ArenaAllocator m_allocator;
};