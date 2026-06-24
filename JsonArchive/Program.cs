using System.Text.Json;

if (args.Length < 2)
{
    PrintUsage();
    return 1;
}

string mode = args[0].ToLower();

if (mode is "-c" or "--create")
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: jsonarchive -c <source-folder> <output-file>");
        return 1;
    }
    return Create(sourceFolder: args[1], outputFile: args[2]);
}
else if (mode is "-x" or "--extract")
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: jsonarchive -x <archive-file> <output-folder>");
        return 1;
    }
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

    var entries = new List<ArchiveEntry>();
    var files = Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories);

    foreach (var file in files)
    {
        var relativePath = Path.GetRelativePath(sourceFolder, file).Replace('\\', '/');
        var content = File.ReadAllText(file);
        entries.Add(new ArchiveEntry(relativePath, content.Length, content));
        Console.WriteLine($"  + {relativePath} ({content.Length} chars)");
    }

    var archive = new Archive("jsontar/1.0", entries);
    var json = JsonSerializer.Serialize(archive, ArchiveContext.Default.Archive);
    File.WriteAllText(outputFile, json);

    Console.WriteLine($"Created {outputFile} with {entries.Count} file(s).");
    return 0;
}

static int Extract(string archiveFile, string outputFolder)
{
    if (!File.Exists(archiveFile))
    {
        Console.Error.WriteLine($"Archive not found: {archiveFile}");
        return 1;
    }

    var json = File.ReadAllText(archiveFile);
    var archive = JsonSerializer.Deserialize(json, ArchiveContext.Default.Archive)
        ?? throw new InvalidDataException("Failed to deserialize archive.");

    foreach (var entry in archive.Files)
    {
        var destPath = Path.Combine(outputFolder, entry.Path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        File.WriteAllText(destPath, entry.Content);
        Console.WriteLine($"  > {entry.Path} ({entry.Length} chars)");
    }

    Console.WriteLine($"Extracted {archive.Files.Count} file(s) to {outputFolder}.");
    return 0;
}

static void PrintUsage()
{
    Console.WriteLine("jsonarchive — JSON-based text archive tool");
    Console.WriteLine();
    Console.WriteLine("  Create:  jsonarchive -c <source-folder> <output-file>");
    Console.WriteLine("  Extract: jsonarchive -x <archive-file>  <output-folder>");
}

record ArchiveEntry(string Path, int Length, string Content);
record Archive(string Format, List<ArchiveEntry> Files);

[System.Text.Json.Serialization.JsonSerializable(typeof(Archive))]
[System.Text.Json.Serialization.JsonSerializable(typeof(ArchiveEntry))]
partial class ArchiveContext : System.Text.Json.Serialization.JsonSerializerContext { }
