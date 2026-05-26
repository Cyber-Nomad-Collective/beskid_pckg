namespace Server.Components.Layout;

/// <summary>Static markup shared with <c>@beskid/docs-ui</c> hub (launcher + close icons).</summary>
internal static class BeskidHubMarkup
{
	public const string LauncherIcon =
		"""<svg class="beskid-hub-trigger__icon" xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect width="7" height="7" x="3" y="3" rx="1"/><rect width="7" height="7" x="14" y="3" rx="1"/><rect width="7" height="7" x="3" y="14" rx="1"/><rect width="7" height="7" x="14" y="14" rx="1"/></svg>""";

	public const string CloseIcon =
		"""<svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 6l12 12M18 6 6 18"></path></svg>""";
}
