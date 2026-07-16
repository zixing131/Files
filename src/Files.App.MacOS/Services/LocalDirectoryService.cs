using Files.App.MacOS.Models;

namespace Files.App.MacOS.Services;

public sealed class LocalDirectoryService : IDirectoryService
{
	public Task<IReadOnlyList<LocalFileSystemItem>> GetItemsAsync(string path, CancellationToken cancellationToken)
	{
		return Task.Run<IReadOnlyList<LocalFileSystemItem>>(() =>
		{
			var items = new List<LocalFileSystemItem>();

			foreach (FileSystemInfo info in new DirectoryInfo(path).EnumerateFileSystemInfos())
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (InternalFileArtifact.IsPath(info.FullName))
				{
					continue;
				}

				try
				{
					System.IO.FileAttributes attributes = info.Attributes;
					bool isDirectory = attributes.HasFlag(System.IO.FileAttributes.Directory);
					bool isPackage = isDirectory && MacOSFilePackage.IsPackage(info);
					bool isHidden = attributes.HasFlag(System.IO.FileAttributes.Hidden) || info.Name.StartsWith('.');
					long? size = info is FileInfo fileInfo ? fileInfo.Length : null;
					MacOSFinderTagService.SortMetadata sortMetadata = MacOSFinderTagService.GetSortMetadata(info.FullName);

					items.Add(new(
						info.FullName,
						info.Name,
						isDirectory,
						isHidden,
						size,
						info.LastWriteTimeUtc,
						isPackage,
						info.CreationTimeUtc,
						sortMetadata.LastOpened ?? info.LastAccessTimeUtc,
						sortMetadata.Added ?? info.CreationTimeUtc,
						sortMetadata.Tags,
						sortMetadata.Version,
						sortMetadata.Comments,
						sortMetadata.Kind));
				}
				catch (IOException)
				{
				}
				catch (UnauthorizedAccessException)
				{
				}
			}

			return items.ToArray();
		}, cancellationToken);
	}
}
