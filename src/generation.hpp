#pragma once

#include <algorithm>
#include <cassert>

#include "parser.hpp"
#include "variables.hpp"

class Generator {
public:
    explicit Generator(NodeProg prog)
        : _prog(std::move(prog))
    {
    }

    void genTerm(const NodeTerm* term)
    {
        struct TermVisitor {
            Generator& gen;

            void operator()(const NodeTermIntLit* term_int_lit) const
            {
                gen._output << "    mov rax, " << term_int_lit->int_lit.value.value() << "\n";
                gen.push("rax");
            }

            void operator()(const NodeTermStrLit* term_str_lit) const
            {
                int size = gen._strLiterals.size();
                std::string litName = "strlit" + std::to_string(size);
                gen._strLiterals.push_back(VARIABLES::StrVariable(litName,term_str_lit->str_lit.value.value()));            
            }

            void operator()(const NodeTermCharLit* term_char_lit) const
            {
                int size = gen._charLiterals.size();
                std::string litName = "charlit" + std::to_string(size);
                gen._charLiterals.push_back(VARIABLES::CharVariable(litName, term_char_lit->char_lit.value.value()));            
            }

            void operator()(const NodeTermBoolLit* term_bool_lit) const
            {
                int size = gen._boolLiteral.size();
                std::string litName = "boolLit" + std::to_string(size);
                if(term_bool_lit->bool_lit.type == TokenType::TRUE)
                {
                    gen._boolLiteral.push_back(VARIABLES::BoolVariable(litName, true ));          
                }
                else if(term_bool_lit->bool_lit.type == TokenType::FALSE)
                {
                    gen._boolLiteral.push_back(VARIABLES::BoolVariable(litName, false ));         
                }
                else
                {
                    std::cerr << "[" << term_bool_lit->bool_lit.line << "] " << "Unknown bool value: " << term_bool_lit->bool_lit.value.value() << std::endl;
                    exit(EXIT_FAILURE);
                }
            }

            void operator()(const NodeTermIdent* term_ident) const
            {
                TokenType variableType;

                const auto it = std::ranges::find_if(std::as_const(gen._intLiterals), [&](const VARIABLES::IntVariable& var) {
                    return var.name == term_ident->ident.value.value();
                });
                if (it == gen._intLiterals.cend()) {
                    std::cerr << "[" << term_ident->ident.line << "] " << "Undeclared identifier: " << term_ident->ident.value.value() << std::endl;
                    exit(EXIT_FAILURE);
                }
                std::stringstream offset;
                offset << "QWORD [rsp + " << (gen.m_stack_size - it->stackLocation - 1) * 8 << "]";
                gen.push(offset.str());
            }

            void operator()(const NodeTermParen* term_paren) const
            {
                gen.genExpr(term_paren->expr);
            }    
        };
        TermVisitor visitor({ .gen = *this });
        std::visit(visitor, term->var);
    }

    void genBinExpr(const NodeBinExpr* bin_expr)
    {
        struct BinExprVisitor {
            Generator& gen;

            void operator()(const NodeBinExprSub* sub) const
            {
                gen.genExpr(sub->rhs);
                gen.genExpr(sub->lhs);
                gen.pop("rax");
                gen.pop("rbx");
                gen._output << "    sub rax, rbx\n";
                gen.push("rax");
            }

            void operator()(const NodeBinExprAdd* add) const
            {
                gen.genExpr(add->rhs);
                gen.genExpr(add->lhs);
                gen.pop("rax");
                gen.pop("rbx");
                gen._output << "    add rax, rbx\n";
                gen.push("rax");
            }

            void operator()(const NodeBinExprMulti* multi) const
            {
                gen.genExpr(multi->rhs);
                gen.genExpr(multi->lhs);
                gen.pop("rax");
                gen.pop("rbx");
                gen._output << "    mul rbx\n";
                gen.push("rax");
            }

            void operator()(const NodeBinExprDiv* div) const
            {
                gen.genExpr(div->rhs);
                gen.genExpr(div->lhs);
                gen.pop("rax");
                gen.pop("rbx");
                gen._output << "    div rbx\n";
                gen.push("rax");
            }
        };

        BinExprVisitor visitor { .gen = *this };
        std::visit(visitor, bin_expr->var);
    }

    void genExpr(const NodeExpr* expr)
    {
        struct ExprVisitor {
            Generator& gen;

            void operator()(const NodeTerm* term) const
            {
                gen.genTerm(term);
            }

            void operator()(const NodeBinExpr* bin_expr) const
            {
                gen.genBinExpr(bin_expr);
            }
        };

        ExprVisitor visitor { .gen = *this };
        std::visit(visitor, expr->var);
    }

    void genPrintArg(const NodeStmtPrint* prt)
    {
        struct PrintArgVisitor{
            Generator& gen;

            void operator()(const NodeExpr* term) const
            {
                gen.genExpr(term);

                //todo
                
            }

            /*
                gen.pop("rax");
                gen.m_output << "    call print_number\n";
            */

            /*
                gen.m_output << "    load rdi, litName\n";
                gen.m_output << "    mov rax, length\n";
                gen.m_output << "    call print_string\n";
                gen.m_output << "\n";  
                gen.m_output << "    length: equ $-litName\n";
            */
        };
       
        PrintArgVisitor visitor { .gen = *this };
        std::visit(visitor, prt->var);
    }

    void gen_scope(const NodeScope* scope)
    {
        beginScope();
        for (const NodeStmt* stmt : scope->stmts) {
            genStmt(stmt);
        }
        endScope();
    }

    void genIfPred(const NodeIfPred* pred, const std::string& end_label)
    {
        struct PredVisitor {
            Generator& gen;
            const std::string& end_label;

            void operator()(const NodeIfPredElif* elif) const
            {
                gen._output << "    ;; elif\n";
                gen.genExpr(elif->expr);
                gen.pop("rax");
                const std::string label = gen.createLabel();
                gen._output << "    test rax, rax\n";
                gen._output << "    jz " << label << "\n";
                gen.gen_scope(elif->scope);
                gen._output << "    jmp " << end_label << "\n";
                if (elif->pred.has_value()) {
                    gen._output << label << ":\n";
                    gen.genIfPred(elif->pred.value(), end_label);
                }
            }

            void operator()(const NodeIfPredElse* else_) const
            {
                gen._output << "    ;; else\n";
                gen.gen_scope(else_->scope);
            }
        };

        PredVisitor visitor { .gen = *this, .end_label = end_label };
        std::visit(visitor, pred->var);
    }

    void genStmt(const NodeStmt* stmt)
    {
        struct StmtVisitor {
            Generator& gen;

            void operator()(const NodeStmtExit* stmt_exit) const
            {
                gen._output << "    ; exit\n";
                gen.genExpr(stmt_exit->expr);
                gen._output << "    mov rax, 60\n";
                gen.pop("rdi");
                gen._output << "    syscall\n";
                gen._output << "\n";
            }

            void operator()(const NodeVarDeclare* stmt_vardeclare) const
            {
                gen._output << "    ; variable " << stmt_vardeclare->ident.value.value() << "\n";
                if (std::ranges::find_if(std::as_const(gen._intLiterals), 
                    [&](const VARIABLES::IntVariable& var) 
                    { 
                        return var.name == stmt_vardeclare->ident.value.value(); 
                    }) != gen._intLiterals.cend()) 
                {
                    std::cerr << "[" << stmt_vardeclare->ident.line << "] " << "Variable already exists: " << stmt_vardeclare->ident.value.value() << std::endl;
                    exit(EXIT_FAILURE);
                }
                gen._intLiterals.emplace_back(VARIABLES::IntVariable(stmt_vardeclare->ident.value.value(), gen.m_stack_size));
                gen.genExpr(stmt_vardeclare->var.value());
                gen._output << "\n";
            }

            void operator()(const NodeStmtPrint* stmt_print) const
            {
                gen._output << "    ; print\n"; 

                gen.genPrintArg(stmt_print);
                
                //gen.m_output << "    mov rax, 12345\n";
                //gen.m_output << "    call print_number\n";
                /*gen.m_output << "    mov rdi, message\n";
                gen.m_output << "    mov rax, length\n";
                gen.m_output << "    call print_string\n";
                gen.m_output << "\n";  
                gen.m_output << "    message: dd 'test',10\n"; 
                gen.m_output << "    length: equ $-message\n";*/
            }

            /*void operator()(const NodeStmtLet* stmt_let) const
            {
                gen.m_output << "    ; let " << stmt_let->ident.value.value() << "\n";
                if (std::ranges::find_if(
                        std::as_const(gen.m_vars),
                        [&](const Var& var) { return var.name == stmt_let->ident.value.value(); })
                    != gen.m_vars.cend()) {
                    std::cerr << "Identifier already used: " << stmt_let->ident.value.value() << std::endl;
                    exit(EXIT_FAILURE);
                }
                gen.m_vars.push_back({ .name = stmt_let->ident.value.value(), .stack_loc = gen.m_stack_size });
                gen.gen_expr(stmt_let->expr);
                gen.m_output << "\n";
            }*/

            void operator()(const NodeStmtAssign* stmt_assign) const
            {
                const auto it = std::ranges::find_if(gen._intLiterals, [&](const VARIABLES::IntVariable& var) {
                    return var.name == stmt_assign->ident.value.value();
                });
                if (it == gen._intLiterals.end()) {
                    std::cerr << "[" << stmt_assign->ident.line << "] " <<"Undeclared identifier: " << stmt_assign->ident.value.value() << std::endl;
                    exit(EXIT_FAILURE);
                }
                gen.genExpr(stmt_assign->expr);
                gen.pop("rax");
                gen._output << "    mov [rsp + " << (gen.m_stack_size - it->stackLocation - 1) * 8 << "], rax\n";
            }

            void operator()(const NodeScope* scope) const
            {
                gen._output << "    ;; scope\n";
                gen.gen_scope(scope);
                gen._output << "    ;; /scope\n";
            }

            void operator()(const NodeWhile* stmt_while) const
            {
                gen._output << "    ;; if\n";
                gen.genExpr(stmt_while->expr);
                gen.pop("rax");

                const std::string whileStartLabel = gen.createLabel();
                gen._output << whileStartLabel << ":\n";
                gen._output << "    test rax, rax\n";
                const std::string whileEndLabel = gen.createLabel();
                gen._output << "    jz " << whileEndLabel << "\n";
                gen.gen_scope(stmt_while->scope);
                gen._output << "    jmp " << whileStartLabel << "\n";        
                gen._output << whileEndLabel << ":\n";
            }

            void operator()(const NodeStmtIf* stmt_if) const
            {
                gen._output << "    ;; if\n";
                gen.genExpr(stmt_if->expr);
                gen.pop("rax");
                const std::string label = gen.createLabel();
                gen._output << "    test rax, rax\n";
                gen._output << "    jz " << label << "\n";
                gen.gen_scope(stmt_if->scope);
                if (stmt_if->pred.has_value()) {
                    const std::string end_label = gen.createLabel();
                    gen._output << "    jmp " << end_label << "\n";
                    gen._output << label << ":\n";
                    gen.genIfPred(stmt_if->pred.value(), end_label);
                    gen._output << end_label << ":\n";
                }
                else {
                    gen._output << label << ":\n";
                }
                gen._output << "    ;; /if\n";
            }
        };

        StmtVisitor visitor { .gen = *this };
        std::visit(visitor, stmt->var);
    }

    void genDataSection()
    {
        _output << "\nsection .data\n";
        for(auto &stl : _strLiterals)
        {
            _output << "    " << stl.name << ": db \"" << stl.text << "\", 0\n";
        }

        for(auto &crl : _charLiterals)
        {
            _output << "    " << crl.name << ": db '" << crl.character << "'\n";
        }

        for(auto &bol : _boolLiteral)
        {
            if(bol.value)
            {
                _output << "    " << bol.name << ": db " << 1 << "\n";
            }
            else
            {
                _output << "    " << bol.name << ": db " << 1 << "\n";
            }
        }
    }

    [[nodiscard]] std::string genProg()
    {
        _output << "global _start\n_start:\n";

        for (const NodeStmt* stmt : _prog.stmts) 
        {
            genStmt(stmt);
        }

        _output << "    mov rax, 60\n";
        _output << "    mov rdi, 69\n";
        _output << "    syscall\n";

        _output << "print_number:\n";
        _output << "    push rax\n";
        _output << "    mov rcx, 10\n";
        _output << "    mov rbx, rsp\n";
        _output << "    add rbx, 20\n";
        _output << "    mov byte [rbx], 0\n";
        _output << ".convert_loop:\n";
        _output << "    dec rbx\n";
        _output << "    xor rdx, rdx\n";
        _output << "    div rcx\n";
        _output << "    add dl, '0'\n";
        _output << "    mov [rbx], dl\n";
        _output << "    test rax, rax\n";
        _output << "    jnz .convert_loop\n";
        _output << "    mov rsi, rbx\n";
        _output << "    mov rdx, 21\n";
        _output << "    mov rax, 1\n";
        _output << "    mov rdi, 1\n";
        _output << "    syscall\n";
        _output << "    pop rax\n";
        _output << "    ret\n";

        _output << "print_string:\n";
        _output << ".print:\n";
        _output << "    mov rsi, rdi\n";
        _output << "    mov rdx, rax\n";
        _output << "    mov rax, 1\n";
        _output << "    mov rdi, 1\n";
        _output << "    syscall\n";
        _output << "    ret\n";

        genDataSection();

        return _output.str();
    }

private:
    void push(const std::string& reg)
    {
        _output << "    push " << reg << "\n";
        m_stack_size++;
    }

    void pop(const std::string& reg)
    {
        _output << "    pop " << reg << "\n";
        m_stack_size--;
    }

    void beginScope()
    {
        _scopes.push_back(_intLiterals.size());
    }

    void endScope()
    {
        const size_t pop_count = _intLiterals.size() - _scopes.back();
        if (pop_count != 0) {
            _output << "    add rsp, " << pop_count * 8 << "\n";
        }
        m_stack_size -= pop_count;
        for (size_t i = 0; i < pop_count; i++) {
            _intLiterals.pop_back();
        }
        _scopes.pop_back();
    }

    std::string createLabel()
    {
        std::stringstream ss;
        ss << "label" << _labelCount++;
        return ss.str();
    }

    struct StringLiteral
    {
        std::string name;
        std::string text;
    };

    struct CharLiteral
    {
        std::string name;
        std::string c;
    };

    struct BoolLiteral
    {
        std::string name;
        bool val;
    };

    struct IntLiteral
    {
        std::string name;
        size_t stackLoc;
    };

    const NodeProg _prog;
    std::stringstream _output;
    size_t m_stack_size = 0;
    std::vector<VARIABLES::IntVariable> _intLiterals {};
    std::vector<VARIABLES::StrVariable> _strLiterals {};
    std::vector<VARIABLES::CharVariable> _charLiterals {};
    std::vector<VARIABLES::BoolVariable> _boolLiteral {};
    std::vector<size_t> _scopes {};
    int _labelCount = 0;
};
