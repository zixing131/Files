using Files.App.MacOS.Interop;
using Files.App.MacOS.Models;

namespace Files.App.MacOS.Services;

internal static class MacOSAccessibilityDisplayService
{
	private const MacOSAccessibilityDisplayOptions SupportedOptions =
		MacOSAccessibilityDisplayOptions.IncreaseContrast |
		MacOSAccessibilityDisplayOptions.ReduceTransparency |
		MacOSAccessibilityDisplayOptions.ReduceMotion;

	public static MacOSAccessibilityDisplayOptions GetCurrentOptions()
	{
		return (MacOSAccessibilityDisplayOptions)MacOSNativeMethods.GetAccessibilityDisplayOptions() & SupportedOptions;
	}
}
