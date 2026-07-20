using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Files.App.MacOS.Interop;
using Microsoft.UI.Dispatching;

namespace Files.App.MacOS.Services;

internal sealed class MacOSFileManagerIntegrationService : IDisposable
{
	private GCHandle contextHandle;
	private DispatcherQueue? dispatcherQueue;
	private Func<IReadOnlyList<string>, Task>? openPaths;

	public unsafe void Install(DispatcherQueue callbackDispatcherQueue, Func<IReadOnlyList<string>, Task> openPathsHandler)
	{
		ArgumentNullException.ThrowIfNull(callbackDispatcherQueue);
		ArgumentNullException.ThrowIfNull(openPathsHandler);
		Dispose();
		dispatcherQueue = callbackDispatcherQueue;
		openPaths = openPathsHandler;
		contextHandle = GCHandle.Alloc(this);
		MacOSNativeMethods.InstallFileManagerServices(&OpenPathsCallback, GCHandle.ToIntPtr(contextHandle));
	}

	public void Dispose()
	{
		if (contextHandle.IsAllocated)
		{
			MacOSNativeMethods.UninstallFileManagerServices();
			contextHandle.Free();
		}
		dispatcherQueue = null;
		openPaths = null;
	}

	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
	private static void OpenPathsCallback(nint context, nint pathsJson)
	{
		if (GCHandle.FromIntPtr(context).Target is not MacOSFileManagerIntegrationService service ||
			service.dispatcherQueue is null || service.openPaths is null)
		{
			return;
		}

		string? json = Marshal.PtrToStringUTF8(pathsJson);
		if (string.IsNullOrWhiteSpace(json))
		{
			return;
		}

		string[]? paths;
		try
		{
			paths = JsonSerializer.Deserialize<string[]>(json);
		}
		catch (JsonException)
		{
			return;
		}
		if (paths is not { Length: > 0 })
		{
			return;
		}

		Func<IReadOnlyList<string>, Task> handler = service.openPaths;
		service.dispatcherQueue.TryEnqueue(async () => await handler(paths));
	}
}
