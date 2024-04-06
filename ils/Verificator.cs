using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ils.IRGenerator;

namespace ils
{
    public class Verificator
    {
        public static readonly List<string> RESERVED_KEYWORDS = ["WHILE", "str", "char", "int", "bool", "while", "break", "else", "elif", "if", "true", "false"];

        public void Verify(ASTScope mainScope)
        {
            //CheckKeywordUsage(mainScope);
        }

        /// <summary>
        /// Checks if any variable is named after a keyword.
        /// </summary>
        public static void CheckKeywordUsage(List<(string variableName, int line)> variables)
        {
            foreach (var variable in variables)
            {
                if (RESERVED_KEYWORDS.Contains(variable.variableName))
                {
                    ErrorHandler.Custom($"[{variable.line}] Identifier expected. {variable.variableName} is a keyword!");
                }
            }
        }

        public List<T> GetASTNodesOfType<T>(List<ASTStatement> nodes) where T : ASTStatement
        {
            return nodes.OfType<T>().ToList();
        }

        /*
        /// <summary>
        /// Checks if there are no variable duplicates
        /// </summary>
        public bool ScopeVariableDuplicates(ASTScope scope)
        {
            List<ASTVariableDeclaration> variables = scope.GetStatementsOfType<ASTVariableDeclaration>();
            foreach(ASTScope childScope in scope.GetChildScopes())
            {              
                variables.AddRange(childScope.GetStatementsOfType<ASTVariableDeclaration>());
            }

            Dictionary<string, Token> uniqueIdentifiers = new Dictionary<string, Token>();

            foreach (var variable in variables)
            {
                string name = variable.name.value;
                if (uniqueIdentifiers.ContainsKey(name))
                {
                    ErrorHandler.Custom($"[{variable.name.line}] Variable '{name}' already exists! Line {uniqueIdentifiers[name].line}.");
                    return false; // Found a duplicate name
                }
                else
                {
                    uniqueIdentifiers.Add(name, variable.name);
                }
            }

            return true;
        }
        */
    }
}
