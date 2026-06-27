using System.Text;

const string Magic = "TEXTTAR 1.0";
const string End = "END";

if (args.Length < 2)
{
    PrintUsage();
    return 1;
}

string mode = args[0].ToLower();

if (mode is "-c" or "--create")
{
    if (args.Length < 3) { Console.Error.WriteLine("Usage: texttar -c <source-folder> <output-file>"); return 1; }
    return Create(sourceFolder: args[1], outputFile: args[2]);
}
else if (mode is "-x" or "--extract")
{
    if (args.Length < 3) { Console.Error.WriteLine("Usage: texttar -x <archive-file> <output-folder>"); return 1; }
    return Extract(archiveFile: args[1], outputFolder: args[2]);
}
else
{
    Console.Error.WriteLine($"Unknown mode: {mode}");
    PrintUsage();
    return 1;
}

static int Create(string sourceFolder, string outputFile)
{
    if (!Directory.Exists(sourceFolder))
    {
        Console.Error.WriteLine($"Source folder not found: {sourceFolder}");
        return 1;
    }

    using var stream = new FileStream(outputFile, FileMode.Create, FileAccess.Write);

    WriteLine(stream, Magic);

    int count = 0;
    foreach (var file in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories))
    {
        var relativePath = Path.GetRelativePath(sourceFolder, file).Replace('\\', '/');
        var text = File.ReadAllText(file);
        var normalized = text.ReplaceLineEndings("\n");
        var bytes = Encoding.UTF8.GetBytes(normalized);

        WriteLine(stream, $"FILE {relativePath}");
        WriteLine(stream, $"LENGTH {bytes.Length}");
        stream.Write(bytes);
        count++;

        Console.WriteLine($"  + {relativePath} ({bytes.Length} bytes)");
    }

    WriteLine(stream, End);
    Console.WriteLine($"Created {outputFile} with {count} file(s).");
    return 0;
}

static int Extract(string archiveFile, string outputFolder)
{
    if (!File.Exists(archiveFile))
    {
        Console.Error.WriteLine($"Archive not found: {archiveFile}");
        return 1;
    }

    using var stream = new FileStream(archiveFile, FileMode.Open, FileAccess.Read);

    var header = ReadLine(stream);
    if (header != Magic)
    {
        Console.Error.WriteLine($"Not a valid texttar archive (got: {header})");
        return 1;
    }

    int count = 0;
    while (true)
    {
        var line = ReadLine(stream);
        if (line == null || line == End) break;

        if (!line.StartsWith("FILE ")) { Console.Error.WriteLine($"Unexpected line: {line}"); return 1; }
        var relativePath = line["FILE ".Length..];

        var lengthLine = ReadLine(stream) ?? throw new InvalidDataException("Unexpected end of archive.");
        if (!lengthLine.StartsWith("LENGTH ")) { Console.Error.WriteLine($"Expected LENGTH, got: {lengthLine}"); return 1; }
        var byteCount = int.Parse(lengthLine["LENGTH ".Length..]);

        var bytes = new byte[byteCount];
        stream.ReadExactly(bytes);

        var text = Encoding.UTF8.GetString(bytes).ReplaceLineEndings(Environment.NewLine);
        var destPath = Path.Combine(outputFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        File.WriteAllText(destPath, text, Encoding.UTF8);
        count++;

        Console.WriteLine($"  > {relativePath} ({byteCount} bytes)");
    }

    Console.WriteLine($"Extracted {count} file(s) to {outputFolder}.");
    return 0;
}

// Writes a line as UTF-8 followed by \n (no \r, keeps it simple and cross-platform)
static void WriteLine(Stream stream, string line)
{
    var bytes = Encoding.UTF8.GetBytes(line + "\n");
    stream.Write(bytes);
}

// Reads a \n-terminated line from the stream without buffering ahead
static string? ReadLine(Stream stream)
{
    var sb = new StringBuilder();
    int b;
    while ((b = stream.ReadByte()) != -1)
    {
        if (b == '\n') return sb.ToString().TrimEnd('\r');
        sb.Append((char)b);
    }
    return sb.Length > 0 ? sb.ToString() : null;
}

static void PrintUsage()
{
    Console.WriteLine("texttar — length-prefixed text archive tool");
    Console.WriteLine();
    Console.WriteLine("  Create:  texttar -c <source-folder> <output-file>");
    Console.WriteLine("  Extract: texttar -x <archive-file>  <output-folder>");
}
