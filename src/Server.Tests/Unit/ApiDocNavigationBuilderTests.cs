using Server.Components.Docs;
using Server.Contracts.ApiDocumentation;

namespace Server.Tests.Unit;

public class ApiDocNavigationBuilderTests
{
    [Fact]
    public void SupportsStructuredGraph_requires_v3_graph_v1_and_ids()
    {
        var legacy = new StructuredApiDocDto { SchemaVersion = 2, NavigationModel = "graph-v1" };
        Assert.False(ApiDocNavigationBuilder.SupportsStructuredGraph(legacy));

        var missingModel = new StructuredApiDocDto { SchemaVersion = 3 };
        Assert.False(ApiDocNavigationBuilder.SupportsStructuredGraph(missingModel));

        var missingId = new StructuredApiDocDto
        {
            SchemaVersion = 3,
            NavigationModel = "graph-v1",
            Items = [new StructuredApiItemDto { Id = null, Name = "x" }],
        };
        Assert.False(ApiDocNavigationBuilder.SupportsStructuredGraph(missingId));
    }

    [Fact]
    public void BuildGraphRoots_orders_children_by_memberIds_then_id()
    {
        var doc = new StructuredApiDocDto
        {
            SchemaVersion = 3,
            NavigationModel = "graph-v1",
            Items =
            [
                new StructuredApiItemDto
                {
                    Id = 1,
                    Name = "Root",
                    Kind = "type",
                    MemberIds = [3, 2],
                },
                new StructuredApiItemDto { Id = 2, Name = "b", Kind = "field", ParentId = 1 },
                new StructuredApiItemDto { Id = 3, Name = "a", Kind = "field", ParentId = 1 },
            ],
        };

        var roots = ApiDocNavigationBuilder.BuildGraphRoots(doc);
        Assert.Single(roots);
        Assert.Equal(2, roots[0].Children.Count);
        Assert.Equal(3, roots[0].Children[0].Item.Id);
        Assert.Equal(2, roots[0].Children[1].Item.Id);
    }

    [Fact]
    public void FilterGraphRoots_keeps_ancestors_of_matches()
    {
        var doc = new StructuredApiDocDto
        {
            SchemaVersion = 3,
            NavigationModel = "graph-v1",
            Items =
            [
                new StructuredApiItemDto { Id = 1, Name = "Root", Kind = "type", MemberIds = [2, 3] },
                new StructuredApiItemDto { Id = 2, Name = "keep", Kind = "field", ParentId = 1 },
                new StructuredApiItemDto { Id = 3, Name = "drop", Kind = "field", ParentId = 1 },
            ],
        };

        var roots = ApiDocNavigationBuilder.BuildGraphRoots(doc);
        var filtered = ApiDocNavigationBuilder.FilterGraphRoots(roots, new HashSet<int> { 1, 2 });
        Assert.Single(filtered);
        Assert.Single(filtered[0].Children);
        Assert.Equal(2, filtered[0].Children[0].Item.Id);
    }

    [Fact]
    public void ModuleScopeRootCandidates_returns_only_root_modules()
    {
        var doc = new StructuredApiDocDto
        {
            SchemaVersion = 3,
            NavigationModel = "graph-v1",
            Items =
            [
                new StructuredApiItemDto { Id = 1, Name = "mod", Kind = "module", ParentId = null },
                new StructuredApiItemDto { Id = 2, Name = "T", Kind = "type", ParentId = 1 },
                new StructuredApiItemDto { Id = 3, Name = "other", Kind = "module", ParentId = 2 },
            ],
        };

        var modules = ApiDocNavigationBuilder.ModuleScopeRootCandidates(doc).ToList();
        Assert.Single(modules);
        Assert.Equal(1, modules[0].Id);
    }
}
