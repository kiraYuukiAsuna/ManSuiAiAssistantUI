using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using MyElysiaUI.CustomTheme;
using MyElysiaUI.Pages;
using MyElysiaUI.Pages.About;
using MyElysiaUI.Pages.ExtraConfig;
using MyElysiaUI.Pages.HomePage;
using MyElysiaUI.Pages.LlmConfig;
using MyElysiaUI.Pages.StatusPage;
using MyElysiaUI.Pages.VoiceInputConfig;
using SukiUI;
using SukiUI.Controls;
using SukiUI.Models;
using BertVits2ConfigPageViewModel = MyElysiaUI.Pages.BertVits2Config.BertVits2ConfigPageViewModel;
using CharacterPresetConfigPageViewModel = MyElysiaUI.Pages.CharacterPresetPage.CharacterPresetConfigPageViewModel;

namespace MyElysiaUI;

public class ViewLocator : IDataTemplate
{
    private readonly Dictionary<object, Control> _controlCache = new();

    public Control Build(object? data)
    {
        var fullName = data?.GetType().FullName;
        if (fullName is null)
        {
            return new TextBlock { Text = "Data is null or has no name." };
        }

        var name = fullName.Replace("ViewModel", "");
        var type = Type.GetType(name);
        if (type is null)
        {
            return new TextBlock { Text = $"No View For {name}." };
        }

        if (!_controlCache.TryGetValue(data!, out var res))
        {
            res ??= (Control)Activator.CreateInstance(type)!;
            _controlCache[data!] = res;
        }

        res.DataContext = data;
        return res;
    }

    public bool Match(object? data) => data is INotifyPropertyChanged;
}

public class PageNavigationService
{
    public Action<Type>? NavigationRequested { get; set; }

    public void RequestNavigation<T>() where T : PageBase
    {
        NavigationRequested?.Invoke(typeof(T));
    }
}

public partial class MainWindowViewModel : PageBase
{
    public MainWindowViewModel() : base("满穗AI助手", MaterialIconKind.Home)
    {
        _theme = SukiTheme.GetInstance();

        Pages = new AvaloniaList<PageBase>
        {
            new HomePageViewModel(),
            new StatusPageViewModel(),
            new CharacterPresetConfigPageViewModel(),
            new LlmConfigPageViewModel(),
            new BertVits2ConfigPageViewModel(),
            new VoiceInputConfigPageViewModel(),
            new AboutPageViewModel(),
            new ExtraConfigPageViewModel()
        };
        _activePage = Pages[0];

        _pageNavigationService.NavigationRequested += pageType =>
        {
            var page = Pages.FirstOrDefault(x => x.GetType() == pageType);
            if (page is null || ActivePage?.GetType() == pageType) return;
            ActivePage = page;
        };

        Themes = _theme.ColorThemes;
        BaseTheme = _theme.ActiveBaseTheme;

        // Subscribe to the base theme changed events
        _theme.OnBaseThemeChanged += variant =>
        {
            BaseTheme = variant;
            SukiHost.ShowToast("Successfully Changed Theme", $"Changed Theme To {variant}");
        };

        // Subscribe to the color theme changed events
        _theme.OnColorThemeChanged += theme =>
            SukiHost.ShowToast("Successfully Changed Color", $"Changed Color To {theme.DisplayName}.");

        // Subscribe to the background animation changed events
        _theme.OnBackgroundAnimationChanged +=
            value => AnimationsEnabled = value;
    }

    public static void OpenUrlInternal(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo(url.Replace("&", "^&")) { UseShellExecute = true });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Process.Start("xdg-open", url);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", url);
    }

    [RelayCommand]
    private static void OpenUrl(string url) => OpenUrlInternal(url);

    public IAvaloniaReadOnlyList<PageBase> Pages { get; }
    [ObservableProperty] private PageBase? _activePage;

    PageNavigationService _pageNavigationService = new();

    public IAvaloniaReadOnlyList<SukiColorTheme> Themes { get; }

    [ObservableProperty] private ThemeVariant _baseTheme;
    [ObservableProperty] private bool _animationsEnabled;
    [ObservableProperty] private bool _titleBarVisible = true;

    private readonly SukiTheme _theme;

    [RelayCommand]
    private Task ToggleAnimations()
    {
        AnimationsEnabled = !AnimationsEnabled;
        _theme.SetBackgroundAnimationsEnabled(AnimationsEnabled);
        var title = AnimationsEnabled ? "Animation Enabled" : "Animation Disabled";
        var content = AnimationsEnabled
            ? "Background animations are now enabled."
            : "Background animations are now disabled.";
        return SukiHost.ShowToast(title, content);
    }

    [RelayCommand]
    private void ToggleBaseTheme() =>
        _theme.SwitchBaseTheme();

    public void ChangeTheme(SukiColorTheme theme) =>
        _theme.ChangeColorTheme(theme);

    [RelayCommand]
    private void CreateCustomTheme() =>
        SukiHost.ShowDialog(new CustomThemeDialogViewModel(_theme), allowBackgroundClose: true);
}