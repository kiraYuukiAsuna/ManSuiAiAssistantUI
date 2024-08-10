using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using LLama.Common;
using Material.Icons;
using MyElysiaCore;
using MyElysiaRunner;
using MyElysiaUI.ControlsLibrary.Dialog;
using SukiUI.Controls;

namespace MyElysiaUI.Pages.CharacterPresetPage;

public partial class ExceptTextRegexExpressionsContentViewModel : ObservableObject
{
    [ObservableProperty] private string _regexExpression;
}

public partial class EnabledPluginContentViewModel : ObservableObject
{
    [ObservableProperty] private string _plugininName;
}

public partial class HotZhWordsContentViewModel : ObservableObject
{
    [ObservableProperty] private string _word;
}

public partial class HotRulesContentViewModel : ObservableObject
{
    [ObservableProperty] private string _left;
    [ObservableProperty] private string _right;
}

public partial class CharacterPresetConfigPageViewModel : PageBase
{
    public CharacterPresetConfigPageViewModel() : base("人物预设配置", MaterialIconKind.FileCog, int.MinValue)
    {
        RescanConfig();
    }

    string m_ConfigRootPath = System.IO.Directory.GetCurrentDirectory() + "/Config/CharacterPreset";

    [ObservableProperty] public AvaloniaList<string> _configFileNameList = [];
    [ObservableProperty] private string _selectedConfigFileName = "";
    [ObservableProperty] private string _newConfigFileName = "";

    [ObservableProperty] private string _configName = "";
    [ObservableProperty] private string _yourName = "";
    [ObservableProperty] private string _characterPresetTextAkaSystemPrompt = "";

    [ObservableProperty]
    private AvaloniaList<ExceptTextRegexExpressionsContentViewModel> _exceptTextRegexExpressionsDataGridContent = [];

    [ObservableProperty]
    private ExceptTextRegexExpressionsContentViewModel _selectedExceptTextRegexExpressionDataGridContent = new();

    [ObservableProperty] private AvaloniaList<EnabledPluginContentViewModel> _enabledPluginDataGridContent = [];
    [ObservableProperty] private EnabledPluginContentViewModel _selectedPluginDataGridContent = new();

    [ObservableProperty] private string _hotZhWordsDataGridContent = "";
    [ObservableProperty] private string _hotRulesContentViewModel = "";

    [ObservableProperty] private int _idleAskTime;
    [ObservableProperty] private string _idleAskMessage = "";

    private void ReloadConfig(string configPath)
    {
        if (configPath.Length == 0)
        {
            return;
        }

        if (!File.Exists(configPath))
        {
            return;
        }

        var config = CharacterPreset.ReadConfig(configPath);
        ConfigName = config.Name;
        YourName = config.YourName;

        string systemPromptMessage = "";

        foreach (var message in config.ChatContent.Messages)
        {
            systemPromptMessage += message.Content;
        }

        CharacterPresetTextAkaSystemPrompt = systemPromptMessage;

        foreach (var regexExpression in config.ExceptTextRegexExpression)
        {
            ExceptTextRegexExpressionsDataGridContent.Add(new ExceptTextRegexExpressionsContentViewModel()
            {
                RegexExpression = regexExpression
            });
        }

        if (config.ExceptTextRegexExpression.Count == 0)
        {
            ExceptTextRegexExpressionsDataGridContent.Clear();
        }

        foreach (var pluginName in config.EnabledPlugins)
        {
            EnabledPluginDataGridContent.Add(new EnabledPluginContentViewModel
            {
                PlugininName = pluginName
            });
        }

        if (config.EnabledPlugins.Count == 0)
        {
            EnabledPluginDataGridContent.Clear();
        }

        HotRulesContentViewModel = config.HotRules;
        HotZhWordsDataGridContent = config.HotZhWords;

        IdleAskTime = config.IdleAskMeTime;
        IdleAskMessage = config.IdleAskMeMessage;
    }

    [RelayCommand]
    private void RescanConfig()
    {
        SelectedConfigFileName = "";
        ConfigFileNameList.Clear();

        var configFileNames = CharacterPreset.GetAllConfigFileNames(m_ConfigRootPath);
        foreach (var fileName in configFileNames)
        {
            ConfigFileNameList.Add(fileName);
        }

        if (ConfigFileNameList.Count > 0)
        {
            SelectedConfigFileName = ConfigFileNameList[0];
        }
    }

    partial void OnSelectedConfigFileNameChanged(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        ReloadConfig(m_ConfigRootPath + "/" + value);
    }


    [RelayCommand]
    private void CreateNewConfig()
    {
        if (NewConfigFileName.Length == 0)
        {
            SukiHost.ShowDialog(new StandardDialog("请输入新配置文件名称!", "确定"));
            return;
        }

        var configPath = m_ConfigRootPath + "/" + NewConfigFileName + ".json";
        if (File.Exists(configPath))
        {
            SukiHost.ShowDialog(new StandardDialog("同名文件已存在!", "确定"));
            return;
        }

        var config = new CharacterPreset();
        CharacterPreset.WriteConfig(configPath, config);
        SukiHost.ShowDialog(new StandardDialog("创建新配置文件成功!", "确定"));
        RescanConfig();
        ReloadConfig(configPath);
    }


    [RelayCommand]
    private void DeleteConfig()
    {
        if (SelectedConfigFileName == null || SelectedConfigFileName.Length == 0)
        {
            SukiHost.ShowDialog(new StandardDialog("请选择配置文件!", "确定"));
            return;
        }

        var configPath = m_ConfigRootPath + "/" + SelectedConfigFileName;
        if (!File.Exists(configPath))
        {
            SukiHost.ShowDialog(new StandardDialog("删除成功!", "确定"));
            return;
        }

        try
        {
            File.Delete(configPath);
            SukiHost.ShowDialog(new StandardDialog("删除成功!", "确定"));
        }
        catch (Exception e)
        {
            SukiHost.ShowDialog(new StandardDialog("删除失败! 错误信息：" + e.Message, "确定"));
        }

        RescanConfig();
    }

    public static FilePickerFileType ConfigFilePickerFileType { get; set; } = new("LlmConfig")
    {
        Patterns = new[] { "*.json" },
    };

    [RelayCommand]
    private void AddNewRegexExpression()
    {
        string defaultRegexExpression = "\\（.*?\\）";
        string newRegexExpression = defaultRegexExpression;
        int i = 0;
        while (true)
        {
            bool found = false;
            foreach (var exp in ExceptTextRegexExpressionsDataGridContent)
            {
                if (exp.RegexExpression == newRegexExpression)
                {
                    found = true;
                    break;
                }
            }

            if (found)
            {
                newRegexExpression = defaultRegexExpression + "_" + i;
                i++;
            }
            else
            {
                break;
            }
        }

        ExceptTextRegexExpressionsDataGridContent.Add(new ExceptTextRegexExpressionsContentViewModel()
        {
            RegexExpression = newRegexExpression
        });
    }

    [RelayCommand]
    private void DeleteSelectedRegexExpression()
    {
        if (SelectedExceptTextRegexExpressionDataGridContent == null)
        {
            return;
        }

        ExceptTextRegexExpressionsDataGridContent.Remove(SelectedExceptTextRegexExpressionDataGridContent);
    }

    [RelayCommand]
    private void AddNewPlugin()
    {
        string defaultPluginName = "NewPluginName";
        string newPluginName = defaultPluginName;
        int i = 0;
        while (true)
        {
            bool found = false;
            foreach (var plugin in EnabledPluginDataGridContent)
            {
                if (plugin.PlugininName == newPluginName)
                {
                    found = true;
                    break;
                }
            }

            if (found)
            {
                newPluginName = defaultPluginName + "_" + i;
                i++;
            }
            else
            {
                break;
            }
        }

        EnabledPluginDataGridContent.Add(new EnabledPluginContentViewModel()
        {
            PlugininName = newPluginName
        });
    }

    [RelayCommand]
    private void DeleteSelectedPlugin()
    {
        if (SelectedPluginDataGridContent == null)
        {
            return;
        }

        EnabledPluginDataGridContent.Remove(SelectedPluginDataGridContent);
    }

    [RelayCommand]
    private async void ImportConfig()
    {
        // Get top level from the current control. Alternatively, you can use Window reference instead.
        var topLevel =
            TopLevel.GetTopLevel(((IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime)
                .MainWindow);

        // Start async operation to open the dialog.
        var files = await topLevel?.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            Title = "选择配置文件",
            AllowMultiple = false,
            FileTypeFilter = new[] { ConfigFilePickerFileType }
        });

        if (files.Count == 0)
        {
            return;
        }

        if (File.Exists(m_ConfigRootPath + "/" + files[0].Name))
        {
            SukiHost.ShowDialog(new StandardDialog("同名配置文件已经存在，导入失败!", "确定"));
            return;
        }

        File.Copy(files[0].Path.LocalPath, m_ConfigRootPath + "/" + files[0].Name, true);
        SukiHost.ShowDialog(new StandardDialog("导入配置文件： " + files[0].Path.LocalPath + " 成功!", "确定"));
        RescanConfig();
    }

    [RelayCommand]
    private async void ExportConfig()
    {
        if (SelectedConfigFileName.Length == 0)
        {
            SukiHost.ShowDialog(new StandardDialog("请选择要导出的配置文件!", "确定"));
            return;
        }

        var configPath = m_ConfigRootPath + "/" + SelectedConfigFileName;
        if (!File.Exists(configPath))
        {
            SukiHost.ShowDialog(new StandardDialog("选中的配置文件不存在!导出失败！", "确定"));
            return;
        }

        // Get top level from the current control. Alternatively, you can use Window reference instead.
        var topLevel =
            TopLevel.GetTopLevel(((IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime)
                .MainWindow);

        // Start async operation to open the dialog.
        var folders = await topLevel?.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
        {
            AllowMultiple = false
        });

        if (folders.Count == 0)
        {
            return;
        }

        File.Copy(m_ConfigRootPath + "/" + SelectedConfigFileName,
            folders[0].Path.LocalPath + "/" + Path.GetFileName((string?)SelectedConfigFileName), true);
        SukiHost.ShowDialog(new StandardDialog(
            "导入配置文件： " + SelectedConfigFileName + " 到目录 " + folders[0].Path.LocalPath + " 成功!", "确定"));
    }

    [RelayCommand]
    private void SaveConfig()
    {
        if (string.IsNullOrEmpty(SelectedConfigFileName))
        {
            SukiHost.ShowDialog(new StandardDialog("请选择配置文件!", "确定"));
            return;
        }

        List<string> enabledPlugins = new();

        foreach (var plugin in EnabledPluginDataGridContent)
        {
            enabledPlugins.Add(plugin.PlugininName);
        }

        List<string> exceptTextRegexExpressions = new();

        foreach (var regexExpression in ExceptTextRegexExpressionsDataGridContent)
        {
            exceptTextRegexExpressions.Add(regexExpression.RegexExpression);
        }

        var configPath = m_ConfigRootPath + "/" + SelectedConfigFileName;
        if (!File.Exists(configPath))
        {
            return;
        }

        var config = new CharacterPreset
        {
            Name = ConfigName,
            YourName = YourName,
            ExceptTextRegexExpression = exceptTextRegexExpressions,
            ChatContent = new()
            {
                Messages = new List<Message> { new(Role.System, CharacterPresetTextAkaSystemPrompt) }
            },
            EnabledPlugins = enabledPlugins,
            HotZhWords = HotZhWordsDataGridContent,
            HotRules = HotRulesContentViewModel,
            IdleAskMeTime = IdleAskTime,
            IdleAskMeMessage = IdleAskMessage
        };
        CharacterPreset.WriteConfig(configPath, config);
        SukiHost.ShowDialog(new StandardDialog("保存配置文件成功!", "确定"));
    }
}