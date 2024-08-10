using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using MyElysiaCore;
using MyElysiaRunner;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/Log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

Log.Information("MyElysiaRunner Start.");

string currentWorkingDirectory = System.IO.Directory.GetCurrentDirectory();
string configFilePath = currentWorkingDirectory + "/Config/Config.json";
string characterPresetFilePath = currentWorkingDirectory + "/Config/CharacterPreset/CharacterPreset.json";
string localModelPath = currentWorkingDirectory + "/" + "LlmModel.gguf";

var characterPreset = CharacterPreset.ReadConfig(characterPresetFilePath);
var modelConfig = ModelParameterConfig.ReadConfig(configFilePath);

try
{
    Log.Information("Start BertVitsConnectionHandler handler.");
    var bertVitsConnectionHandlerInstance = new BertVitsConnectionHandler("http://127.0.0.1:14252");
    var bertVitsConnectionDetectThread = new Thread(() =>
    {
        while (true)
        {
            try
            {
                if (bertVitsConnectionHandlerInstance.sendIsVoiceServiceOnline().GetAwaiter().GetResult())
                {
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

// 启动 Python 脚本进程
    ProcessStartInfo bertvitsStartInfo = new ProcessStartInfo
    {
        FileName = currentWorkingDirectory + @"\Python3.9.13\python.exe",
        Arguments = currentWorkingDirectory + @"\bertvits2CNExtraFix-simple-api\app.py",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = false,
        WorkingDirectory = currentWorkingDirectory + @"\bertvits2CNExtraFix-simple-api\"
    };

    Process process1 = new Process { StartInfo = bertvitsStartInfo };

    if (!process1.Start())
    {
        Log.Error("Failed to start BertVits server.");
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

    string[] prefixes = { "http://127.0.0.1:14251/" };
    var cancellationToken = new CancellationTokenSource();
    var voiceConnectionHandlerInstance = new VoiceInputConnectionHandler(prefixes);
    Thread voiceConnectionHandlerThread = new Thread(() =>
    {
        voiceConnectionHandlerInstance._listener.Start();
        Log.Information("HTTP Server started. Listening for requests...");

        while (!cancellationToken.IsCancellationRequested)
        {
            Log.Information("Waiting for request...");
            HttpListenerContext context = voiceConnectionHandlerInstance._listener.GetContextAsync()
                .GetAwaiter().GetResult();
            voiceConnectionHandlerInstance.ProcessRequest(context).GetAwaiter().GetResult();
            Log.Information("Received request...");
        }
    });
    var voiceConnectionDetectThread = new Thread(() =>
    {
        while (true)
        {
            voiceConnectionHandlerInstance.CheckVoiceClientConnection();
        }
    });

    if (modelConfig.TextInputMode == TextInputMode.Voice)
    {
        Log.Information("Start VoiceConnectionHandler handler.");

        voiceConnectionHandlerThread.Start();

        voiceConnectionDetectThread.Start();

        // 启动 Python 脚本进程
        ProcessStartInfo voiceInputServerStartInfo = new ProcessStartInfo
        {
            FileName = currentWorkingDirectory + @"\Python3.9.13\python.exe",
            Arguments = currentWorkingDirectory + @"\CapsWriter-Offline\core_server.py",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = currentWorkingDirectory + @"\CapsWriter-Offline\"
        };

        Process process2 = new Process { StartInfo = voiceInputServerStartInfo };

        if (!process2.Start())
        {
            Log.Error("Failed to start voice input server.");
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
                // 进程已退出，捕获异常并结束线程
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
                // 进程已退出，捕获异常并结束线程
            }
        });
        errorThread2.Start();


        // 启动 Python 脚本进程
        ProcessStartInfo voiceInputClientStartInfo = new ProcessStartInfo
        {
            FileName = currentWorkingDirectory + @"\Python3.9.13\python.exe",
            Arguments = currentWorkingDirectory + @"\CapsWriter-Offline\core_client.py",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = currentWorkingDirectory + @"\CapsWriter-Offline\"
        };

        Process process3 = new Process { StartInfo = voiceInputClientStartInfo };

        while (true)
        {
            if (testVoiceInputServer.Contains("开始服务"))
            {
                break;
            }

            Log.Information("Waiting for voice input server to start.");
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }

        Log.Information("Voice input server started.");

        if (!process3.Start())
        {
            Log.Error("Failed to start voice input client.");
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
                // 进程已退出，捕获异常并结束线程
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
                // 进程已退出，捕获异常并结束线程
            }
        });
        errorThread3.Start();
    }


    void llmResponseCallback(string message)
    {
        string audioText = "";
        string processedMessage = message;

        foreach (var pattern in characterPreset.ExceptTextRegexExpression)
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

        Task.Run(async () => { await bertVitsConnectionHandlerInstance.SendHttpRequestForAudioAsync(audioText); })
            .GetAwaiter().GetResult();
    }


    if (modelConfig.ModelType == ModelType.Local)
    {
        Log.Information("Local model mode enabled.");

        Log.Information("Local model path: {0}", localModelPath);
        LocalLlmController controller = new(
            localModelPath,
            new LocalLlmCreateInfo()
            {
                ContextSize = modelConfig.LocalLlmCreateInfo.ContextSize,
                NGpuLayers = modelConfig.LocalLlmCreateInfo.NGpuLayers,
            }, llmResponseCallback);

        controller.LoadModel();
        characterPreset.ChatContent.YourName = characterPreset.YourName;
        characterPreset.ChatContent.CharacterName = characterPreset.Name;
        controller.LoadPresetMessage(characterPreset.ChatContent);

        if (modelConfig.TextInputMode == TextInputMode.Text)
        {
            Log.Information("Text input mode enabled.");
            await controller.ConsoleBlockTextModeInfer();
        }
        else if (modelConfig.TextInputMode == TextInputMode.Voice)
        {
            Log.Information("Voice input mode enabled.");
            while (true)
            {
                if (voiceConnectionHandlerInstance.VoiceText != "")
                {
                    Log.Information("Voice text: {0}", voiceConnectionHandlerInstance.VoiceText);
                    voiceConnectionHandlerInstance.VoiceText = "";
                    await controller.Infer(new Message(Role.User, voiceConnectionHandlerInstance.VoiceText));
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            }
        }
    }
    else if (modelConfig.ModelType == ModelType.Online)
    {
        Log.Information("Online model mode enabled.");

        Log.Information("Online model name: {OnlineModelName}", modelConfig.OnlineLlmCreateInfo.OnlineModelName);
        Log.Information("Online model url: {OnlineModelUrl}", modelConfig.OnlineLlmCreateInfo.OnlineModelUrl);
        Log.Information("Online model api key: {OnlineModelApiKey}", modelConfig.OnlineLlmCreateInfo.OnlineModelApiKey);

        OnlineLlmController controller = new(
            new OnlineLlmCreateInfo()
            {
                OnlineModelName = modelConfig.OnlineLlmCreateInfo.OnlineModelName,
                OnlineModelUrl = modelConfig.OnlineLlmCreateInfo.OnlineModelUrl,
                OnlineModelApiKey = modelConfig.OnlineLlmCreateInfo.OnlineModelApiKey
            }, llmResponseCallback);
        characterPreset.ChatContent.YourName = characterPreset.YourName;
        characterPreset.ChatContent.CharacterName = characterPreset.Name;
        controller.LoadPresetMessage(characterPreset.ChatContent);

        if (modelConfig.TextInputMode == TextInputMode.Text)
        {
            Log.Information("Text input mode enabled.");
            await controller.ConsoleBlockTextModeInfer();
        }
        else if (modelConfig.TextInputMode == TextInputMode.Voice)
        {
            Log.Information("Voice input mode enabled.");
            while (true)
            {
                if (voiceConnectionHandlerInstance.VoiceText != "")
                {
                    Log.Information("Voice text: {0}", voiceConnectionHandlerInstance.VoiceText);
                    voiceConnectionHandlerInstance.VoiceText = "";
                    await controller.Infer(new Message(Role.User, voiceConnectionHandlerInstance.VoiceText));
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            }
        }
    }
}
catch (Exception e)
{
    Log.Error(
        $"An error occurred: {e.Message}\n" +
        $"Stack trace: {e.StackTrace}"
    );
}