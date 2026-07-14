namespace Files.App.MacOS.Services;

public interface IFileOperationService
{
	Task<string> CreateFolderAsync(string parentPath, string desiredName, CancellationToken cancellationToken = default);

	Task<string> CreateFileAsync(string parentPath, string desiredName, CancellationToken cancellationToken = default);

	Task<string> RenameAsync(string sourcePath, string desiredName, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<string>> DeletePermanentlyAsync(
		IReadOnlyList<string> paths,
		CancellationToken cancellationToken = default);

}

public sealed class PermanentDeletePartialException(
	string failedPath,
	IReadOnlyList<string> completedPaths,
	Exception innerException)
	: IOException(innerException.Message, innerException)
{
	public string FailedPath { get; } = failedPath;

	public IReadOnlyList<string> CompletedPaths { get; } = completedPaths;
}
