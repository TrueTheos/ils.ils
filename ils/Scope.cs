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
        public HashSet<string> AllVariables => GetVariables();
        public HashSet<string> LocalVariables = new();
        public ScopeType scopeType;

        public Scope(int _id, ScopeType _scopeType)
        {
            Id = _id;
            scopeType = _scopeType;
        }

        public void SetParent(Scope _parent)
        {
            Parent = _parent;

            _parent.Children.Add(Id, this);
        }

        public HashSet<string> GetVariables()
        {
            var res = LocalVariables;

            if(Parent != null)
            {
                res.UnionWith(Parent.GetVariables());
            }

            return res;
        }

        public BaseVariable GetVariable(Token name)
        {
            VariableExistsErr(name);
            return IRGenerator.GetVariable(name.Value);
        }

        public bool VariableExists(string name)
        {
            return (AllVariables.Contains(name)) ||
                   (IRGenerator.GlobalVariables.ContainsKey(name) && IRGenerator.GlobalVariables[name] != null);
        }

        public void VariableExistsErr(Token name)
        {
            if (!VariableExists(name.Value)) ErrorHandler.Throw(new VariableDoesntExistError(name.Value, name.Line));
        }

        public void AddLocalVariable(BaseVariable var)
        {
            LocalVariables.Add(var.guid);
        }
    }
}
