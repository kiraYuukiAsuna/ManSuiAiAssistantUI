namespace MyElysiaCore;

public class LocalLlmCreateInfo
{
    public UInt32 ContextSize;
    public float Temperature;
    public Int32 NGpuLayers;
    public UInt32 Seed;
    public bool UseMemoryLock;
    public UInt32 BatchThreads;
    public UInt32 Threads;
    public UInt32 BatchSize;
    public bool FlashAttention;

    public LocalLlmCreateInfo() : this(2048, 0.7f, 0, 114514, true, Convert.ToUInt32(Environment.ProcessorCount),
        Convert.ToUInt32(Environment.ProcessorCount), 512, true)
    {
    }

    public LocalLlmCreateInfo(UInt32 contextSize, float temperature, Int32 nGpuLayers, UInt32 seed, bool useMemoryLock,
        UInt32 batchThreads, UInt32 threads, UInt32 batchSize, bool flashAttention)
    {
        ContextSize = contextSize;
        Temperature = temperature;
        NGpuLayers = nGpuLayers;
        Seed = seed;
        UseMemoryLock = useMemoryLock;
        BatchThreads = batchThreads;
        Threads = threads;
        BatchSize = batchSize;
        FlashAttention = flashAttention;
    }
}

public class OnlineLlmCreateInfo
{
    public float Temperature;
    public int ContextSize;
    public string OnlineModelName;
    public string OnlineModelUrl;
    public string OnlineModelApiKey;

    public OnlineLlmCreateInfo() : this(0.7f, 2048, "gpt-4o", "https://api.openai.com", "")
    {
    }

    public OnlineLlmCreateInfo(float temperature, int contextSize, string onlineModelName, string onlineModelUrl,
        string onlineModelApiKey)
    {
        Temperature = temperature;
        ContextSize = contextSize;
        OnlineModelName = onlineModelName;
        OnlineModelUrl = onlineModelUrl;
        OnlineModelApiKey = onlineModelApiKey;
    }
}

public enum Role
{
    System,
    Assistant,
    User
}

public class Message
{
    public Role Role;
    public String Content;

    public Message(Role role, String content)
    {
        Role = role;
        Content = content;
    }
}

public class ChatContent
{
    public string CharacterName;
    public string YourName;
    public List<Message> Messages;

    public ChatContent()
    {
        CharacterName = "未知";
        YourName = "未知";
        Messages = new List<Message>();
    }
}

public delegate void CallbackDelegate(string message);