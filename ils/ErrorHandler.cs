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

        public static void Expected(string message, Token token) 
        {
            Log($"[{token.line}] Expected '{message}' but received {token.tokenType}!");
        }
    }
}
