using System.Text;
using TextArchive;
using Xunit;

namespace TextArchive.Tests;

public class ArchiverTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public ArchiverTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string TempPath(string name) => Path.Combine(_tempDir, name);

    [Fact]
    public void RoundTrip_PreservesContent()
    {
        var src = TempPath("src");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "hello.txt"), "hello\nworld\n");

        var archive = TempPath("out.texttar");
        var dst = TempPath("dst");

        Assert.Equal(0, Archiver.Create(src, archive));
        Assert.Equal(0, Archiver.Extract(archive, dst));

        var result = File.ReadAllText(Path.Combine(dst, "hello.txt"));
        Assert.Equal("hello\nworld\n".ReplaceLineEndings(Environment.NewLine), result);
    }

    [Theory]
    [InlineData("lf.txt", "line1\nline2\n")]
    [InlineData("crlf.txt", "line1\r\nline2\r\n")]
    [InlineData("cr.txt", "line1\rline2\r")]
    public void Create_NormalizesNewlinesToLf(string fileName, string content)
    {
        var src = TempPath("src_" + fileName);
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, fileName), content);

        var archive = TempPath(fileName + ".texttar");
        Assert.Equal(0, Archiver.Create(src, archive));

        // Read the raw archive bytes and verify no \r appears in the file content block
        var raw = File.ReadAllBytes(archive);
        var text = Encoding.UTF8.GetString(raw);

        // Strip the header lines to isolate file content
        var headerEnd = text.IndexOf("\nLENGTH ");
        var lengthEnd = text.IndexOf('\n', headerEnd + 1);
        var fileContent = text[(lengthEnd + 1)..^(Archiver.End.Length + 1)];

        Assert.DoesNotContain('\r', fileContent);
    }

    [Fact]
    public void Extract_RestoresPlatformNewlines()
    {
        var src = TempPath("src");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "a.txt"), "line1\r\nline2\r\n");

        var archive = TempPath("out.texttar");
        var dst = TempPath("dst");

        Archiver.Create(src, archive);
        Archiver.Extract(archive, dst);

        var result = File.ReadAllText(Path.Combine(dst, "a.txt"));
        Assert.Equal("line1" + Environment.NewLine + "line2" + Environment.NewLine, result);
    }

    [Fact]
    public void RoundTrip_MultipleFilesAndSubdirectories()
    {
        var src = TempPath("src");
        Directory.CreateDirectory(Path.Combine(src, "sub"));
        File.WriteAllText(Path.Combine(src, "root.txt"), "root");
        File.WriteAllText(Path.Combine(src, "sub", "child.txt"), "child");

        var archive = TempPath("multi.texttar");
        var dst = TempPath("dst");

        Assert.Equal(0, Archiver.Create(src, archive));
        Assert.Equal(0, Archiver.Extract(archive, dst));

        Assert.Equal("root", File.ReadAllText(Path.Combine(dst, "root.txt")).ReplaceLineEndings("\n").TrimEnd('\n'));
        Assert.Equal("child", File.ReadAllText(Path.Combine(dst, "sub", "child.txt")).ReplaceLineEndings("\n").TrimEnd('\n'));
    }

    [Fact]
    public void Create_ExcludesMatchingFiles()
    {
        var src = TempPath("src");
        Directory.CreateDirectory(Path.Combine(src, "bin", "Debug"));
        File.WriteAllText(Path.Combine(src, "main.cs"), "code");
        File.WriteAllText(Path.Combine(src, "bin", "Debug", "app.dll"), "binary");

        var archive = TempPath("out.texttar");
        Assert.Equal(0, Archiver.Create(src, archive, ["**/bin/**"]));

        var dst = TempPath("dst");
        Archiver.Extract(archive, dst);
        Assert.True(File.Exists(Path.Combine(dst, "main.cs")));
        Assert.False(File.Exists(Path.Combine(dst, "bin", "Debug", "app.dll")));
    }

    [Fact]
    public void Create_ExcludesMultiplePatternsViaSemicolon()
    {
        var src = TempPath("src");
        Directory.CreateDirectory(Path.Combine(src, "bin"));
        Directory.CreateDirectory(Path.Combine(src, "obj"));
        File.WriteAllText(Path.Combine(src, "keep.txt"), "keep");
        File.WriteAllText(Path.Combine(src, "bin", "a.dll"), "bin");
        File.WriteAllText(Path.Combine(src, "obj", "b.obj"), "obj");

        var patterns = "**/bin/**;**/obj/**"
            .Split(';', StringSplitOptions.RemoveEmptyEntries);

        var archive = TempPath("out.texttar");
        Assert.Equal(0, Archiver.Create(src, archive, patterns));

        var dst = TempPath("dst");
        Archiver.Extract(archive, dst);
        Assert.True(File.Exists(Path.Combine(dst, "keep.txt")));
        Assert.False(File.Exists(Path.Combine(dst, "bin", "a.dll")));
        Assert.False(File.Exists(Path.Combine(dst, "obj", "b.obj")));
    }

    [Fact]
    public void Create_SkipsLockedFiles_AndContinues()
    {
        var src = TempPath("src");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "a.txt"), "included");
        var lockedPath = Path.Combine(src, "locked.txt");
        File.WriteAllText(lockedPath, "locked content");

        using var lockStream = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var archive = TempPath("out.texttar");
        var result = Archiver.Create(src, archive);

        Assert.Equal(0, result);
        var dst = TempPath("dst");
        Archiver.Extract(archive, dst);
        Assert.True(File.Exists(Path.Combine(dst, "a.txt")));
        Assert.False(File.Exists(Path.Combine(dst, "locked.txt")));
    }

    [Fact]
    public void Create_ReturnError_WhenSourceFolderMissing()
    {
        var result = Archiver.Create(TempPath("nonexistent"), TempPath("out.texttar"));
        Assert.Equal(1, result);
    }

    [Fact]
    public void Extract_ReturnError_WhenArchiveFileMissing()
    {
        var result = Archiver.Extract(TempPath("nonexistent.texttar"), TempPath("dst"));
        Assert.Equal(1, result);
    }

    [Fact]
    public void ArchiveHeader_ContainsExpectedMagic()
    {
        var src = TempPath("src");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "f.txt"), "x");

        var archive = TempPath("out.texttar");
        Archiver.Create(src, archive);

        var firstLine = File.ReadLines(archive).First();
        Assert.Equal(Archiver.Magic, firstLine);
    }
}
