using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils
{
    public class Verificator
    {
        public void Verify(ASTScope mainScope)
        {
            VariableDuplicates(mainScope);
        }

        /// <summary>
        /// Checks if there are no variable duplicates
        /// </summary>
        public bool VariableDuplicates(ASTScope scope)
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
    }
}
