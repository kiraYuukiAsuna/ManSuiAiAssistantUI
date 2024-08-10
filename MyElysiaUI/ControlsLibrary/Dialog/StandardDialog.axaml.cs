using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using SukiUI.Controls;

namespace MyElysiaUI.ControlsLibrary.Dialog;

public partial class StandardDialog : UserControl
{
    public StandardDialog(string content, string options)
    {
        InitializeComponent();
        
        TextBlockWidget.Text = content;
        ButtonWidget.Content = options;
        
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        SukiHost.CloseDialog();
    }
}