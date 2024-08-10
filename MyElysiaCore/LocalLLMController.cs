using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using LLama.Common;
using LLama;
using LLama.Abstractions;
using LLama.Native;
using LLama.Sampling;
using Serilog;

namespace MyElysiaCore;

public class LocalLlmController
{
    private String m_ModelFilePath;
    private LocalLlmCreateInfo m_CreateInfo;
    private LLamaWeights m_LLamaWeights;
    private LLamaContext m_LLamaContext;
    private InteractiveExecutor m_Executor;
    private ChatHistory m_PresetChatHistory;
    private ChatHistory m_ChatHistory;
    CallbackDelegate m_CallbackDelegate;

    private string CurrentWorkingDirectory;
    private string HistoryPath;

    private string CharacterName;

    public DateTime LastInferTime;

    public LocalLlmController(String modelFilePath, LocalLlmCreateInfo createInfo, CallbackDelegate callbackDelegate)
    {
        m_ModelFilePath = modelFilePath;
        m_CreateInfo = createInfo;
        m_CallbackDelegate = callbackDelegate;

        // auto load native library
        var nativeLibraryCuda12 = System.IO.Directory.GetCurrentDirectory() + "/" +
                                  "runtimes/win-x64/native/cuda12/llama.dll";
        var nativeLibraryVulkan = System.IO.Directory.GetCurrentDirectory() + "/" +
                                  "runtimes/win-x64/native/vulkan/llama.dll";
        var nativeLibraryCpu = System.IO.Directory.GetCurrentDirectory() + "/" +
                               "runtimes/win-x64/native/cpu/llama.dll";
        if (NativeLibrary.TryLoad(nativeLibraryCuda12, out var _))
        {
            Log.Information("Load cuda12 llama.dll successfully");
        }
        else if (NativeLibrary.TryLoad(nativeLibraryVulkan, out var _))
        {
            Log.Information("Load vulkan llama.dll successfully");
        }
        else if (NativeLibrary.TryLoad(nativeLibraryCpu, out var _))
        {
            Log.Information("Load cpu llama.dll successfully");
        }

        NativeLibraryConfig.Instance.WithLogCallback(delegate(LLamaLogLevel level, string message)
        {
            Log.Information($"{level}: {message}");
        });

        // NativeLibraryConfig.LLama.WithLibrary(
        //    System.IO.Directory.GetCurrentDirectory() + "/" + "runtimes/win-x64/native/cuda12/llama.dll");

        CurrentWorkingDirectory = System.IO.Directory.GetCurrentDirectory();
        HistoryPath = CurrentWorkingDirectory + "/History";

        CharacterName = "";

        LastInferTime = DateTime.Now;
    }


    public void LoadModel()
    {
        var parameters = new ModelParams(m_ModelFilePath)
        {
            ContextSize = m_CreateInfo.ContextSize, // The longest length of chat as memory.
            GpuLayerCount =
                m_CreateInfo
                    .NGpuLayers, // How many layers to offload to GPU. Please adjust it according to your GPU memory.
            Seed = m_CreateInfo.Seed,
            UseMemoryLock = m_CreateInfo.UseMemoryLock,
            BatchThreads = m_CreateInfo.BatchThreads,
            Threads = m_CreateInfo.Threads,
            BatchSize = m_CreateInfo.BatchSize,
            FlashAttention = m_CreateInfo.FlashAttention,
            Encoding = Encoding.UTF8
        };
        Console.WriteLine("parameters.Threads: {0}", parameters.Threads);
        m_LLamaWeights = LLamaWeights.LoadFromFile(parameters);
        m_LLamaContext = m_LLamaWeights.CreateContext(parameters);
        m_Executor = new InteractiveExecutor(m_LLamaContext);
        m_ChatHistory = new ChatHistory();
        m_PresetChatHistory = new ChatHistory();
    }

    public void ReloadConfig(LocalLlmCreateInfo createInfo)
    {
        m_CreateInfo = createInfo;
    }

    public void LoadPresetMessage(ChatContent chatContent)
    {
        if (chatContent.Messages == null)
        {
            return;
        }

        m_PresetChatHistory.Messages.Clear();
        m_ChatHistory.Messages.Clear();

        foreach (var message in chatContent.Messages)
        {
            AddMessage(ref m_PresetChatHistory, message);
        }

        foreach (var message in chatContent.Messages)
        {
            AddMessage(ref m_ChatHistory, message);
        }

        CharacterName = chatContent.CharacterName;
    }

    public void AddMessage(ref ChatHistory history, Message message)
    {
        switch (message.Role)
        {
            case Role.System:
                history.AddMessage(AuthorRole.System, message.Content);
                break;
            case Role.Assistant:
                history.AddMessage(AuthorRole.Assistant, message.Content);
                break;
            case Role.User:
                history.AddMessage(AuthorRole.User, message.Content);
                break;
        }
    }

    public void ensureContextSize(Message nextMessage)
    {
        int messagesTokenSize = 0;
        foreach (var message in m_ChatHistory.Messages)
        {
            messagesTokenSize += message.Content.Length;
        }

        if (messagesTokenSize + nextMessage.Content.Length > m_CreateInfo.ContextSize)
        {
            int currentMessagesTokenSize = 0;
            foreach (var message in m_PresetChatHistory.Messages)
            {
                currentMessagesTokenSize += message.Content.Length;
            }

            if (currentMessagesTokenSize > m_CreateInfo.ContextSize)
            {
                Log.Warning(
                    "Preset messages size is larger than context size. Please adjust the context size or preset message.");
            }

            var tempChatHistory = new ChatHistory();
            foreach (var message in m_PresetChatHistory.Messages)
            {
                tempChatHistory.AddMessage(message.AuthorRole, message.Content);
            }

            int tempChatHistorySize = currentMessagesTokenSize;
            tempChatHistorySize += nextMessage.Content.Length;
            int targetIndex = -1;

            for (int i = m_ChatHistory.Messages.Count - 1; i >= 0; i--)
            {
                if (tempChatHistorySize + m_ChatHistory.Messages[i].Content.Length <= m_CreateInfo.ContextSize)
                {
                    tempChatHistorySize += m_ChatHistory.Messages[i].Content.Length;
                }
                else
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex != -1)
            {
                for (int i = targetIndex + 1; i < m_ChatHistory.Messages.Count; i++)
                {
                    if (currentMessagesTokenSize + m_ChatHistory.Messages[i].Content.Length <= m_CreateInfo.ContextSize)
                    {
                        if (i == targetIndex + 1 && m_ChatHistory.Messages[i].AuthorRole == AuthorRole.Assistant)
                        {
                            continue;
                        }

                        tempChatHistory.AddMessage(m_ChatHistory.Messages[i].AuthorRole,
                            m_ChatHistory.Messages[i].Content);
                        currentMessagesTokenSize += m_ChatHistory.Messages[i].Content.Length;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < m_ChatHistory.Messages.Count; i++)
                {
                    if (currentMessagesTokenSize + m_ChatHistory.Messages[i].Content.Length <= m_CreateInfo.ContextSize)
                    {
                        tempChatHistory.AddMessage(m_ChatHistory.Messages[i].AuthorRole,
                            m_ChatHistory.Messages[i].Content);
                        currentMessagesTokenSize += m_ChatHistory.Messages[i].Content.Length;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            m_ChatHistory = tempChatHistory;
        }
    }

    class QwenHistoryTransform : IHistoryTransform
    {
        private const string StartHeaderId = "<|im_start|>";
        private const string EndHeaderId = "<|im_end|>";
        private const string Bos = "<|im_start|>";
        private const string Eos = "<|im_end|>";
        private const string EndofTurn = "<|endoftext|>";

        /// <summary>
        /// Convert a ChatHistory instance to plain text.
        /// </summary>
        /// <param name="history">The ChatHistory instance</param>
        /// <returns></returns>
        public string HistoryToText(ChatHistory history)
        {
            StringBuilder sb = new StringBuilder(1024);

            foreach (var message in history.Messages)
            {
                BuildMessage(sb, GetRoleName(message.AuthorRole), message.Content);
            }

            return sb.ToString();
        }

        private string GetRoleName(AuthorRole authorRole)
        {
            switch (authorRole)
            {
                case AuthorRole.User:
                    return "user";
                case AuthorRole.Assistant:
                    return "assistant";
                case AuthorRole.System:
                    return "system";
                default:
                    // 这里需要考虑如何设置自定义用户的角色的输入参数情况
                    throw new Exception("Unsupported role: " + authorRole);
            }
        }

        private static void BuildMessage(StringBuilder sb, string roleName, string message)
        {
            sb.Append(Bos);
            sb.Append(roleName);
            sb.Append("\n");
            sb.Append(message);
            sb.Append(Eos);
            sb.Append("\n");
        }

        /// <summary>
        /// Converts plain text to a ChatHistory instance.
        /// </summary>
        /// <param name="role">The role for the author.</param>
        /// <param name="text">The chat history as plain text.</param>
        /// <returns>The updated history.</returns>
        public ChatHistory TextToHistory(AuthorRole role, string text)
        {
            return new ChatHistory(new ChatHistory.Message[] { new ChatHistory.Message(role, text) });
        }

        /// <summary>
        /// Copy the transform.
        /// </summary>
        /// <returns></returns>
        public IHistoryTransform Clone()
        {
            return new QwenHistoryTransform();
        }
    }

    public async Task Infer(Message userInputMessage)
    {
        LastInferTime = DateTime.Now;

        ensureContextSize(userInputMessage);

        var session = new ChatSession(m_Executor, m_ChatHistory);
        session.WithHistoryTransform(new QwenHistoryTransform());
        session.WithOutputTransform(new LLamaTransforms.KeywordTextOutputStreamTransform(
            new List<string>()
            {
                "<|im_end|>", "<|endoftext|>", "assistant:", "user:", "system:", "Assistant:", "User:", "System:",
                "output:", "assistant", "user", "system", "Assistant", "User", "System", "output"
            },
            0, false
        ));


        var samplingPipeline = new DefaultSamplingPipeline();
        samplingPipeline.Temperature = m_CreateInfo.Temperature;
        InferenceParams inferenceParams = new InferenceParams()
        {
            MaxTokens = -1, // No more than x tokens should appear in answer. Remove it if antiprompt is enough for control.
            AntiPrompts = new List<string>
            {
                "<|im_end|>"
            }, // Stop generation once antiprompts appear.
            SamplingPipeline = samplingPipeline,
        };

        if (!File.Exists(HistoryPath))
        {
            Directory.CreateDirectory(HistoryPath);
        }

        ChatContent chatContentLastUser = new ChatContent();
        chatContentLastUser.CharacterName = CharacterName;
        foreach (var message in m_ChatHistory.Messages)
        {
            switch (message.AuthorRole)
            {
                case AuthorRole.System:
                    chatContentLastUser.Messages.Add(new Message(Role.System, message.Content));
                    break;
                case AuthorRole.Assistant:
                    chatContentLastUser.Messages.Add(new Message(Role.Assistant, message.Content));

                    break;
                case AuthorRole.User:
                    chatContentLastUser.Messages.Add(new Message(Role.User, message.Content));
                    break;
            }
        }

        chatContentLastUser.Messages.Add(new Message(Role.User, userInputMessage.Content));

        await File.WriteAllTextAsync(HistoryPath + "/ChatHistory.json",
            JsonConvert.SerializeObject(chatContentLastUser));

        String fullText = "";
        // Generate the response streamingly.
        await foreach (
            var text
            in session.ChatAsync(
                new ChatHistory.Message(AuthorRole.User, userInputMessage.Content),
                inferenceParams))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            fullText += text;
            Console.Write(text);
        }

        if (!File.Exists(HistoryPath))
        {
            Directory.CreateDirectory(HistoryPath);
        }

        ChatContent chatContentLastAssistant = new ChatContent();
        chatContentLastAssistant.CharacterName = CharacterName;
        foreach (var message in m_ChatHistory.Messages)
        {
            switch (message.AuthorRole)
            {
                case AuthorRole.System:
                    chatContentLastAssistant.Messages.Add(new Message(Role.System, message.Content));
                    break;
                case AuthorRole.Assistant:
                    chatContentLastAssistant.Messages.Add(new Message(Role.Assistant, message.Content));

                    break;
                case AuthorRole.User:
                    chatContentLastAssistant.Messages.Add(new Message(Role.User, message.Content));
                    break;
            }
        }

        await File.WriteAllTextAsync(HistoryPath + "/ChatHistory.json",
            JsonConvert.SerializeObject(chatContentLastAssistant));

        m_CallbackDelegate(fullText);
    }

    public async Task ConsoleBlockTextModeInfer()
    {
        ChatSession session = new(m_Executor, m_ChatHistory);
        session.WithHistoryTransform(new QwenHistoryTransform());

        var samplingPipeline = new DefaultSamplingPipeline();
        samplingPipeline.Temperature = m_CreateInfo.Temperature;
        InferenceParams inferenceParams = new InferenceParams()
        {
            MaxTokens = -1, // No more than 256 tokens should appear in answer. Remove it if antiprompt is enough for control.
            AntiPrompts = new List<string>
            {
                "<|im_end|>",
            }, // Stop generation once antiprompts appear.
            SamplingPipeline = samplingPipeline
        };

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("The chat session has started.\nUser: ");
        Console.ForegroundColor = ConsoleColor.Green;
        string userInput = Console.ReadLine() ?? "";

        while (userInput != "exit")
        {
            LastInferTime = DateTime.Now;

            ensureContextSize(new Message(Role.User, userInput));

            String fullText = "";
            // Generate the response streamingly.
            await foreach (
                var text
                in session.ChatAsync(
                    new ChatHistory.Message(AuthorRole.User, userInput),
                    inferenceParams))
            {
                Console.ForegroundColor = ConsoleColor.White;
                fullText += text;
                Console.Write(text);
            }

            m_CallbackDelegate(fullText);

            Console.ForegroundColor = ConsoleColor.Green;
            userInput = Console.ReadLine() ?? "";
        }
    }
}