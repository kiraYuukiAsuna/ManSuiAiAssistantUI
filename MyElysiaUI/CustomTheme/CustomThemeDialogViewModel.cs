using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using MyElysiaUI.Pages;
using SukiUI;
using SukiUI.Controls;
using SukiUI.Models;

namespace MyElysiaUI.CustomTheme;

public partial class CustomThemeDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _displayName = "Pink";
    [ObservableProperty] private Color _primaryColor = Colors.DeepPink;
    [ObservableProperty] private Color _accentColor = Colors.Pink;

    [RelayCommand]
    private void TryCreateTheme()
    {
        if (string.IsNullOrEmpty(DisplayName)) return;
        var theme1 = new SukiColorTheme(DisplayName, PrimaryColor, AccentColor);
        m_Theme.AddColorTheme(theme1);
        m_Theme.ChangeColorTheme(theme1);
        SukiHost.CloseDialog();
    }

    private SukiTheme m_Theme;

    public CustomThemeDialogViewModel(SukiTheme theme)
    {
        m_Theme = theme;
    }
}