#pragma once

#include <sstream>

namespace VARIABLES
{

    class Variable
    {
        public:
            std::string name;
            Variable(const std::string& name) : name(name) {}
    };

    class IntVariable : public Variable
    {
        public:
            size_t stackLocation;
            IntVariable(const std::string& name, size_t stackLoc) : Variable(name) { stackLocation = stackLoc; }
    };

    class StrVariable : public Variable
    {
        public:
            std::string text;
            StrVariable(const std::string& name, std::string txt) : Variable(name) { text = txt; }
    };

    class BoolVariable : public Variable
    {
        public:
            bool value;
            BoolVariable(const std::string& name, bool val) : Variable(name) { value = val; }
    };

    class CharVariable : public Variable
    {
        public:
            char character;
            CharVariable(const std::string& name, std::string c) : Variable(name) { character = c[0]; }
    };

}