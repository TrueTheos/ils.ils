#include <fstream>
#include <iostream>
#include <optional>
#include <sstream>
#include <vector>
#include <cstring>

#include "generation.hpp"

int main(int argc, char* argv[])
{
    std::string outputFile = "out";
    std::string inputFile = "test.ils";
    bool debug = true;

    if (inputFile.empty()) {
        std::cerr << "\u001B[31m \033[1m Error:\u001B[37m \033[0m Input file not provided." << std::endl; 
        exit(EXIT_FAILURE);
    }

    std::string contents;
    {
        std::stringstream contents_stream;
        std::fstream input(inputFile, std::ios::in);
        contents_stream << input.rdbuf();
        contents = contents_stream.str();
    }

    Tokenizer tokenizer(std::move(contents));
    std::vector<Token> tokens = tokenizer.tokenize();

    Parser parser(std::move(tokens));
    std::optional<NodeProg> prog = parser.parse_prog();

    if (!prog.has_value()) {
        std::cerr << "\u001B[31m \033[1m Error:\u001B[37m \033[0m Invalid program" << std::endl;
        exit(EXIT_FAILURE);
    }

    {
        Generator generator(prog.value());
        std::fstream file("out.asm", std::ios::out);
        file << generator.gen_prog();
    }

    std::string ldCmd = "ld out.o -o ";
    ldCmd.append(outputFile);

    system("nasm -felf64 out.asm");
    system(ldCmd.c_str());

    if (!debug) {
        system("rm out.asm");
        system("rm out.o");
    }

    return EXIT_SUCCESS;
}
