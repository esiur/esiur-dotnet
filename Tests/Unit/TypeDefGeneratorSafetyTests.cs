using Esiur.Proxy;

namespace Esiur.Tests.Unit;

public class TypeDefGeneratorSafetyTests
{
    [Fact]
    public void GeneratedFileName_RejectsPathsAndAcceptsQualifiedTypeNames()
    {
        Assert.Equal(
            "Example.Contracts.Device.g.cs",
            TypeDefGenerator.GetGeneratedFileName("Example.Contracts.Device"));

        Assert.Throws<InvalidDataException>(
            () => TypeDefGenerator.GetGeneratedFileName("../outside"));
        Assert.Throws<InvalidDataException>(
            () => TypeDefGenerator.GetGeneratedFileName("folder/type"));
        Assert.Throws<InvalidDataException>(
            () => TypeDefGenerator.GetGeneratedFileName(" "));
    }

    [Fact]
    public void Cleanup_DeletesOnlyManifestedGeneratedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"esiur-generator-{Guid.NewGuid():N}");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(output);

        try
        {
            var generated = Path.Combine(output, "Old.g.cs");
            var unlistedGenerated = Path.Combine(output, "Keep.g.cs");
            var userFile = Path.Combine(output, "notes.txt");
            var outsideFile = Path.Combine(root, "outside.g.cs");
            File.WriteAllText(generated, "generated");
            File.WriteAllText(unlistedGenerated, "unlisted");
            File.WriteAllText(userFile, "user");
            File.WriteAllText(outsideFile, "outside");
            File.WriteAllLines(
                Path.Combine(output, ".esiur-generated-files"),
                new[] { "Old.g.cs", "../outside.g.cs", "notes.txt" });

            TypeDefGenerator.DeletePreviouslyGeneratedFiles(new DirectoryInfo(output));

            Assert.False(File.Exists(generated));
            Assert.True(File.Exists(unlistedGenerated));
            Assert.True(File.Exists(userFile));
            Assert.True(File.Exists(outsideFile));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
