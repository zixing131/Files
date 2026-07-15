using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Files.App.MacOS.Interop;
using Microsoft.UI.Dispatching;

namespace Files.App.MacOS.Services;

internal sealed class MacOSAuxiliaryMouseService : IDisposable
{
	private readonly GCHandle callbackHandle;
	private readonly DispatcherQueue dispatcherQueue;
	private readonly Action<int> auxiliaryMouseCallback;
	private readonly Func<double, double, bool, bool> scrollWheelCallback;
	private bool isDisposed;

	public unsafe MacOSAuxiliaryMouseService(
		DispatcherQueue dispatcherQueue,
		Action<int> auxiliaryMouseCallback,
		Func<double, double, bool, bool> scrollWheelCallback)
	{
		this.dispatcherQueue = dispatcherQueue;
		this.auxiliaryMouseCallback = auxiliaryMouseCallback;
		this.scrollWheelCallback = scrollWheelCallback;
		callbackHandle = GCHandle.Alloc(this);
		MacOSNativeMethods.InstallAuxiliaryMouseHandler(
			&HandleAuxiliaryMouseButton,
			&HandleScrollWheel,
			GCHandle.ToIntPtr(callbackHandle));
	}

	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
	private static void HandleAuxiliaryMouseButton(nint context, int buttonNumber)
	{
		if (GCHandle.FromIntPtr(context).Target is MacOSAuxiliaryMouseService service)
		{
			service.dispatcherQueue.TryEnqueue(() => service.auxiliaryMouseCallback(buttonNumber));
		}
	}

	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
	private static int HandleScrollWheel(nint context, double deltaX, double deltaY, int hasPreciseDeltas)
	{
		try
		{
			if (GCHandle.FromIntPtr(context).Target is not MacOSAuxiliaryMouseService service)
			{
				return 0;
			}
			if (service.dispatcherQueue.HasThreadAccess)
			{
				return service.scrollWheelCallback(deltaX, deltaY, hasPreciseDeltas is not 0) ? 1 : 0;
			}

			return service.dispatcherQueue.TryEnqueue(() =>
				service.scrollWheelCallback(deltaX, deltaY, hasPreciseDeltas is not 0)) ? 1 : 0;
		}
		catch
		{
			return 0;
		}
	}

	internal static void SetGridScrollCapture(bool isEnabled) =>
		MacOSNativeMethods.SetGridScrollCapture(isEnabled ? 1 : 0);

	public void Dispose()
	{
		if (isDisposed)
		{
			return;
		}
		isDisposed = true;
		MacOSNativeMethods.UninstallAuxiliaryMouseHandler();
		callbackHandle.Free();
	}
}
