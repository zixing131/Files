namespace Files.App.MacOS.Services;

internal sealed record FileOperationHistoryEntry(
	FilePathRename[] Renames,
	string? CreatedPath = null,
	bool CreatedDirectory = false,
	FileTransferHistoryEntry? Transfer = null,
	FileTrashHistoryEntry? Trash = null,
	CreatedSymbolicLink[]? SymbolicLinks = null);

internal sealed class FileTrashHistoryEntry(IReadOnlyList<TrashedItemResult> items)
{
	public IReadOnlyList<TrashedItemResult> Items { get; set; } = items;
}
