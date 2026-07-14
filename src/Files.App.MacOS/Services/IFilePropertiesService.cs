namespace Files.App.MacOS.Services;

public sealed record FilePropertiesSummary(
	int RootItemCount,
	long FileCount,
	long FolderCount,
	long TotalSize,
	string? Name,
	string? Path,
	DateTimeOffset? Created,
	DateTimeOffset? Modified,
	UnixFileMode? UnixMode,
	string? LinkTarget,
	IReadOnlyList<string>? FinderTags);

public interface IFilePropertiesService
{
	Task<FilePropertiesSummary> GetSummaryAsync(
		IReadOnlyList<string> paths,
		CancellationToken cancellationToken = default);

	Task UpdateAsync(
		string path,
		UnixFileMode unixMode,
		IReadOnlyList<string> finderTags,
		CancellationToken cancellationToken = default);
}
