using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils
{    public class ErrorHandler
    {
        private static void Log(string message)
        {
            Console.WriteLine($"\n{message}");
            Environment.Exit(-1);
        }

        public static void Custom(string message)
        {
            Log(message);
        }

        public static void Throw(CustomError error) 
        {
            Log(error.Throw());
        }
    }

    public abstract class CustomError
    {
        public int id;
        public string name;
        public int line;

        public CustomError(int _id, string _name, int _line)
        {
            id = _id;
            name = _name;
            line = _line;
        }

        public abstract string Throw();
    }

    public class ExpectedError : CustomError
    {
        private string _expected;
        private Token _received;

        public ExpectedError(string expected, Token received, int line) : base(1, "ExpectedError", line)
        {
            _expected = expected;
            _received = received;
        }
        public override string Throw()
        {
            return $"[{line}] {name}: expected '{_expected}' but received '{_received.value}'";
        }
    }
}
