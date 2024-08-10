using Newtonsoft.Json;
using OpenAI;
using Serilog;

namespace MyElysiaCore;

using OpenAI.Chat;

public class OnlineLlmController
{
    private OnlineLlmCreateInfo m_CreateInfo;
    private List<ChatMessage> m_PresetChatHistory;
    private List<ChatMessage> m_ChatHistory;
    CallbackDelegate m_CallbackDelegate;

    private string CurrentWorkingDirectory;
    private string HistoryPath;

    private string CharacterName;
    
    public DateTime LastInferTime;

    public OnlineLlmController(OnlineLlmCreateInfo createInfo, CallbackDelegate callbackDelegate)
    {
        m_CreateInfo = createInfo;
        m_PresetChatHistory = new List<ChatMessage>();
        m_ChatHistory = new List<ChatMessage>();
        m_CallbackDelegate = callbackDelegate;

        CurrentWorkingDirectory = System.IO.Directory.GetCurrentDirectory();
        HistoryPath = CurrentWorkingDirectory + "/History";

        CharacterName = "";
        LastInferTime = DateTime.Now;
    }
    
    public void ReloadConfig(OnlineLlmCreateInfo createInfo)
    {
        m_CreateInfo = createInfo;
    }

    public void LoadPresetMessage(ChatContent chatContent)
    {
        if (chatContent.Messages == null)
        {
            return;
        }

        m_PresetChatHistory.Clear();
        m_ChatHistory.Clear();

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

    public void AddMessage(ref List<ChatMessage> chatMessages, Message message)
    {
        switch (message.Role)
        {
            case Role.System:
                chatMessages.Add(new SystemChatMessage(message.Content));
                break;
            case Role.Assistant:
                chatMessages.Add(new AssistantChatMessage(message.Content));
                break;
            case Role.User:
                chatMessages.Add(new UserChatMessage(message.Content));
                break;
        }
    }

    public void ensureContextSize(Message nextMessage)
    {
        int messagesTokenSize = 0;
        foreach (var message in m_ChatHistory)
        {
            messagesTokenSize += message.Content.Count;
        }

        if (messagesTokenSize + nextMessage.Content.Length > m_CreateInfo.ContextSize)
        {
            int currentMessagesTokenSize = 0;
            foreach (var message in m_PresetChatHistory)
            {
                currentMessagesTokenSize += message.Content.Count;
            }

            if (currentMessagesTokenSize > m_CreateInfo.ContextSize)
            {
                Log.Warning(
                    "Preset messages size is larger than context size. Please adjust the context size or preset message.");
            }

            var tempChatHistory = new List<ChatMessage>();
            foreach (var message in m_PresetChatHistory)
            {
                tempChatHistory.Add(message);
            }

            int tempChatHistorySize = currentMessagesTokenSize;
            tempChatHistorySize += nextMessage.Content.Length;
            int targetIndex = -1;

            for (int i = m_ChatHistory.Count - 1; i >= 0; i--)
            {
                if (tempChatHistorySize + m_ChatHistory[i].Content.Count <= m_CreateInfo.ContextSize)
                {
                    tempChatHistorySize += m_ChatHistory[i].Content.Count;
                }
                else
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex != -1)
            {
                for (int i = targetIndex + 1; i < m_ChatHistory.Count; i++)
                {
                    if (currentMessagesTokenSize + m_ChatHistory[i].Content.Count <= m_CreateInfo.ContextSize)
                    {
                        if (i == targetIndex + 1 && m_ChatHistory[i] is AssistantChatMessage)
                        {
                            continue;
                        }

                        tempChatHistory.Add(m_ChatHistory[i]);
                        currentMessagesTokenSize += m_ChatHistory[i].Content.Count;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < m_ChatHistory.Count; i++)
                {
                    if (currentMessagesTokenSize + m_ChatHistory[i].Content.Count <= m_CreateInfo.ContextSize)
                    {
                        tempChatHistory.Add(m_ChatHistory[i]);
                        currentMessagesTokenSize += m_ChatHistory[i].Content.Count;
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

    public async Task Infer(Message userInputMessage)
    {
        LastInferTime = DateTime.Now;

        ensureContextSize(userInputMessage);
        
        var client = new ChatClient(model: m_CreateInfo.OnlineModelName,
            m_CreateInfo.OnlineModelApiKey.Length > 0 ? m_CreateInfo.OnlineModelApiKey : "123456",
            options: new OpenAIClientOptions()
            {
                Endpoint = new Uri(m_CreateInfo.OnlineModelUrl),
                NetworkTimeout = TimeSpan.FromSeconds(30)
            });

        if (!File.Exists(HistoryPath))
        {
            Directory.CreateDirectory(HistoryPath);
        }

        ChatContent chatContentLastUser = new ChatContent();
        chatContentLastUser.CharacterName = CharacterName;
        foreach (var message in m_ChatHistory)
        {
            switch (message)
            {
                case SystemChatMessage systemMessage:
                    chatContentLastUser.Messages.Add(new Message(Role.System,
                        systemMessage.Content != null && systemMessage.Content.Count > 0
                            ? systemMessage.Content[0].Text
                            : ""));
                    break;
                case AssistantChatMessage assistantMessage:
                    chatContentLastUser.Messages.Add(new Message(Role.Assistant,
                        assistantMessage.Content != null && assistantMessage.Content.Count > 0
                            ? assistantMessage.Content[0].Text
                            : ""));

                    break;
                case UserChatMessage userMessage:
                    chatContentLastUser.Messages.Add(new Message(Role.User,
                        userMessage.Content != null && userMessage.Content.Count > 0
                            ? userMessage.Content[0].Text
                            : ""));
                    break;
            }
        }

        chatContentLastUser.Messages.Add(new Message(Role.User, userInputMessage.Content));

        await File.WriteAllTextAsync(HistoryPath + "/ChatHistory.json",
            JsonConvert.SerializeObject(chatContentLastUser));

        AddMessage(ref m_ChatHistory, userInputMessage);

        ChatCompletion completion = await client.CompleteChatAsync(m_ChatHistory, new ChatCompletionOptions
        {
            Temperature = m_CreateInfo.Temperature
        });

        m_CallbackDelegate(completion.ToString());

        AddMessage(ref m_ChatHistory, new Message(Role.Assistant, completion.ToString()));

        Console.WriteLine(completion.ToString());
        // Console.WriteLine($"[ASSISTANT]: {completion}");

        if (!File.Exists(HistoryPath))
        {
            Directory.CreateDirectory(HistoryPath);
        }

        ChatContent chatContentLastAssistant = new ChatContent();
        chatContentLastAssistant.CharacterName = CharacterName;
        foreach (var message in m_ChatHistory)
        {
            switch (message)
            {
                case SystemChatMessage systemMessage:
                    chatContentLastAssistant.Messages.Add(new Message(Role.System,
                        systemMessage.Content != null && systemMessage.Content.Count > 0
                            ? systemMessage.Content[0].Text
                            : ""));
                    break;
                case AssistantChatMessage assistantMessage:
                    chatContentLastAssistant.Messages.Add(new Message(Role.Assistant,
                        assistantMessage.Content != null && assistantMessage.Content.Count > 0
                            ? assistantMessage.Content[0].Text
                            : ""));

                    break;
                case UserChatMessage userMessage:
                    chatContentLastAssistant.Messages.Add(new Message(Role.User,
                        userMessage.Content != null && userMessage.Content.Count > 0
                            ? userMessage.Content[0].Text
                            : ""));
                    break;
            }
        }

        await File.WriteAllTextAsync(HistoryPath + "/ChatHistory.json",
            JsonConvert.SerializeObject(chatContentLastAssistant));
    }

    public async Task ConsoleBlockTextModeInfer()
    {
        var client = new ChatClient(model: m_CreateInfo.OnlineModelName, m_CreateInfo.OnlineModelApiKey,
            options: new OpenAIClientOptions()
            {
                Endpoint = new Uri(m_CreateInfo.OnlineModelUrl)
            });


        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("The chat session has started.\nUser: ");
        Console.ForegroundColor = ConsoleColor.Green;
        string userInput = Console.ReadLine() ?? "";

        while (userInput != "exit")
        {
            LastInferTime = DateTime.Now;

            ensureContextSize(new Message(Role.User, userInput));
            
            AddMessage(ref m_ChatHistory, new Message(Role.User, userInput));

            ChatCompletion completion = await client.CompleteChatAsync(m_ChatHistory);

            m_CallbackDelegate(completion.ToString());

            AddMessage(ref m_ChatHistory, new Message(Role.Assistant, completion.ToString()));

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(completion.ToString());
            // Console.WriteLine($"[ASSISTANT]: {completion}");

            Console.ForegroundColor = ConsoleColor.Green;
            userInput = Console.ReadLine() ?? "";
        }
    }
}