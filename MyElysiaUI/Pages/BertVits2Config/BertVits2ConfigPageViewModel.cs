using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using MyElysiaRunner;
using MyElysiaUI.ControlsLibrary.Dialog;
using SukiUI.Controls;

namespace MyElysiaUI.Pages.BertVits2Config;

public partial class BertVits2ConfigPageViewModel : PageBase
{
    private string CurrentWorkingDirectory;
    private string TTSConfigPath;

    public BertVits2ConfigPageViewModel() : base("TTS语音输出配置", MaterialIconKind.FileCog, int.MinValue)
    {
        CurrentWorkingDirectory = System.IO.Directory.GetCurrentDirectory();
        TTSConfigPath = CurrentWorkingDirectory + "/Config/ApplicationConfig/BertVits2Config.json";

        ReloadConfig();
    }

    [ObservableProperty] private int _id;
    [ObservableProperty] private string _format;
    [ObservableProperty] private string _lang;
    [ObservableProperty] private double _length;
    [ObservableProperty] private double _noise;
    [ObservableProperty] private double _noisew;
    [ObservableProperty] private double _sdpRatio;
    [ObservableProperty] private int _segmentSize;

    [RelayCommand]
    private void ReloadConfig()
    {
        BertVits2Configuration ttsConfig = BertVits2Configuration.ReadConfig(TTSConfigPath);

        Id = ttsConfig.Id;
        Format = ttsConfig.Format;
        Lang = ttsConfig.Lang;
        Length = ttsConfig.Length;
        Noise = ttsConfig.Noise;
        Noisew = ttsConfig.Noisew;
        SdpRatio = ttsConfig.SdpRatio;
        SegmentSize = ttsConfig.SegmentSize;
    }

    [RelayCommand]
    private void SaveConfig()
    {
        BertVits2Configuration ttsConfig = new BertVits2Configuration
        {
            Id = Id,
            Format = Format,
            Lang = Lang,
            Length = Length,
            Noise = Noise,
            Noisew = Noisew,
            SdpRatio = SdpRatio,
            SegmentSize = SegmentSize
        };

        BertVits2Configuration.WriteConfig(TTSConfigPath, ttsConfig);
        SukiHost.ShowDialog(new StandardDialog("保存配置文件成功!", "确定"));
    }
}