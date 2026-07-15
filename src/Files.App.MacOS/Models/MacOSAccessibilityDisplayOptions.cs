namespace Files.App.MacOS.Models;

[Flags]
public enum MacOSAccessibilityDisplayOptions
{
	None = 0,
	IncreaseContrast = 1,
	ReduceTransparency = 2,
	ReduceMotion = 4,
}
