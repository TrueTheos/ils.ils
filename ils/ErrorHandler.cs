using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils
{
    public class CustomException : Exception
    {
        public CustomException(string message) : base(message) { }
    }

    public class ErrorHandler
    {
        public static void Custom(string message)
        {
            throw new CustomException(message);
        }

        public static void Expected(string message, int line) 
        {
            throw new CustomException($"[{line}] Expected '{message}'!");
        }
    }
}
