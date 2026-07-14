namespace Files.App.MacOS.Services;

public interface IFileOperationService
{
	Task<string> CreateFolderAsync(string parentPath, string desiredName, CancellationToken cancellationToken = default);

	Task<string> CreateFileAsync(string parentPath, string desiredName, CancellationToken cancellationToken = default);

	Task<string> RenameAsync(string sourcePath, string desiredName, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<string>> DeletePermanentlyAsync(
		IReadOnlyList<string> paths,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<CreatedSymbolicLink>> CreateSymbolicLinksAsync(
		IReadOnlyList<SymbolicLinkRequest> requests,
		string destinationDirectory,
		CancellationToken cancellationToken = default);

	Task ReplaySymbolicLinksAsync(
		IReadOnlyList<CreatedSymbolicLink> links,
		bool isUndo,
		CancellationToken cancellationToken = default);

}

public sealed record SymbolicLinkRequest(string SourcePath, string DesiredName);

public sealed record CreatedSymbolicLink(string Path, string LinkTarget, bool IsDirectory);

public sealed class PermanentDeletePartialException(
	string failedPath,
	IReadOnlyList<string> completedPaths,
	Exception innerException)
	: IOException(innerException.Message, innerException)
{
	public string FailedPath { get; } = failedPath;

	public IReadOnlyList<string> CompletedPaths { get; } = completedPaths;
}
