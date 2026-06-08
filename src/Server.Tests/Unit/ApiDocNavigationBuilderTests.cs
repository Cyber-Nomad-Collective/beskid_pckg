using Server.Components.Docs;
using Server.Contracts.ApiDocumentation;

namespace Server.Tests.Unit;

public class ApiDocNavigationBuilderTests
{
    [Fact]
    public void BuildLibraryTreeRoots_Groups_Type_Under_Module_Folder()
    {
        var doc = new StructuredApiDocDto
        {
            SchemaVersion = 4,
            NavigationModel = ApiDocNavigationBuilder.NavigationModelGraphV1,
            Items =
            [
                new StructuredApiItemDto
                {
                    Id = 10,
                    QualifiedName = "App",
                    Name = "App",
                    Kind = "module",
                    ModulePath = ["App"],
                    ParentId = null,
                },
                new StructuredApiItemDto
                {
                    Id = 1,
                    QualifiedName = "App::Customer",
                    Name = "Customer",
                    Kind = "type",
                    ModulePath = ["App"],
                    ParentId = 10,
                },
            ],
        };

        var roots = ApiDocNavigationBuilder.BuildLibraryTreeRoots(doc, "demo");
        Assert.Single(roots);
        Assert.Equal("App", roots[0].Item?.Name);
        Assert.Contains(roots[0].Children, c => c.Item?.Id == 1 && c.Item?.Kind == "type");
    }

    [Fact]
    public void BuildLibraryTreeRoots_Omits_Builtin_Dependency_Group()
    {
        var doc = new StructuredApiDocDto
        {
            SchemaVersion = 4,
            NavigationModel = ApiDocNavigationBuilder.NavigationModelGraphV1,
            Items =
            [
                new StructuredApiItemDto
                {
                    Id = 1,
                    QualifiedName = "Host",
                    Name = "Host",
                    Kind = "module",
                    ParentId = null,
                },
                new StructuredApiItemDto
                {
                    Id = 2,
                    QualifiedName = "beskid::print",
                    Name = "print",
                    Kind = "function",
                    DeclaringPackage = ApiDocNavigationBuilder.BuiltinDeclaringPackage,
                    ParentId = null,
                },
                new StructuredApiItemDto
                {
                    Id = 3,
                    QualifiedName = "Foreign::Type",
                    Name = "Type",
                    Kind = "type",
                    DeclaringPackage = "other_pkg",
                    ParentId = null,
                },
            ],
        };

        var roots = ApiDocNavigationBuilder.BuildLibraryTreeRoots(doc, "host");
        Assert.Equal(2, roots.Count);
        Assert.Contains(roots, r => r.Label == "Dependencies");
        var deps = roots.First(r => r.Label == "Dependencies");
        Assert.DoesNotContain(deps.Children, c => c.Label == ApiDocNavigationBuilder.BuiltinDeclaringPackage);
        Assert.Contains(deps.Children, c => c.Label == "other_pkg");
    }

    [Fact]
    public void BuildLibraryTreeRoots_Adds_Dependencies_Group()
    {
        var doc = new StructuredApiDocDto
        {
            SchemaVersion = 4,
            NavigationModel = ApiDocNavigationBuilder.NavigationModelGraphV1,
            Items =
            [
                new StructuredApiItemDto
                {
                    Id = 1,
                    QualifiedName = "Host",
                    Name = "Host",
                    Kind = "module",
                    ParentId = null,
                },
                new StructuredApiItemDto
                {
                    Id = 2,
                    QualifiedName = "Foreign::Type",
                    Name = "Type",
                    Kind = "type",
                    DeclaringPackage = "other_pkg",
                    ParentId = null,
                },
            ],
        };

        var roots = ApiDocNavigationBuilder.BuildLibraryTreeRoots(doc, "host");
        Assert.Equal(2, roots.Count);
        Assert.Contains(roots, r => r.Label == "Dependencies");
    }
}
