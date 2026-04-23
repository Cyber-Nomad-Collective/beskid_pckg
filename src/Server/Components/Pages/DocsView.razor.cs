using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Components.Pages;

public partial class DocsView
{
    [Parameter] public string PackageWithVersion { get; set; } = string.Empty;

    private bool _parseError;
    private string _packageSegment = string.Empty;
    private string _versionSegment = string.Empty;
    private string _packageLabel = string.Empty;
    private string? _packagesHref;
    private string PageHeading => _parseError ? "Documentation" : $"Docs · {_packageLabel}";

    protected override async Task OnParametersSetAsync()
    {
        _parseError = false;
        _packagesHref = null;
        _packageLabel = string.Empty;

        var raw = Uri.UnescapeDataString(PackageWithVersion ?? string.Empty).Trim().TrimEnd('/');
        var at = raw.IndexOf('@');
        if (at <= 0 || at >= raw.Length - 1)
        {
            _parseError = true;
            return;
        }

        _packageSegment = raw[..at].Trim();
        _versionSegment = raw[(at + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(_packageSegment) || string.IsNullOrWhiteSpace(_versionSegment))
        {
            _parseError = true;
            return;
        }

        _packageLabel = _packageSegment;
        if (Guid.TryParse(_packageSegment, out var packageId))
        {
            var row = await DbContext.Packages.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == packageId);
            if (row is not null)
            {
                _packageLabel = row.Name;
                _packagesHref = $"/packages/{Uri.EscapeDataString(row.Name)}";
            }
        }
        else
        {
            _packagesHref = $"/packages/{Uri.EscapeDataString(_packageSegment)}";
        }
    }
}
