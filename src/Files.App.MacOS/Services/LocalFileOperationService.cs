namespace Files.App.MacOS.Services;

public sealed class LocalFileOperationService : IFileOperationService
{
	public Task<string> CreateFolderAsync(string parentPath, string desiredName, CancellationToken cancellationToken = default)
	{
		return Task.Run(() =>
		{
			cancellationToken.ThrowIfCancellationRequested();
			ValidateName(desiredName);

			string destination = GetUniquePath(parentPath, desiredName);
			Directory.CreateDirectory(destination);
			return destination;
		}, cancellationToken);
	}

	public Task<string> RenameAsync(string sourcePath, string desiredName, CancellationToken cancellationToken = default)
	{
		return Task.Run(() =>
		{
			cancellationToken.ThrowIfCancellationRequested();
			ValidateName(desiredName);

			string? parentPath = Path.GetDirectoryName(sourcePath);
			if (string.IsNullOrEmpty(parentPath))
			{
				throw new FileOperationException(FileOperationError.MissingParent);
			}

			string destination = Path.Combine(parentPath, desiredName);
			if (File.Exists(destination) || Directory.Exists(destination))
			{
				throw new FileOperationException(FileOperationError.AlreadyExists, desiredName);
			}

			if (Directory.Exists(sourcePath))
			{
				Directory.Move(sourcePath, destination);
			}
			else if (File.Exists(sourcePath))
			{
				File.Move(sourcePath, destination);
			}
			else
			{
				throw new FileOperationException(FileOperationError.ItemNotFound, Path.GetFileName(sourcePath));
			}

			return destination;
		}, cancellationToken);
	}

	public Task<string> CreateFileAsync(string parentPath, string desiredName, CancellationToken cancellationToken = default)
	{
		return Task.Run(() =>
		{
			cancellationToken.ThrowIfCancellationRequested();
			ValidateName(desiredName);

			string destination = GetUniqueFilePath(parentPath, desiredName);
			using (File.Create(destination))
			{
			}
			return destination;
		}, cancellationToken);
	}

	public Task<IReadOnlyList<string>> DeletePermanentlyAsync(
		IReadOnlyList<string> paths,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(paths);
		return Task.Run<IReadOnlyList<string>>(() =>
		{
			string[] roots = RemoveDescendantPaths(paths);
			var completed = new List<string>(roots.Length);
			foreach (string path in roots)
			{
				cancellationToken.ThrowIfCancellationRequested();
				try
				{
					System.IO.FileAttributes attributes = File.GetAttributes(path);
					bool isDirectory = attributes.HasFlag(System.IO.FileAttributes.Directory);
					string? linkTarget = isDirectory ? new DirectoryInfo(path).LinkTarget : new FileInfo(path).LinkTarget;
					if (isDirectory && linkTarget is null)
					{
						Directory.Delete(path, recursive: true);
					}
					else
					{
						File.Delete(path);
					}
					completed.Add(path);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					throw new PermanentDeletePartialException(path, completed.ToArray(), ex);
				}
			}

			return completed;
		}, cancellationToken);
	}

	public Task<IReadOnlyList<CreatedSymbolicLink>> CreateSymbolicLinksAsync(
		IReadOnlyList<SymbolicLinkRequest> requests,
		string destinationDirectory,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(requests);
		return Task.Run<IReadOnlyList<CreatedSymbolicLink>>(() =>
		{
			string destination = Path.GetFullPath(destinationDirectory);
			if (!Directory.Exists(destination))
			{
				throw new FileOperationException(FileOperationError.ItemNotFound, Path.GetFileName(destination));
			}

			var created = new List<CreatedSymbolicLink>(requests.Count);
			try
			{
				foreach (SymbolicLinkRequest request in requests)
				{
					cancellationToken.ThrowIfCancellationRequested();
					ValidateName(request.DesiredName);
					string source = Path.GetFullPath(request.SourcePath);
					System.IO.FileAttributes attributes = File.GetAttributes(source);
					bool isDirectory = attributes.HasFlag(System.IO.FileAttributes.Directory);
					string linkPath = GetUniqueSymbolicLinkPath(destination, request.DesiredName, isDirectory);
					string linkTarget = Path.GetRelativePath(destination, source);
					CreateSymbolicLink(linkPath, linkTarget, isDirectory);
					created.Add(new(linkPath, linkTarget, isDirectory));
				}
				return created;
			}
			catch
			{
				DeleteCreatedLinks(created);
				throw;
			}
		}, cancellationToken);
	}

	public Task ReplaySymbolicLinksAsync(
		IReadOnlyList<CreatedSymbolicLink> links,
		bool isUndo,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(links);
		return Task.Run(() =>
		{
			if (isUndo)
			{
				foreach (CreatedSymbolicLink link in links)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (!string.Equals(GetLinkTarget(link.Path), link.LinkTarget, StringComparison.Ordinal))
					{
						throw new FileOperationException(FileOperationError.CreatedItemChanged, Path.GetFileName(link.Path));
					}
				}

				var deleted = new List<CreatedSymbolicLink>(links.Count);
				try
				{
					foreach (CreatedSymbolicLink link in links)
					{
						cancellationToken.ThrowIfCancellationRequested();
						File.Delete(link.Path);
						deleted.Add(link);
					}
				}
				catch
				{
					CreateExactLinks(deleted);
					throw;
				}
				return;
			}

			foreach (CreatedSymbolicLink link in links)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (EntryExistsIncludingLink(link.Path))
				{
					throw new FileOperationException(FileOperationError.AlreadyExists, Path.GetFileName(link.Path));
				}
			}
			var recreated = new List<CreatedSymbolicLink>(links.Count);
			try
			{
				foreach (CreatedSymbolicLink link in links)
				{
					cancellationToken.ThrowIfCancellationRequested();
					CreateSymbolicLink(link.Path, link.LinkTarget, link.IsDirectory);
					recreated.Add(link);
				}
			}
			catch
			{
				DeleteCreatedLinks(recreated);
				throw;
			}
		}, cancellationToken);
	}

	private static string GetUniqueSymbolicLinkPath(string destination, string desiredName, bool isDirectory)
	{
		string candidate = Path.Combine(destination, desiredName);
		if (!EntryExistsIncludingLink(candidate))
		{
			return candidate;
		}

		string name = isDirectory ? desiredName : Path.GetFileNameWithoutExtension(desiredName);
		string extension = isDirectory ? string.Empty : Path.GetExtension(desiredName);
		for (int suffix = 2; ; suffix++)
		{
			candidate = Path.Combine(destination, $"{name} ({suffix}){extension}");
			if (!EntryExistsIncludingLink(candidate))
			{
				return candidate;
			}
		}
	}

	private static void CreateExactLinks(IEnumerable<CreatedSymbolicLink> links)
	{
		foreach (CreatedSymbolicLink link in links)
		{
			CreateSymbolicLink(link.Path, link.LinkTarget, link.IsDirectory);
		}
	}

	private static void DeleteCreatedLinks(IEnumerable<CreatedSymbolicLink> links)
	{
		foreach (CreatedSymbolicLink link in links.Reverse())
		{
			try
			{
				File.Delete(link.Path);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
			}
		}
	}

	private static void CreateSymbolicLink(string path, string target, bool isDirectory)
	{
		if (isDirectory)
		{
			Directory.CreateSymbolicLink(path, target);
		}
		else
		{
			File.CreateSymbolicLink(path, target);
		}
	}

	private static string? GetLinkTarget(string path) =>
		new FileInfo(path).LinkTarget ?? new DirectoryInfo(path).LinkTarget;

	private static bool EntryExistsIncludingLink(string path) =>
		File.Exists(path) || Directory.Exists(path) || GetLinkTarget(path) is not null;

	private static string[] RemoveDescendantPaths(IReadOnlyList<string> paths)
	{
		string[] ordered = paths
			.Select(Path.GetFullPath)
			.Distinct(StringComparer.Ordinal)
			.OrderBy(static path => path.Length)
			.ToArray();
		var roots = new List<string>(ordered.Length);
		foreach (string path in ordered)
		{
			if (!roots.Any(root => IsDescendant(path, root)))
			{
				roots.Add(path);
			}
		}
		return roots.ToArray();
	}

	private static bool IsDescendant(string path, string potentialParent)
	{
		string relativePath = Path.GetRelativePath(potentialParent, path);
		return relativePath != "." && relativePath != ".." &&
			!relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
			!Path.IsPathRooted(relativePath);
	}

	private static string GetUniquePath(string parentPath, string desiredName)
	{
		string destination = Path.Combine(parentPath, desiredName);
		if (!File.Exists(destination) && !Directory.Exists(destination))
		{
			return destination;
		}

		for (int suffix = 2; ; suffix++)
		{
			destination = Path.Combine(parentPath, $"{desiredName} ({suffix})");
			if (!File.Exists(destination) && !Directory.Exists(destination))
			{
				return destination;
			}
		}
	}

	private static string GetUniqueFilePath(string parentPath, string desiredName)
	{
		string destination = Path.Combine(parentPath, desiredName);
		if (!File.Exists(destination) && !Directory.Exists(destination))
		{
			return destination;
		}

		string name = Path.GetFileNameWithoutExtension(desiredName);
		string extension = Path.GetExtension(desiredName);
		for (int suffix = 2; ; suffix++)
		{
			destination = Path.Combine(parentPath, $"{name} ({suffix}){extension}");
			if (!File.Exists(destination) && !Directory.Exists(destination))
			{
				return destination;
			}
		}
	}

	private static void ValidateName(string name)
	{
		if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
		{
			throw new FileOperationException(FileOperationError.InvalidName);
		}

		if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
			name.Contains(Path.DirectorySeparatorChar) ||
			name.Contains(Path.AltDirectorySeparatorChar))
		{
			throw new FileOperationException(FileOperationError.InvalidCharacters);
		}
	}
}
