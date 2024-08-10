using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MyElysiaUI.CustomTheme;

namespace MyElysiaUI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainViewModel = new MainWindowViewModel();
            var viewLocator = Current?.DataTemplates.First(x => x is ViewLocator);
            desktop.MainWindow = viewLocator.Build(mainViewModel) as Window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}