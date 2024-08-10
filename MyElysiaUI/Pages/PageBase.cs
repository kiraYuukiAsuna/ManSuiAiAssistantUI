using CommunityToolkit.Mvvm.ComponentModel;
using Material.Icons;

namespace MyElysiaUI.Pages;

public abstract partial class PageBase(string displayName, MaterialIconKind icon, int index = 0) : ObservableValidator
{
    [ObservableProperty] private string _displayName = displayName;
    [ObservableProperty] private MaterialIconKind _icon = icon;
    [ObservableProperty] private int _index = index;
}