using ils;

class ILS
{
    const string SOURCE_FILE = "C:\\Projects\\ils.ils\\ils\\test.ils";
    const string OUTPUT_FILE = "C:\\Projects\\ils.ils\\ils\\out.asm";

    static void Main(string[] args)
    {
        if (File.Exists(SOURCE_FILE))
        {
            // Read all lines from the file
            string source = File.ReadAllText(SOURCE_FILE);

            Tokenizer tokenizer = new Tokenizer();
            List<Token> tokens = tokenizer.Tokenize(source);

            int line = 1;
            foreach (Token token in tokens)
            {
                Console.Write(token.tokenType.ToString() + " ");
                if(token.line != line)
                {
                    line = token.line;
                    Console.Write("\n");
                }
            }
        }
        else
        {
            Console.WriteLine("File not found.");
        }
    }
}