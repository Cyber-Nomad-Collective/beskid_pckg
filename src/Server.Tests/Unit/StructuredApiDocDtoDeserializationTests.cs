using System.Text.Json;
using Server.Contracts.ApiDocumentation;

namespace Server.Tests.Unit;

public class StructuredApiDocDtoDeserializationTests
{

    [Fact]
    public void Deserializes_graph_nav_and_structured_doc_payload()
    {
        const string json = """
            {
              "schemaVersion": 3,
              "navigationModel": "graph-v1",
              "source": "test.bd",
              "items": [
                {
                  "id": 1,
                  "qualifiedName": "Root",
                  "name": "Root",
                  "kind": "module",
                  "visibility": "public",
                  "parentId": null,
                  "memberIds": [2],
                  "location": { "file": "a.bd", "startLine": 1, "startColumn": 1, "endLine": 2, "endColumn": 1 },
                  "doc_markdown": "## Body\n",
                  "doc": {
                    "summaryMarkdown": "Sum **x**.",
                    "returnsMarkdown": "An `i64`.",
                    "arguments": [{ "name": "x", "markdown": "Arg." }],
                    "enumVariants": [],
                    "typeParameters": []
                  }
                },
                {
                  "id": 2,
                  "qualifiedName": "Root::f",
                  "name": "f",
                  "kind": "function",
                  "visibility": "public",
                  "parentId": 1,
                  "memberIds": [],
                  "location": { "file": "a.bd", "startLine": 3, "startColumn": 1, "endLine": 4, "endColumn": 1 },
                  "doc_markdown": null,
                  "doc": null
                }
              ]
            }
            """;

        var doc = JsonSerializer.Deserialize<StructuredApiDocDto>(json, StructuredApiDocJson.Options);
        Assert.NotNull(doc);
        Assert.Equal(3, doc!.SchemaVersion);
        Assert.Equal("graph-v1", doc.NavigationModel);
        Assert.Equal(2, doc.Items.Count);

        var root = doc.Items[0];
        Assert.Equal(1, root.Id);
        Assert.Null(root.ParentId);
        Assert.Single(root.MemberIds);
        Assert.Equal(2, root.MemberIds[0]);
        Assert.NotNull(root.Doc);
        Assert.Equal("Sum **x**.", root.Doc!.SummaryMarkdown);
        Assert.Single(root.Doc.Arguments);
        Assert.Equal("x", root.Doc.Arguments[0].Name);

        var child = doc.Items[1];
        Assert.Equal(2, child.Id);
        Assert.Equal(1, child.ParentId);
    }
}
