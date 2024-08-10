using Avalonia.Controls;
using Avalonia.Interactivity;
using MyElysiaUI.Pages.StatusPage;
using SukiUI.Controls;
using SukiUI.Models;

namespace MyElysiaUI;

public partial class MainWindow : SukiWindow
{
    public MainWindow()
    {
        InitializeComponent();

        this.Closing += OnMainWindowClosing;
    }

    private void MenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (e.Source is not MenuItem mItem) return;
        if (mItem.DataContext is not SukiColorTheme cTheme) return;
        vm.ChangeTheme(cTheme);
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        foreach (var page in (DataContext as MainWindowViewModel).Pages)
        {
            switch (page)
            {
                case StatusPageViewModel statusPageViewModel:
                {
                    statusPageViewModel.StopIfRunning();
                    break;
                }
            }
        }
    }
}