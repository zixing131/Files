using Microsoft.Extensions.Logging;
using Uno.Resizetizer;
using Files.App.MacOS.Services;

namespace Files.App.MacOS;

public partial class App : Application, IMacOSMenuCommandTarget
{
	private readonly Dictionary<Window, MainPage> windows = [];
	private readonly MacOSMainMenuService mainMenuService = new();
	private MainPage? activePage;
	private bool isMainMenuInstalled;

	public App()
	{
		AppLanguageManager.Apply(AppLanguageManager.LoadPreference());
		InitializeComponent();
	}

	protected Window? MainWindow { get; private set; }

	protected override void OnLaunched(LaunchActivatedEventArgs args)
	{
		MainWindow = CreateWindow(restoreWorkspace: true);
	}

	internal MacOSMainMenuService MainMenuService => mainMenuService;

	internal int WindowCount => windows.Count;

	internal MainPage? ActivePage => activePage;

	internal Window CreateWindow(bool restoreWorkspace = false)
	{
		var page = new MainPage(restoreWorkspace);
		var window = new Window { Content = page };
		windows.Add(window, page);
		window.Activated += Window_Activated;
		window.Closed += Window_Closed;
		window.SetWindowIcon();

		if (!isMainMenuInstalled)
		{
			mainMenuService.Install(
				this,
				string.Equals(Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride, "zh-Hans", StringComparison.Ordinal),
				page.DispatcherQueue);
			isMainMenuInstalled = true;
		}

		activePage = page;
		window.Activate();
		UpdateMainMenu(page);
		return window;
	}

	internal void CloseWindow(MainPage page)
	{
		Window? window = windows.FirstOrDefault(pair => ReferenceEquals(pair.Value, page)).Key;
		window?.Close();
	}

	internal void UpdateMainMenu(MainPage page)
	{
		if (ReferenceEquals(activePage, page))
		{
			mainMenuService.UpdateValidationSnapshot(this);
		}
	}

	private void Window_Activated(object sender, WindowActivatedEventArgs args)
	{
		if (args.WindowActivationState is Windows.UI.Core.CoreWindowActivationState.Deactivated || sender is not Window window || !windows.TryGetValue(window, out MainPage? page))
		{
			return;
		}

		activePage = page;
		mainMenuService.UpdateValidationSnapshot(this);
	}

	private void Window_Closed(object sender, WindowEventArgs args)
	{
		if (sender is not Window window || !windows.Remove(window, out MainPage? page))
		{
			return;
		}

		window.Activated -= Window_Activated;
		window.Closed -= Window_Closed;
		if (ReferenceEquals(activePage, page))
		{
			activePage = windows.Values.FirstOrDefault();
		}
		if (ReferenceEquals(MainWindow, window))
		{
			MainWindow = windows.Keys.FirstOrDefault();
		}

		if (activePage is not null)
		{
			mainMenuService.UpdateValidationSnapshot(this);
		}
		else
		{
			mainMenuService.Dispose();
			isMainMenuInstalled = false;
		}
	}

	void IMacOSMenuCommandTarget.ExecuteMenuCommand(MacOSMenuCommand command)
	{
		switch (command)
		{
			case MacOSMenuCommand.NewWindow:
				CreateWindow();
				break;
			case MacOSMenuCommand.CloseWindow when activePage is not null:
				CloseWindow(activePage);
				break;
			default:
				(activePage as IMacOSMenuCommandTarget)?.ExecuteMenuCommand(command);
				break;
		}
	}

	bool IMacOSMenuCommandTarget.CanExecuteMenuCommand(MacOSMenuCommand command)
	{
		return command switch
		{
			MacOSMenuCommand.NewWindow => true,
			MacOSMenuCommand.CloseWindow => activePage is not null,
			_ => (activePage as IMacOSMenuCommandTarget)?.CanExecuteMenuCommand(command) is true,
		};
	}

	public static void InitializeLogging()
	{
#if DEBUG
		var factory = LoggerFactory.Create(builder =>
		{
			builder.AddConsole();
			builder.SetMinimumLevel(LogLevel.Information);
			builder.AddFilter("Uno", LogLevel.Warning);
			builder.AddFilter("Microsoft", LogLevel.Warning);
		});

		global::Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = factory;
		global::Uno.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
#endif
	}
}
