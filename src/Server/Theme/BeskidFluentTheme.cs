namespace Server.Theme;

/// <summary>
/// Fluent UI Blazor <c>FluentDesignTheme</c> brand inputs aligned with
/// <c>packages/beskid-docs-ui/src/styles/theme.material.css</c> (site, tracker, hub).
/// Keep in sync when the Material seed or surface-variant neutrals change.
/// </summary>
public static class BeskidFluentTheme
{
	public const string StorageName = "theme";

	/// <summary>Maps to <c>--beskid-material-seed</c> / <c>--beskid-fluent-accent</c>.</summary>
	public const string AccentSeed = "#0ea5a3";

	/// <summary>Maps to <c>--beskid-fluent-neutral</c> (dark <c>--beskid-surface-variant</c>).</summary>
	public const string NeutralBase = "#3f4948";
}
