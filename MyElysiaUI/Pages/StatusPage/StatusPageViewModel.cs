using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using MyElysiaCore;
using MyElysiaRunner;
using MyElysiaUI.ControlsLibrary.Dialog;
using Newtonsoft.Json;
using Serilog;
using SukiUI.Controls;
using Path = System.IO.Path;

namespace MyElysiaUI.Pages.StatusPage;

public partial class StatusPageViewModel : PageBase
{
    public StatusPageViewModel() : base("当前运行状态", MaterialIconKind.PlayCircle, int.MinValue)
    {
        RescanConfig();
        RunningState = false;
        RunCodeProtection = false;

        RunButtonText = "点击开始运行!";

        InfoBarTitle = "当前状态：";
        InfoBarMessage = "未启动...";
        InfoBarSeverity = SukiUI.Enums.NotificationType.Info;
        StatusBrush = new SolidColorBrush(Color.FromRgb(33, 71, 192));
    }

    public void StopIfRunning()
    {
        if (RunningState)
        {
            Run();
        }
    }

    [ObservableProperty] private AvaloniaList<string> _steps;

    [ObservableProperty] private int _stepperIndex;

    [ObservableProperty] AvaloniaList<string> _modelParameterConfigList = [];
    [ObservableProperty] private string _selectedModelParameterConfig;
    [ObservableProperty] private string _selectedCharacterPresetConfig;

    [ObservableProperty] private int _caretIndex = Int32.MaxValue;


    [ObservableProperty] private SolidColorBrush _statusBrush = new SolidColorBrush(Color.FromRgb(33, 71, 192));

    partial void OnSelectedModelParameterConfigChanged(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        try
        {
            var applicationConfig = ApplicationConfig.ReadConfig(m_ApplicationConfigFilePath);

            m_CurrentModelParameter =
                ModelParameterConfig.ReadConfig(m_ModelConfigRootPath + "/" + SelectedModelParameterConfig);
            ModelTypeProperty = m_CurrentModelParameter.ModelType != ModelType.Local;

            applicationConfig.LastSelectedModelConfigFileName = SelectedModelParameterConfig;
            applicationConfig.LastSelectedModelType = m_CurrentModelParameter.ModelType;
            applicationConfig.LastSelectedTextInputMode = m_CurrentModelParameter.TextInputMode;
            ApplicationConfig.WriteConfig(m_ApplicationConfigFilePath, applicationConfig);
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
            SukiHost.ShowDialog(new StandardDialog("加载配置文件失败！没有选择有效的大语言模型配置文件!", "确定"));
        }
    }

    partial void OnSelectedCharacterPresetConfigChanged(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        try
        {
            var applicationConfig = ApplicationConfig.ReadConfig(m_ApplicationConfigFilePath);

            m_CurrentCharacterPreset =
                CharacterPreset.ReadConfig(m_CharactPresetConfigRootPath + "/" + SelectedCharacterPresetConfig);
            TextInputModeProperty = m_CurrentModelParameter.TextInputMode != TextInputMode.Text;

            applicationConfig.LastSelectedCharacterPresetConfigFileName = SelectedCharacterPresetConfig;
            ApplicationConfig.WriteConfig(m_ApplicationConfigFilePath, applicationConfig);
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
            SukiHost.ShowDialog(new StandardDialog("加载配置文件失败！没有选择有效的人物设定配置文件!", "确定"));
        }
    }

    [ObservableProperty] AvaloniaList<string> _characterPresetConfigList = [];

    private string m_ModelConfigRootPath = System.IO.Directory.GetCurrentDirectory() + "/Config/ModelConfig";

    string m_CharactPresetConfigRootPath = System.IO.Directory.GetCurrentDirectory() + "/Config/CharacterPreset";

    private string m_LocalModelRootPath = System.IO.Directory.GetCurrentDirectory() + "/Model";

    private string m_ApplicationConfigFilePath =
        System.IO.Directory.GetCurrentDirectory() + "/Config/ApplicationConfig/ApplicationConfig.json";


    [ObservableProperty] private bool _runningState;
    ModelParameterConfig m_CurrentModelParameter;
    CharacterPreset m_CurrentCharacterPreset;

    [ObservableProperty] private bool _modelTypeProperty;
    [ObservableProperty] private bool _textInputModeProperty;

    partial void OnModelTypePropertyChanged(bool value)
    {
        if (SelectedModelParameterConfig == "")
        {
            return;
        }

        var configFile = m_ModelConfigRootPath + "/" + SelectedModelParameterConfig;
        if (!File.Exists(configFile))
        {
            return;
        }

        try
        {
            var config = ModelParameterConfig.ReadConfig(configFile);
            config.ModelType = value ? ModelType.Online : ModelType.Local;
            ModelParameterConfig.WriteConfig(configFile, config);
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
            SukiHost.ShowDialog(new StandardDialog("更新配置文件失败!", "确定"));
        }
    }

    partial void OnTextInputModePropertyChanged(bool value)
    {
        if (SelectedModelParameterConfig == "")
        {
            return;
        }

        var configFile = m_ModelConfigRootPath + "/" + SelectedModelParameterConfig;
        if (!File.Exists(configFile))
        {
            return;
        }

        try
        {
            var config = ModelParameterConfig.ReadConfig(configFile);
            config.TextInputMode = value ? TextInputMode.Voice : TextInputMode.Text;
            ModelParameterConfig.WriteConfig(configFile, config);
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
            SukiHost.ShowDialog(new StandardDialog("更新配置文件失败!", "确定"));
        }
    }

    partial void OnSelectedLocalModelFileNameChanged(string value)
    {
        if (SelectedLocalModelFileName == "")
        {
            return;
        }

        var configFile = m_LocalModelRootPath + "/" + SelectedLocalModelFileName;
        if (!File.Exists(configFile))
        {
            return;
        }

        try
        {
            var applicationConfig = ApplicationConfig.ReadConfig(m_ApplicationConfigFilePath);
            applicationConfig.LastSelectedModelFileName = SelectedLocalModelFileName;
            ApplicationConfig.WriteConfig(m_ApplicationConfigFilePath, applicationConfig);
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
            SukiHost.ShowDialog(new StandardDialog("更新配置文件失败!", "确定"));
        }
    }

    [ObservableProperty] public AvaloniaList<string> _localModelFileNames = [];
    [ObservableProperty] private string _selectedLocalModelFileName;

    [RelayCommand]
    private void RescanConfig()
    {
        SelectedModelParameterConfig = "";
        SelectedCharacterPresetConfig = "";
        ModelParameterConfigList.Clear();
        CharacterPresetConfigList.Clear();

        var modelConfigFileNames = ModelParameterConfig.GetAllConfigFileNames(m_ModelConfigRootPath);
        foreach (var fileName in modelConfigFileNames)
        {
            ModelParameterConfigList.Add(fileName);
        }

        var characterConfigFileNames = CharacterPreset.GetAllConfigFileNames(m_CharactPresetConfigRootPath);
        foreach (var fileName in characterConfigFileNames)
        {
            CharacterPresetConfigList.Add(fileName);
        }

        var modelFiles = Directory.GetFiles(m_LocalModelRootPath);
        foreach (var modelFile in modelFiles)
        {
            if (Path.GetExtension(modelFile) == ".gguf")
            {
                LocalModelFileNames.Add(Path.GetFileName(modelFile));
            }
        }

        var applicationConfig = ApplicationConfig.ReadConfig(m_ApplicationConfigFilePath);

        bool foundModel = false;
        foreach (var configFileName in modelConfigFileNames)
        {
            if (applicationConfig.LastSelectedModelConfigFileName == configFileName)
            {
                SelectedModelParameterConfig = configFileName;
                foundModel = true;
                break;
            }
        }

        if (!foundModel)
        {
            if (ModelParameterConfigList.Count > 0)
            {
                SelectedModelParameterConfig = ModelParameterConfigList[0];
            }
        }

        bool foundCharacter = false;
        foreach (var configFileName in characterConfigFileNames)
        {
            if (applicationConfig.LastSelectedCharacterPresetConfigFileName == configFileName)
            {
                SelectedCharacterPresetConfig = configFileName;
                foundCharacter = true;
                break;
            }
        }

        if (!foundCharacter)
        {
            if (CharacterPresetConfigList.Count > 0)
            {
                SelectedCharacterPresetConfig = CharacterPresetConfigList[0];
            }
        }

        bool foundModelFile = false;
        foreach (var fileName in modelFiles)
        {
            if (applicationConfig.LastSelectedModelFileName == fileName)
            {
                SelectedLocalModelFileName = fileName;
                foundModelFile = true;
                break;
            }
        }

        if (!foundModelFile)
        {
            if (LocalModelFileNames.Count > 0)
            {
                SelectedLocalModelFileName = LocalModelFileNames[0];
            }
        }
    }

    private string CurrentWorkingDirectory = System.IO.Directory.GetCurrentDirectory();
    private CancellationTokenSource CurrentCancellationToken;
    private CancellationTokenSource CurrentSubCancellationToken;

    [ObservableProperty] private bool _runCodeProtection;


    // critical resources
    private BertVitsConnectionHandler bertVitsConnectionHandlerInstance;
    private Thread bertVitsConnectionDetectThread;
    private ProcessStartInfo bertvitsStartInfo;

    private VoiceInputConnectionHandler voiceConnectionHandlerInstance;
    private Thread voiceConnectionHandlerThread;
    private Thread voiceConnectionDetectThread;

    private ProcessStartInfo voiceInputServerStartInfo;
    private ProcessStartInfo voiceInputClientStartInfo;

    private Process process1;
    private Process process2;
    private Process process3;

    private LocalLlmController localLlmController;
    private OnlineLlmController onlineLlmController;

    private Thread voiceAutoInputThread;

    private Thread idleAskThread;

    private List<Process> pluginInstances = new();

    [RelayCommand]
    private void Run()
    {
        if (RunningState)
        {
            if (CurrentSubCancellationToken != null && !CurrentSubCancellationToken.IsCancellationRequested)
            {
                CurrentSubCancellationToken.Cancel();
            }

            if (CurrentCancellationToken != null && !CurrentCancellationToken.IsCancellationRequested)
            {
                CurrentCancellationToken.Cancel();
            }

            foreach (var plugin in pluginInstances)
            {
                if (plugin != null && !plugin.HasExited)
                {
                    plugin.Kill();
                }
            }

            if (process1 != null && !process1.HasExited)
            {
                process1.Kill();
            }

            if (process2 != null && !process2.HasExited)
            {
                process2.Kill();
            }

            if (process3 != null && !process3.HasExited)
            {
                process3.Kill();
            }

            Log.Information("关闭所有Python进程开始...");
            try
            {
                // 获取所有正在运行的进程
                Process[] allProcesses = Process.GetProcesses();

                // 查找所有名为 "python" 的进程
                var pythonProcesses = allProcesses.Where(p => p.ProcessName.ToLower().Contains("python"));

                // 逐个关闭这些进程
                foreach (var process in pythonProcesses)
                {
                    Console.WriteLine($"Killing process {process.ProcessName} with ID {process.Id}");
                    process.Kill();
                    process.WaitForExit(); // 等待进程完全退出
                }

                Log.Information("All Python processes have been terminated.");
            }
            catch (Exception ex)
            {
                SukiHost.ShowDialog(new StandardDialog("关闭Python进程失败! 请重新打开程序并手动检查关闭任务管理器中是否存在未关闭的Python进程！", "确定"));
                Log.Error($"An error occurred: {ex.Message}");
            }

            Log.Information("关闭所有Python进程结束...");


            RunningState = false;

            GlobalStatus.Instance.IsBertVitsConnectionEstablished = false;
            GlobalStatus.Instance.IsVoiceConnectionEstablished = false;
            GlobalStatus.Instance.IsVoiceInputServerOnline = false;
            GlobalStatus.Instance.IsVoiceInputClientOnline = false;

            RunButtonText = "点击开始运行!";

            InfoBarTitle = "当前状态：";
            InfoBarMessage = "未启动...";
            InfoBarSeverity = SukiUI.Enums.NotificationType.Info;
            UpdateStatusColor(Color.FromRgb(33, 71, 192));

            StepperIndex = 0;
            Steps = new AvaloniaList<string>();

            return;
        }

        if (SelectedModelParameterConfig == "")
        {
            SukiHost.ShowDialog(new StandardDialog("没有选择有效的大语言模型配置文件!", "确定"));
            return;
        }

        if (SelectedModelParameterConfig == "")
        {
            SukiHost.ShowDialog(new StandardDialog("没有选择有效的人物设定配置文件!", "确定"));
            return;
        }

        try
        {
            m_CurrentModelParameter =
                ModelParameterConfig.ReadConfig(m_ModelConfigRootPath + "/" + SelectedModelParameterConfig);
            m_CurrentModelParameter.ModelType = ModelTypeProperty ? ModelType.Online : ModelType.Local;
            m_CurrentModelParameter.TextInputMode = TextInputModeProperty ? TextInputMode.Voice : TextInputMode.Text;
            ModelParameterConfig.WriteConfig(m_ModelConfigRootPath + "/" + SelectedModelParameterConfig,
                m_CurrentModelParameter);
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
            SukiHost.ShowDialog(new StandardDialog("加载配置文件失败！没有选择有效的大语言模型配置文件!", "确定"));
            return;
        }

        try
        {
            m_CurrentCharacterPreset =
                CharacterPreset.ReadConfig(m_CharactPresetConfigRootPath + "/" + SelectedCharacterPresetConfig);
        }
        catch (Exception e)
        {
            Log.Error(e.Message);

            SukiHost.ShowDialog(new StandardDialog("加载配置文件失败！没有选择有效的人物设定配置文件!", "确定"));
            return;
        }


        Thread startThread = new Thread(() =>
        {
            RunCodeProtection = true;

            HistoryTextBlock = "";

            CurrentCancellationToken = new CancellationTokenSource();
            CurrentSubCancellationToken = new CancellationTokenSource();

            RunButtonText = "点击停止运行!";

            Log.Information("关闭所有Python进程开始...");
            try
            {
                // 获取所有正在运行的进程
                Process[] allProcesses = Process.GetProcesses();

                // 查找所有名为 "python" 的进程
                var pythonProcesses = allProcesses.Where(p => p.ProcessName.ToLower().Contains("python"));

                // 逐个关闭这些进程
                foreach (var process in pythonProcesses)
                {
                    Console.WriteLine($"Killing process {process.ProcessName} with ID {process.Id}");
                    process.Kill();
                    process.WaitForExit(); // 等待进程完全退出
                }

                Log.Information("All Python processes have been terminated.");
            }
            catch (Exception ex)
            {
                ShowMessageBox("关闭Python进程失败! 请重新打开程序并手动检查关闭任务管理器中是否存在未关闭的Python进程！", "确定");
                Log.Error($"An error occurred: {ex.Message}");
            }

            Log.Information("关闭所有Python进程结束...");

            Log.Information("Start.");

            InfoBarTitle = "当前状态：";
            InfoBarMessage = "正在启动...";
            InfoBarSeverity = SukiUI.Enums.NotificationType.Info;
            UpdateStatusColor(Color.FromRgb(33, 71, 192));

            if (m_CurrentModelParameter.TextInputMode == TextInputMode.Text)
            {
                Steps = new AvaloniaList<string>()
                {
                    "启动BertVits2 TTS服务", "连接大语言模型",
                };
                StepperIndex = 0;
            }
            else if (m_CurrentModelParameter.TextInputMode == TextInputMode.Voice)
            {
                Steps = new AvaloniaList<string>()
                {
                    "启动语音输入服务", "启动语音输入客户端", "启动BertVits2 TTS服务", "连接大语言模型",
                };
                StepperIndex = 0;
            }

            if (bertVitsConnectionHandlerInstance == null)
            {
                bertVitsConnectionHandlerInstance = new BertVitsConnectionHandler("http://127.0.0.1:14252");
            }

            bertVitsConnectionHandlerInstance.reloadConfig();

            bertVitsConnectionDetectThread = new Thread(() =>
            {
                while (!CurrentCancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (bertVitsConnectionHandlerInstance.sendIsVoiceServiceOnline().GetAwaiter().GetResult())
                        {
                            if (m_CurrentModelParameter.TextInputMode == TextInputMode.Text && !RunningState)
                            {
                                StepperIndex = 1;

                                InfoBarTitle = "当前状态：";
                                InfoBarMessage = "启动成功! 当前输入模式: 文本输入模式; " +
                                                 (m_CurrentModelParameter.ModelType == ModelType.Local
                                                     ? "(本地模型)"
                                                     : "(在线模型)");
                                InfoBarSeverity = SukiUI.Enums.NotificationType.Success;
                                UpdateStatusColor(Color.FromRgb(36, 192, 81));

                                idleAskThread = new Thread(() =>
                                {
                                    Log.Information("Idle ask thread started.");

                                    if (m_CurrentCharacterPreset.IdleAskMeTime >= 10)
                                    {
                                        TimeSpan timeout = TimeSpan.FromSeconds(m_CurrentCharacterPreset.IdleAskMeTime);

                                        while (!CurrentCancellationToken.IsCancellationRequested)
                                        {
                                            DateTime startTime = DateTime.Now;
                                            if (m_CurrentModelParameter.ModelType == ModelType.Local)
                                            {
                                                startTime = localLlmController.LastInferTime;
                                            }
                                            else if (m_CurrentModelParameter.ModelType == ModelType.Online)
                                            {
                                                startTime = onlineLlmController.LastInferTime;
                                            }

                                            // 检查是否已经超过了预定的最大执行时间
                                            if (DateTime.Now - startTime > timeout)
                                            {
                                                Log.Information("Idle ask thread timeout. Send idle ask message.");

                                                string idleAskMessage = m_CurrentCharacterPreset.IdleAskMeMessage;
                                                if (m_CurrentModelParameter.ModelType == ModelType.Local)
                                                {
                                                    HistoryTextBlock += "<" + m_CurrentCharacterPreset.YourName +
                                                                        ">:\n" + idleAskMessage + "\n\n";
                                                    CaretIndex = Int32.MaxValue;
                                                    try
                                                    {
                                                        localLlmController.Infer(new Message(Role.User, idleAskMessage))
                                                            .GetAwaiter().GetResult();
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        Log.Error(e.Message);
                                                        ShowMessageBox("请求大模型推理数据失败! 错误信息：" + e.Message, "确定")
                                                            .GetAwaiter().GetResult();
                                                    }

                                                    localLlmController.LastInferTime = DateTime.Now;
                                                }
                                                else if (m_CurrentModelParameter.ModelType == ModelType.Online)
                                                {
                                                    HistoryTextBlock += "<" + m_CurrentCharacterPreset.YourName +
                                                                        ">:\n" + idleAskMessage + "\n\n";
                                                    CaretIndex = Int32.MaxValue;
                                                    try
                                                    {
                                                        onlineLlmController
                                                            .Infer(new Message(Role.User, idleAskMessage))
                                                            .GetAwaiter().GetResult();
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        Log.Error(e.Message);
                                                        ShowMessageBox("请求大模型推理数据失败! 错误信息：" + e.Message, "确定")
                                                            .GetAwaiter().GetResult();
                                                    }

                                                    onlineLlmController.LastInferTime = DateTime.Now;
                                                }
                                            }

                                            Thread.Sleep(TimeSpan.FromSeconds(1));
                                        }
                                    }

                                    Log.Information("Idle ask thread stopped.");
                                });
                                idleAskThread.Start();

                                RunningState = true;
                            }
                            else if (m_CurrentModelParameter.TextInputMode == TextInputMode.Voice && !RunningState)
                            {
                                TimeSpan timeout = TimeSpan.FromSeconds(180);
                                DateTime startTime = DateTime.Now;

                                while (!CurrentSubCancellationToken.IsCancellationRequested)
                                {
                                    if (GlobalStatus.Instance.IsVoiceInputServerOnline &&
                                        GlobalStatus.Instance.IsVoiceInputClientOnline)
                                    {
                                        StepperIndex = 3;

                                        InfoBarTitle = "当前状态：";
                                        InfoBarMessage = "启动成功! 当前输入模式: 语音输入模式+文本输入模式; " +
                                                         (m_CurrentModelParameter.ModelType == ModelType.Local
                                                             ? "(本地模型)"
                                                             : "(在线模型)");
                                        InfoBarSeverity = SukiUI.Enums.NotificationType.Success;
                                        UpdateStatusColor(Color.FromRgb(36, 192, 81));

                                        idleAskThread = new Thread(() =>
                                        {
                                            Log.Information("Idle ask thread started.");

                                            if (m_CurrentCharacterPreset.IdleAskMeTime >= 10)
                                            {
                                                TimeSpan timeout =
                                                    TimeSpan.FromSeconds(m_CurrentCharacterPreset.IdleAskMeTime);

                                                while (!CurrentCancellationToken.IsCancellationRequested)
                                                {
                                                    DateTime startTime = DateTime.Now;
                                                    if (m_CurrentModelParameter.ModelType == ModelType.Local)
                                                    {
                                                        startTime = localLlmController.LastInferTime;
                                                    }
                                                    else if (m_CurrentModelParameter.ModelType == ModelType.Online)
                                                    {
                                                        startTime = onlineLlmController.LastInferTime;
                                                    }

                                                    // 检查是否已经超过了预定的最大执行时间
                                                    if (DateTime.Now - startTime > timeout)
                                                    {
                                                        Log.Information(
                                                            "Idle ask thread timeout. Send idle ask message.");

                                                        string idleAskMessage =
                                                            m_CurrentCharacterPreset.IdleAskMeMessage;
                                                        if (m_CurrentModelParameter.ModelType == ModelType.Local)
                                                        {
                                                            HistoryTextBlock += "<" +
                                                                m_CurrentCharacterPreset.YourName +
                                                                ">:\n" + idleAskMessage + "\n\n";
                                                            CaretIndex = Int32.MaxValue;
                                                            try
                                                            {
                                                                localLlmController
                                                                    .Infer(new Message(Role.User, idleAskMessage))
                                                                    .GetAwaiter().GetResult();
                                                            }
                                                            catch (Exception e)
                                                            {
                                                                Log.Error(e.Message);
                                                                ShowMessageBox("请求大模型推理数据失败! 错误信息：" + e.Message, "确定")
                                                                    .GetAwaiter().GetResult();
                                                            }

                                                            localLlmController.LastInferTime = DateTime.Now;
                                                        }
                                                        else if (m_CurrentModelParameter.ModelType == ModelType.Online)
                                                        {
                                                            HistoryTextBlock += "<" +
                                                                m_CurrentCharacterPreset.YourName +
                                                                ">:\n" + idleAskMessage + "\n\n";
                                                            CaretIndex = Int32.MaxValue;
                                                            try
                                                            {
                                                                onlineLlmController
                                                                    .Infer(new Message(Role.User, idleAskMessage))
                                                                    .GetAwaiter().GetResult();
                                                            }
                                                            catch (Exception e)
                                                            {
                                                                Log.Error(e.Message);
                                                                ShowMessageBox("请求大模型推理数据失败! 错误信息：" + e.Message, "确定")
                                                                    .GetAwaiter().GetResult();
                                                            }

                                                            onlineLlmController.LastInferTime = DateTime.Now;
                                                        }
                                                    }

                                                    Thread.Sleep(TimeSpan.FromSeconds(1));
                                                }
                                            }

                                            Log.Information("Idle ask thread stopped.");
                                        });
                                        idleAskThread.Start();

                                        RunningState = true;
                                        break;
                                    }

                                    // 检查是否已经超过了预定的最大执行时间
                                    if (DateTime.Now - startTime > timeout)
                                    {
                                        Log.Information("Timeout: voice input server or client maybe not started");
                                        ShowMessageBox("语音输入服务或客户端启动超时!", "确定");
                                        break;
                                    }
                                }
                            }

                            Util.LoggerBertVits.Information("BertVits connection established.");
                            GlobalStatus.Instance.IsBertVitsConnectionEstablished = true;
                            GlobalStatus.Instance.LastVoiceConnectionTime = DateTime.Now;
                            Thread.Sleep(TimeSpan.FromSeconds(4));
                        }
                        else
                        {
                            Util.LoggerBertVits.Information("BertVits connection not established.");
                            GlobalStatus.Instance.IsBertVitsConnectionEstablished = false;
                            Thread.Sleep(TimeSpan.FromSeconds(2));
                        }
                    }
                    catch (Exception e)
                    {
                        Util.LoggerBertVits.Error(e.Message);
                        Thread.Sleep(TimeSpan.FromSeconds(2));
                    }
                }
            });
            bertVitsConnectionDetectThread.Start();


            bertvitsStartInfo = new ProcessStartInfo
            {
                FileName = CurrentWorkingDirectory + @"\Python3.9.13\python.exe",
                Arguments = CurrentWorkingDirectory + @"\bertvits2CNExtraFix-simple-api\app.py",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = CurrentWorkingDirectory + @"\bertvits2CNExtraFix-simple-api\"
            };

            process1 = new Process { StartInfo = bertvitsStartInfo };
            if (!process1.Start())
            {
                Log.Error("Failed to start BertVits server.");
                ShowMessageBox("启动BertVits服务失败!", "确定");
            }

            // 启动一个线程来读取标准输出和错误流
            Thread outputThread1 = new Thread(() =>
            {
                try
                {
                    while (!process1.HasExited)
                    {
                        string output = process1.StandardOutput.ReadLine();
                        if (output != null)
                        {
                            Util.LoggerBertVits.Information(output);
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    // 进程已退出，捕获异常并结束线程
                }
            });
            outputThread1.Start();

            Thread errorThread1 = new Thread(() =>
            {
                try
                {
                    while (!process1.HasExited)
                    {
                        string error = process1.StandardError.ReadLine();
                        if (error != null)
                        {
                            Util.LoggerBertVits.Error(error);
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    // 进程已退出，捕获异常并结束线程
                }
            });
            errorThread1.Start();


            if (voiceConnectionHandlerInstance == null)
            {
                string[] prefixes = { "http://127.0.0.1:14251/" };
                voiceConnectionHandlerInstance =
                    new VoiceInputConnectionHandler(prefixes);
            }

            voiceConnectionHandlerThread = new Thread(() =>
            {
                try
                {
                    voiceConnectionHandlerInstance._listener.Start();
                    Log.Information("HTTP Server started. Listening for requests...");

                    while (!CurrentCancellationToken.IsCancellationRequested)
                    {
                        Log.Information("Waiting for request...");
                        HttpListenerContext context = voiceConnectionHandlerInstance._listener.GetContextAsync()
                            .GetAwaiter().GetResult();
                        voiceConnectionHandlerInstance.ProcessRequest(context).GetAwaiter().GetResult();
                        Log.Information("Received request...");
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                    ShowMessageBox("启动语音输入监听服务失败! 端口被占用！", "确定");
                }
            });

            voiceConnectionDetectThread = new Thread(() =>
            {
                while (!CurrentCancellationToken.IsCancellationRequested)
                {
                    voiceConnectionHandlerInstance.CheckVoiceClientConnection();
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            });


            if (m_CurrentModelParameter.TextInputMode == TextInputMode.Voice)
            {
                Log.Information("Start VoiceConnectionHandler handler.");

                voiceConnectionHandlerThread.Start();

                voiceConnectionDetectThread.Start();

                voiceInputServerStartInfo = new ProcessStartInfo
                {
                    FileName = CurrentWorkingDirectory + @"\Python3.9.13\python.exe",
                    Arguments = CurrentWorkingDirectory + @"\CapsWriter-Offline\core_server.py",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = CurrentWorkingDirectory + @"\CapsWriter-Offline\"
                };
                process2 = new Process { StartInfo = voiceInputServerStartInfo };
                if (!process2.Start())
                {
                    Log.Error("Failed to start voice input server.");
                    ShowMessageBox("启动语音输入服务失败!", "确定");
                }

                String testVoiceInputServer = "";
                // 启动一个线程来读取标准输出和错误流
                Thread outputThread2 = new Thread(() =>
                {
                    try
                    {
                        while (!process2.HasExited)
                        {
                            string output = process2.StandardOutput.ReadLine();
                            if (output != null)
                            {
                                testVoiceInputServer += output;
                                Util.LoggerVoiceInputServer.Information(output);
                            }
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        Log.Error("Voice input  exited.");
                    }
                });
                outputThread2.Start();

                Thread errorThread2 = new Thread(() =>
                {
                    try
                    {
                        while (!process2.HasExited)
                        {
                            string error = process2.StandardError.ReadLine();
                            if (error != null)
                            {
                                Util.LoggerVoiceInputServer.Information(error);
                            }
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        Log.Error("Voice input server exited.");
                    }
                });
                errorThread2.Start();

                voiceInputClientStartInfo = new ProcessStartInfo
                {
                    FileName = CurrentWorkingDirectory + @"\Python3.9.13\python.exe",
                    Arguments = CurrentWorkingDirectory + @"\CapsWriter-Offline\core_client.py",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = CurrentWorkingDirectory + @"\CapsWriter-Offline\"
                };

                TimeSpan timeout = TimeSpan.FromSeconds(60);
                DateTime startTime = DateTime.Now;
                while (!CurrentSubCancellationToken.IsCancellationRequested)
                {
                    if (testVoiceInputServer.Contains("开始服务"))
                    {
                        GlobalStatus.Instance.IsVoiceInputServerOnline = true;
                        StepperIndex = 1;
                        break;
                    }

                    // 检查是否已经超过了预定的最大执行时间
                    if (DateTime.Now - startTime > timeout)
                    {
                        Console.WriteLine("Timeout: Voice input server maybe not started");
                        break;
                    }

                    Log.Information("Waiting for voice input server to start.");
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }

                Log.Information("Voice input server started.");

                process3 = new Process { StartInfo = voiceInputClientStartInfo };

                if (!process3.Start())
                {
                    Log.Error("Failed to start voice input client.");
                    ShowMessageBox("启动语音输入客户端失败!", "确定");
                }

                // 启动一个线程来读取标准输出和错误流
                Thread outputThread3 = new Thread(() =>
                {
                    try
                    {
                        while (!process3.HasExited)
                        {
                            string output = process3.StandardOutput.ReadLine();
                            if (output != null)
                            {
                                Util.LoggerVoiceInputClient.Information(output);
                            }
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        Log.Error("Voice input client exited.");
                    }
                });
                outputThread3.Start();

                Thread errorThread3 = new Thread(() =>
                {
                    try
                    {
                        while (!process3.HasExited)
                        {
                            string error = process3.StandardError.ReadLine();
                            if (error != null)
                            {
                                Util.LoggerVoiceInputClient.Information(error);
                            }
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        Log.Error("Voice input client exited.");
                    }
                });
                errorThread3.Start();
            }

            if (m_CurrentModelParameter.ModelType == ModelType.Local)
            {
                Log.Information("Local model mode enabled.");

                string localModelPath = m_LocalModelRootPath + "/" + SelectedLocalModelFileName;
                Log.Information("Local model path: {0}", localModelPath);

                if (localLlmController == null)
                {
                    localLlmController = new(
                        localModelPath,
                        m_CurrentModelParameter.LocalLlmCreateInfo
                        , llmResponseCallback);
                }

                localLlmController.ReloadConfig(m_CurrentModelParameter.LocalLlmCreateInfo);
                localLlmController.LoadModel();

                try
                {
                    string historyFilePath = CurrentWorkingDirectory + "/History/ChatHistory.json";

                    ChatContent chatContent = new();
                    if (!File.Exists(historyFilePath))
                    {
                        m_CurrentCharacterPreset.ChatContent.YourName = m_CurrentCharacterPreset.YourName;
                        m_CurrentCharacterPreset.ChatContent.CharacterName = m_CurrentCharacterPreset.Name;
                        localLlmController.LoadPresetMessage(m_CurrentCharacterPreset.ChatContent);
                        chatContent = m_CurrentCharacterPreset.ChatContent;
                        chatContent.YourName = m_CurrentCharacterPreset.YourName;
                        chatContent.CharacterName = m_CurrentCharacterPreset.Name;
                    }
                    else
                    {
                        string historyJson = File.ReadAllText(historyFilePath);
                        var chatHistory = JsonConvert.DeserializeObject<ChatContent>(historyJson);
                        if (chatHistory == null)
                        {
                            ShowMessageBox("加载历史聊天记录失败! 错误信息：ChatHistory.json 文件内容为空！加载默认预设！", "确定");
                            m_CurrentCharacterPreset.ChatContent.YourName = m_CurrentCharacterPreset.YourName;
                            m_CurrentCharacterPreset.ChatContent.CharacterName = m_CurrentCharacterPreset.Name;
                            localLlmController.LoadPresetMessage(m_CurrentCharacterPreset.ChatContent);
                            chatContent = m_CurrentCharacterPreset.ChatContent;
                            chatContent.YourName = m_CurrentCharacterPreset.YourName;
                            chatContent.CharacterName = m_CurrentCharacterPreset.Name;
                        }
                        else
                        {
                            chatHistory.YourName = m_CurrentCharacterPreset.YourName;
                            chatHistory.CharacterName = m_CurrentCharacterPreset.Name;
                            localLlmController.LoadPresetMessage(chatHistory);
                            chatContent = chatHistory;
                            chatContent.YourName = m_CurrentCharacterPreset.YourName;
                            chatContent.CharacterName = m_CurrentCharacterPreset.Name;
                        }
                    }

                    foreach (var message in chatContent.Messages)
                    {
                        switch (message.Role)
                        {
                            case Role.System:
                            {
                                HistoryTextBlock += "<系统信息>:\n" + message.Content + "\n\n";
                                break;
                            }
                            case Role.Assistant:
                            {
                                HistoryTextBlock += m_CurrentCharacterPreset.Name != ""
                                    ? ("<" + m_CurrentCharacterPreset.Name + ">:")
                                    : "<(未知)>:";
                                HistoryTextBlock += "\n" + message.Content + "\n\n";
                                break;
                            }
                            case Role.User:
                            {
                                HistoryTextBlock += "<" + m_CurrentCharacterPreset.YourName + ">:\n" + message.Content +
                                                    "\n\n";
                                break;
                            }
                        }
                    }

                    CaretIndex = Int32.MaxValue;
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                    ShowMessageBox("加载历史聊天记录失败! 错误信息：" + e.Message, "确定");
                }
            }
            else if (m_CurrentModelParameter.ModelType == ModelType.Online)
            {
                Log.Information("Online model mode enabled.");

                Log.Information("Online model name: {OnlineModelName}",
                    m_CurrentModelParameter.OnlineLlmCreateInfo.OnlineModelName);
                Log.Information("Online model url: {OnlineModelUrl}",
                    m_CurrentModelParameter.OnlineLlmCreateInfo.OnlineModelUrl);
                Log.Information("Online model api key: {OnlineModelApiKey}",
                    m_CurrentModelParameter.OnlineLlmCreateInfo.OnlineModelApiKey);

                if (onlineLlmController == null)
                {
                    onlineLlmController = new(
                        new OnlineLlmCreateInfo()
                        {
                            OnlineModelName = m_CurrentModelParameter.OnlineLlmCreateInfo.OnlineModelName,
                            OnlineModelUrl = m_CurrentModelParameter.OnlineLlmCreateInfo.OnlineModelUrl,
                            OnlineModelApiKey = m_CurrentModelParameter.OnlineLlmCreateInfo.OnlineModelApiKey,
                            Temperature = m_CurrentModelParameter.OnlineLlmCreateInfo.Temperature,
                            ContextSize = m_CurrentModelParameter.OnlineLlmCreateInfo.ContextSize,
                        }, llmResponseCallback);
                }

                onlineLlmController.ReloadConfig(m_CurrentModelParameter.OnlineLlmCreateInfo);

                try
                {
                    string historyFilePath = CurrentWorkingDirectory + "/History/ChatHistory.json";

                    ChatContent chatContent = new();
                    if (!File.Exists(historyFilePath))
                    {
                        m_CurrentCharacterPreset.ChatContent.YourName = m_CurrentCharacterPreset.YourName;
                        m_CurrentCharacterPreset.ChatContent.CharacterName = m_CurrentCharacterPreset.Name;
                        onlineLlmController.LoadPresetMessage(m_CurrentCharacterPreset.ChatContent);
                        chatContent = m_CurrentCharacterPreset.ChatContent;
                        chatContent.YourName = m_CurrentCharacterPreset.YourName;
                        chatContent.CharacterName = m_CurrentCharacterPreset.Name;
                    }
                    else
                    {
                        string historyJson = File.ReadAllText(historyFilePath);
                        var chatHistory = JsonConvert.DeserializeObject<ChatContent>(historyJson);
                        if (chatHistory == null)
                        {
                            ShowMessageBox("加载历史聊天记录失败! 错误信息：ChatHistory.json 文件内容为空！加载默认预设！", "确定");
                            m_CurrentCharacterPreset.ChatContent.YourName = m_CurrentCharacterPreset.YourName;
                            m_CurrentCharacterPreset.ChatContent.CharacterName = m_CurrentCharacterPreset.Name;
                            onlineLlmController.LoadPresetMessage(m_CurrentCharacterPreset.ChatContent);
                            chatContent = m_CurrentCharacterPreset.ChatContent;
                            chatContent.YourName = m_CurrentCharacterPreset.YourName;
                            chatContent.CharacterName = m_CurrentCharacterPreset.Name;
                        }
                        else
                        {
                            chatHistory.YourName = m_CurrentCharacterPreset.YourName;
                            chatHistory.CharacterName = m_CurrentCharacterPreset.Name;
                            onlineLlmController.LoadPresetMessage(chatHistory);
                            chatContent = chatHistory;
                            chatContent.YourName = m_CurrentCharacterPreset.YourName;
                            chatContent.CharacterName = m_CurrentCharacterPreset.Name;
                        }
                    }

                    foreach (var message in chatContent.Messages)
                    {
                        switch (message.Role)
                        {
                            case Role.System:
                            {
                                HistoryTextBlock += "<系统信息>:\n" + message.Content + "\n\n";
                                break;
                            }
                            case Role.Assistant:
                            {
                                HistoryTextBlock += m_CurrentCharacterPreset.Name != ""
                                    ? ("<" + m_CurrentCharacterPreset.Name + ">:")
                                    : "<(未知)>:";
                                HistoryTextBlock += "\n" + message.Content + "\n\n";
                                break;
                            }
                            case Role.User:
                            {
                                HistoryTextBlock += "<" + m_CurrentCharacterPreset.YourName + ">:\n" + message.Content +
                                                    "\n\n";
                                break;
                            }
                        }
                    }

                    CaretIndex = Int32.MaxValue;
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                    ShowMessageBox("加载历史聊天记录失败! 错误信息：" + e.Message, "确定");
                }
            }

            voiceAutoInputThread = new Thread(() =>
            {
                if (m_CurrentModelParameter.ModelType == ModelType.Local)
                {
                    while (!CurrentCancellationToken.IsCancellationRequested)
                    {
                        lock (voiceConnectionHandlerInstance._lock)
                        {
                            if (voiceConnectionHandlerInstance.VoiceText != "")
                            {
                                HistoryTextBlock += "<" + m_CurrentCharacterPreset.YourName + ">:\n" +
                                                    voiceConnectionHandlerInstance.VoiceText + "\n\n";
                                CaretIndex = Int32.MaxValue;
                                Log.Information("Voice text: {0}", voiceConnectionHandlerInstance.VoiceText);
                                try
                                {
                                    localLlmController
                                        .Infer(new Message(Role.User, voiceConnectionHandlerInstance.VoiceText))
                                        .GetAwaiter().GetResult();
                                }
                                catch (Exception e)
                                {
                                    Log.Error(e.Message);
                                    ShowMessageBox("请求大模型推理数据失败! 错误信息：" + e.Message, "确定");
                                }

                                voiceConnectionHandlerInstance.VoiceText = "";
                            }
                        }

                        Thread.Sleep(TimeSpan.FromMilliseconds(100));
                    }
                }
                else if (m_CurrentModelParameter.ModelType == ModelType.Online)
                {
                    Log.Information("Voice input mode enabled.");
                    while (!CurrentCancellationToken.IsCancellationRequested)
                    {
                        if (voiceConnectionHandlerInstance.VoiceText != "")
                        {
                            HistoryTextBlock += "<" + m_CurrentCharacterPreset.YourName + ">:\n" +
                                                voiceConnectionHandlerInstance.VoiceText + "\n\n";
                            CaretIndex = Int32.MaxValue;
                            Log.Information("Voice text: {0}", voiceConnectionHandlerInstance.VoiceText);
                            try
                            {
                                onlineLlmController
                                    .Infer(new Message(Role.User, voiceConnectionHandlerInstance.VoiceText))
                                    .GetAwaiter().GetResult();
                            }
                            catch (Exception e)
                            {
                                Log.Error(e.Message);
                                ShowMessageBox("请求大模型推理数据失败! 错误信息：" + e.Message, "确定");
                            }

                            voiceConnectionHandlerInstance.VoiceText = "";
                        }

                        Thread.Sleep(TimeSpan.FromMilliseconds(100));
                    }
                }
            });
            voiceAutoInputThread.Start();

            if (m_CurrentModelParameter.TextInputMode == TextInputMode.Voice)
            {
                TimeSpan timeout = TimeSpan.FromSeconds(60);
                DateTime startTime = DateTime.Now;

                while (!CurrentSubCancellationToken.IsCancellationRequested)
                {
                    if (GlobalStatus.Instance.IsVoiceConnectionEstablished)
                    {
                        GlobalStatus.Instance.IsVoiceInputClientOnline = true;
                        StepperIndex = 2;
                        break;
                    }

                    // 检查是否已经超过了预定的最大执行时间
                    if (DateTime.Now - startTime > timeout)
                    {
                        Log.Information("Timeout: Voice input client maybe started");
                        break;
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }

            string voiceInputRules = CurrentWorkingDirectory + "/CapsWriter-Offline";
            File.WriteAllText(voiceInputRules + "/hot-zh.txt", m_CurrentCharacterPreset.HotZhWords);
            File.WriteAllText(voiceInputRules + "/hot-rule.txt", m_CurrentCharacterPreset.HotRules);

            // start plugins
            foreach (var pluginName in m_CurrentCharacterPreset.EnabledPlugins)
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = CurrentWorkingDirectory + @"\Python3.9.13\python.exe",
                    Arguments = CurrentWorkingDirectory + @"\Plugin\" + pluginName + @"\main.py",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = CurrentWorkingDirectory + @"\Plugin\" + pluginName
                };

                var process = new Process { StartInfo = processStartInfo };

                if (!process.Start())
                {
                    Log.Error("Failed to start plugin: " + pluginName);
                    ShowMessageBox("启动语插件 " + pluginName + " 失败!", "确定");
                }
            }

            RunCodeProtection = false;
        });

        startThread.Start();
    }

    private async Task ShowMessageBox(string message, string buttonText)
    {
        // 检查是否在UI线程上运行
        if (!Dispatcher.UIThread.CheckAccess())
        {
            // 将操作调度到UI线程
            await Dispatcher.UIThread.InvokeAsync(() => ShowMessageBox(message, buttonText));
        }
        else
        {
            // 如果已经在UI线程上，直接执行
            SukiHost.ShowDialog(new StandardDialog(message, buttonText));
        }
    }

    private async Task UpdateStatusColor(Color color)
    {
        // 检查是否在UI线程上运行
        if (!Dispatcher.UIThread.CheckAccess())
        {
            // 将操作调度到UI线程
            await Dispatcher.UIThread.InvokeAsync(() => StatusBrush = new SolidColorBrush(color));
        }
        else
        {
            // 如果已经在UI线程上，直接更新
            StatusBrush = new SolidColorBrush(color);
        }
    }

    [ObservableProperty] private string _infoBarTitle;
    [ObservableProperty] private string _infoBarMessage;
    [ObservableProperty] private SukiUI.Enums.NotificationType _infoBarSeverity;
    [ObservableProperty] private string _runButtonText;

    private void llmResponseCallback(string message)
    {
        string audioText = "";
        string processedMessage = message;

        foreach (var pattern in m_CurrentCharacterPreset.ExceptTextRegexExpression)
        {
            if (!string.IsNullOrEmpty(pattern))
            {
                processedMessage = Regex.Replace(processedMessage, pattern, match =>
                {
                    Log.Information($"Removed: {match.Value}");
                    return string.Empty;
                });
            }

            processedMessage = processedMessage == "" ? message : processedMessage;
        }

        audioText = processedMessage == "" ? message : processedMessage;

        Task.Run(async () =>
            {
                var result = await bertVitsConnectionHandlerInstance.SendHttpRequestForAudioAsync(audioText);
                if (!result.Key)
                {
                    ShowMessageBox("请求TTS服务失败! 错误信息：" + result.Value, "确定");
                }
            })
            .GetAwaiter().GetResult();

        /*if (HistoryTextBlock.Length > 8192)
        {
            HistoryTextBlock = HistoryTextBlock.Substring(HistoryTextBlock.Length - 8192);
        }*/

        HistoryTextBlock += m_CurrentCharacterPreset.Name != ""
            ? ("<" + m_CurrentCharacterPreset.Name + ">:")
            : "<(未知)>:";
        HistoryTextBlock += "\n" + message + "\n\n";
        CaretIndex = Int32.MaxValue;
    }


    [ObservableProperty] private string _historyTextBlock;
    [ObservableProperty] private string _inputTextBox;

    [RelayCommand]
    private async Task Infer()
    {
        if (!RunningState)
        {
            return;
        }

        if (string.IsNullOrEmpty(InputTextBox))
        {
            return;
        }

        if (m_CurrentModelParameter.ModelType == ModelType.Local)
        {
            HistoryTextBlock += "<" + m_CurrentCharacterPreset.YourName + ">:\n" + InputTextBox + "\n\n";
            CaretIndex = Int32.MaxValue;
            try
            {
                await localLlmController.Infer(new Message(Role.User, InputTextBox));
                InputTextBox = "";
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                SukiHost.ShowDialog(new StandardDialog("请求大模型推理数据失败! 错误信息：" + e.Message, "确定"));
            }
        }
        else if (m_CurrentModelParameter.ModelType == ModelType.Online)
        {
            HistoryTextBlock += "<" + m_CurrentCharacterPreset.YourName + ">:\n" + InputTextBox + "\n\n";
            CaretIndex = Int32.MaxValue;
            try
            {
                await onlineLlmController.Infer(new Message(Role.User, InputTextBox));
                InputTextBox = "";
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                SukiHost.ShowDialog(new StandardDialog("请求大模型推理数据失败! 错误信息：" + e.Message, "确定"));
            }
        }
        else
        {
            SukiHost.ShowDialog(new StandardDialog("配置文件中检测到未知的输入方式，请检查并修改配置文件后重新运行!", "确定"));
        }
    }


    [RelayCommand]
    private async Task ClearHistory()
    {
        HistoryTextBlock = "";

        try
        {
            File.WriteAllText(CurrentWorkingDirectory + "/History/ChatHistory.json",
                JsonConvert.SerializeObject(m_CurrentCharacterPreset.ChatContent));

            if (m_CurrentModelParameter.ModelType == ModelType.Local)
            {
                try
                {
                    string historyFilePath = CurrentWorkingDirectory + "/History/ChatHistory.json";

                    ChatContent chatContent = new();

                    m_CurrentCharacterPreset.ChatContent.YourName = m_CurrentCharacterPreset.YourName;
                    m_CurrentCharacterPreset.ChatContent.CharacterName = m_CurrentCharacterPreset.Name;
                    localLlmController.LoadPresetMessage(m_CurrentCharacterPreset.ChatContent);
                    chatContent = m_CurrentCharacterPreset.ChatContent;
                    chatContent.YourName = m_CurrentCharacterPreset.YourName;
                    chatContent.CharacterName = m_CurrentCharacterPreset.Name;

                    File.WriteAllText(historyFilePath, JsonConvert.SerializeObject(chatContent));

                    foreach (var message in chatContent.Messages)
                    {
                        switch (message.Role)
                        {
                            case Role.System:
                            {
                                HistoryTextBlock += "<系统信息>:\n" + message.Content + "\n\n";
                                break;
                            }
                            case Role.Assistant:
                            {
                                HistoryTextBlock += m_CurrentCharacterPreset.Name != ""
                                    ? ("<" + m_CurrentCharacterPreset.Name + ">:")
                                    : "<(未知)>:";
                                HistoryTextBlock += "\n" + message.Content + "\n\n";
                                break;
                            }
                            case Role.User:
                            {
                                HistoryTextBlock += "<" + m_CurrentCharacterPreset.YourName + ">:\n" + message.Content +
                                                    "\n\n";
                                break;
                            }
                        }
                    }

                    CaretIndex = Int32.MaxValue;
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                    SukiHost.ShowDialog(new StandardDialog("加载历史聊天记录失败! 错误信息：" + e.Message, "确定"));
                }
            }
            else if (m_CurrentModelParameter.ModelType == ModelType.Online)
            {
                try
                {
                    string historyFilePath = CurrentWorkingDirectory + "/History/ChatHistory.json";

                    ChatContent chatContent = new();

                    m_CurrentCharacterPreset.ChatContent.YourName = m_CurrentCharacterPreset.YourName;
                    m_CurrentCharacterPreset.ChatContent.CharacterName = m_CurrentCharacterPreset.Name;
                    onlineLlmController.LoadPresetMessage(m_CurrentCharacterPreset.ChatContent);
                    chatContent = m_CurrentCharacterPreset.ChatContent;
                    chatContent.YourName = m_CurrentCharacterPreset.YourName;
                    chatContent.CharacterName = m_CurrentCharacterPreset.Name;

                    File.WriteAllText(historyFilePath, JsonConvert.SerializeObject(chatContent));

                    foreach (var message in chatContent.Messages)
                    {
                        switch (message.Role)
                        {
                            case Role.System:
                            {
                                HistoryTextBlock += "<系统信息>:\n" + message.Content + "\n\n";
                                break;
                            }
                            case Role.Assistant:
                            {
                                HistoryTextBlock += m_CurrentCharacterPreset.Name != ""
                                    ? ("<" + m_CurrentCharacterPreset.Name + ">:")
                                    : "<(未知)>:";
                                HistoryTextBlock += "\n" + message.Content + "\n\n";
                                break;
                            }
                            case Role.User:
                            {
                                HistoryTextBlock += "<" + m_CurrentCharacterPreset.YourName + ">:\n" + message.Content +
                                                    "\n\n";
                                break;
                            }
                        }
                    }

                    CaretIndex = Int32.MaxValue;
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                    SukiHost.ShowDialog(new StandardDialog("加载历史聊天记录失败! 错误信息：" + e.Message, "确定"));
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
            SukiHost.ShowDialog(new StandardDialog("清空聊天记录失败! 错误信息：" + e.Message, "确定"));
        }
    }
}