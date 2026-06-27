using TextArchive;

if (args.Length < 2)
{
    PrintUsage();
    return 1;
}

string mode = args[0].ToLower();

if (mode is "-c" or "--create")
{
    if (args.Length < 3) { Console.Error.WriteLine("Usage: texttar -c <source-folder> <output-file>"); return 1; }
    return Archiver.Create(sourceFolder: args[1], outputFile: args[2]);
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

static void PrintUsage()
{
    Console.WriteLine("texttar — length-prefixed text archive tool");
    Console.WriteLine();
    Console.WriteLine("  Create:  texttar -c <source-folder> <output-file>");
    Console.WriteLine("  Extract: texttar -x <archive-file>  <output-folder>");
}
