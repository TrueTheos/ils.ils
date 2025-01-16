using ils.IR.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils
{
    public class Scope
    {
        public int Id;
        public Scope Parent = null;
        public Dictionary<int, Scope> Children = new();
        public Dictionary<string, BaseVariable> AllVariables = new();
        public Dictionary<string, BaseVariable> LocalVariables = new();
        public ScopeType scopeType;

        public Scope(int _id, ScopeType _scopeType)
        {
            Id = _id;
            scopeType = _scopeType;
        }

        public void SetParent(Scope _parent)
        {
            Parent = _parent;
            foreach (var var in _parent.AllVariables)
                AllVariables.Add(var.Key, var.Value);

            _parent.Children.Add(Id, this);
        }

        public BaseVariable GetVariable(Token name)
        {
            VariableExistsErr(name);
            return AllVariables[name.Value];
        }

        public bool VariableExists(string name)
        {
            return (AllVariables.ContainsKey(name) && AllVariables[name] != null) ||
                   (IRGenerator.GlobalVariables.ContainsKey(name) && IRGenerator.GlobalVariables[name] != null);
        }

        public void VariableExistsErr(Token name)
        {
            if (!VariableExists(name.Value)) ErrorHandler.Throw(new VariableDoesntExistError(name.Value, name.Line));
        }

        public void AddLocalVariable(BaseVariable var)
        {
            LocalVariables.Add(var.VarName, var);
            AllVariables.Add(var.VarName, var);
        }
    }
}
