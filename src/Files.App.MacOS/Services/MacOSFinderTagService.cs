using System.Runtime.InteropServices;
using System.Text.Json;
using Files.App.MacOS.Interop;

namespace Files.App.MacOS.Services;

internal static class MacOSFinderTagService
{
	public static IReadOnlyList<string> GetTags(string path)
	{
		try
		{
			nint resultPointer = MacOSNativeMethods.GetFinderTags(path);
			if (resultPointer is 0)
			{
				return [];
			}

			try
			{
				string json = Marshal.PtrToStringUTF8(resultPointer) ?? "[]";
				return JsonSerializer.Deserialize<string[]>(json) ?? [];
			}
			finally
			{
				MacOSNativeMethods.Free(resultPointer);
			}
		}
		catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or JsonException)
		{
			return [];
		}
	}

	public static void SetTags(string path, IReadOnlyList<string> tags)
	{
		string json = JsonSerializer.Serialize(tags);
		nint errorPointer = MacOSNativeMethods.SetFinderTags(path, json);
		if (errorPointer is 0)
		{
			return;
		}

		try
		{
			throw new IOException(Marshal.PtrToStringUTF8(errorPointer) ?? "Finder tags couldn't be saved.");
		}
		finally
		{
			MacOSNativeMethods.Free(errorPointer);
		}
	}
}
