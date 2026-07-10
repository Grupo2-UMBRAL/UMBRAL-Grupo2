using System.Xml.Linq;
using Xunit;

namespace Umbral.ServiceDefaults.UnitTests;

/// <summary>
/// Regression lock for the kernel split (ADR-015). These are file-based assertions
/// over the .csproj graph — they need no reference to the Domain assemblies, so the
/// test project stays framework-only. If either invariant regresses, the intent is to
/// fail here loudly rather than let framework dependencies leak back into Domain.
/// </summary>
public sealed class ArchitectureInvariantsTests
{
    // Umbral.Kernel is the framework-free core: it must never gain a package or
    // framework reference, otherwise the Domain projects that reference it would
    // silently inherit ASP.NET Core / EF Core again (RNF-07).
    [Fact]
    public void Kernel_declares_no_package_or_framework_references()
    {
        var csproj = XDocument.Load(Path.Combine(RepoRoot(), "src", "shared", "Umbral.Kernel", "Umbral.Kernel.csproj"));

        Assert.Empty(csproj.Descendants("PackageReference"));
        Assert.Empty(csproj.Descendants("FrameworkReference"));
    }

    // Every *.Domain project must stay framework-free: it may reference Umbral.Kernel
    // (or nothing at all), but never Umbral.ServiceDefaults nor any NuGet/framework
    // package that would drag a web/persistence stack into the domain layer.
    [Theory]
    [MemberData(nameof(DomainProjects))]
    public void Domain_projects_reference_only_the_kernel(string domainCsprojPath)
    {
        var csproj = XDocument.Load(domainCsprojPath);

        Assert.Empty(csproj.Descendants("PackageReference"));
        Assert.Empty(csproj.Descendants("FrameworkReference"));

        var projectReferences = csproj.Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(projectReferences, include => include.Contains("Umbral.ServiceDefaults"));
        Assert.All(projectReferences, include => Assert.Contains("Umbral.Kernel", include));
    }

    public static IEnumerable<object[]> DomainProjects()
    {
        var servicesRoot = Path.Combine(RepoRoot(), "src", "services");

        return Directory
            .EnumerateFiles(servicesRoot, "*.Domain.csproj", SearchOption.AllDirectories)
            .Select(path => new object[] { path });
    }

    // Walk up from the test binary (artifacts/bin/... at repo root under UseArtifactsOutput)
    // until the solution file is found. Fail with a clear message if it is not.
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Umbral.sln")))
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "Could not locate Umbral.sln walking up from the test output directory.");
        return directory!.FullName;
    }
}
