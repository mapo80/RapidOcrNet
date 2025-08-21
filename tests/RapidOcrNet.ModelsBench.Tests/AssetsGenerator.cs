using Xunit;

namespace RapidOcrNet.ModelsBench.Tests;

public class AssetsGenerator
{
    [Fact]
    public void Generate()
    {
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var dir = Path.Combine(repo, "tests", "RapidOcrNet.ModelsBench.Tests", "assets");
        AccentFixtures.SaveAssets(dir);
    }
}
