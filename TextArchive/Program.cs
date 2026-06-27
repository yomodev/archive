using TextArchive;

if (args.Length < 2)
{
    PrintUsage();
    return 1;
}

string mode = args[0].ToLower();

if (mode is "-c" or "--create")
{
    if (args.Length < 3) { Console.Error.WriteLine("Usage: texttar -c <source-folder> <output-file> [-e <patterns>]"); return 1; }
    var excludePatterns = ParseExclude(args, startIndex: 3);
    return Archiver.Create(sourceFolder: args[1], outputFile: args[2], excludePatterns);
}
else if (mode is "-x" or "--extract")
{
    if (args.Length < 3) { Console.Error.WriteLine("Usage: texttar -x <archive-file> <output-folder>"); return 1; }
    return Archiver.Extract(archiveFile: args[1], outputFolder: args[2]);
}
else
{
    Console.Error.WriteLine($"Unknown mode: {mode}");
    PrintUsage();
    return 1;
}

static IEnumerable<string>? ParseExclude(string[] args, int startIndex)
{
    for (int i = startIndex; i < args.Length - 1; i++)
    {
        if (args[i] is "-e" or "--exclude")
            return args[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries);
    }
    return null;
}

static void PrintUsage()
{
    Console.WriteLine("texttar — length-prefixed text archive tool");
    Console.WriteLine();
    Console.WriteLine("  Create:  texttar -c <source-folder> <output-file> [-e <patterns>]");
    Console.WriteLine("  Extract: texttar -x <archive-file>  <output-folder>");
    Console.WriteLine();
    Console.WriteLine("  -e / --exclude  Semicolon-separated glob patterns to exclude (supports **)");
    Console.WriteLine();
    Console.WriteLine("  Example — skip bin and obj folders:");
    Console.WriteLine("    texttar -c ./src out.texttar -e \"**/bin/**;**/obj/**\"");
}
