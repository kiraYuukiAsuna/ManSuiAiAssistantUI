using Newtonsoft.Json;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MyElysiaRunner;

using MyElysiaCore;

public enum ModelType
{
    Local,
    Online
}

public enum TextInputMode
{
    Text,
    Voice,
}

public struct ModelParameterConfig
{
    public string Name { get; set; }
    public LocalLlmCreateInfo LocalLlmCreateInfo { get; set; }
    public ModelType ModelType { get; set; }
    public OnlineLlmCreateInfo OnlineLlmCreateInfo { get; set; }
    public TextInputMode TextInputMode { get; set; }

    public ModelParameterConfig()
    {
        Name = "默认名称";
        LocalLlmCreateInfo = new LocalLlmCreateInfo();
        ModelType = ModelType.Local;
        OnlineLlmCreateInfo = new OnlineLlmCreateInfo();
        TextInputMode = TextInputMode.Text;
    }

    public static ModelParameterConfig ReadConfig(string configFilePath)
    {
        if (!File.Exists(configFilePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configFilePath));
            Log.Information("Config file not found. Creating a new one.");
            var configManager = new ModelParameterConfig();
            var json = JsonConvert.SerializeObject(configManager);
            File.WriteAllText(configFilePath, json);
            return configManager;
        }

        var config = JsonConvert.DeserializeObject<ModelParameterConfig>(File.ReadAllText(configFilePath));
        File.WriteAllText(configFilePath, JsonConvert.SerializeObject(config));
        Log.Information("Read config info: {@ConfigManager}", config);

        return config;
    }

    public static void WriteConfig(string configFilePath, ModelParameterConfig modelParameterConfig)
    {
        File.WriteAllText(configFilePath, JsonConvert.SerializeObject(modelParameterConfig));
        Log.Information("Write config info to file: {@ConfigManager}", modelParameterConfig);
    }

    public static List<string> GetAllConfigFileNames(string configRootPath)
    {
        if (Directory.Exists(configRootPath) == false)
        {
            Directory.CreateDirectory(configRootPath);
        }

        var files = Directory.GetFiles(configRootPath);
        List<string> configFileNames = new();
        foreach (var file in files)
        {
            if (Path.GetExtension(file) == ".json")
            {
                try
                {
                    ModelParameterConfig.ReadConfig(file);
                    configFileNames.Add(Path.GetRelativePath(configRootPath, file));
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                }
            }
        }

        return configFileNames;
    }
}

public struct CharacterPreset
{
    public string Name { get; set; }

    public string YourName { get; set; }

    public ChatContent ChatContent { get; set; }
    public List<string> ExceptTextRegexExpression { get; set; }
    public List<string> EnabledPlugins { get; set; }
    public string HotZhWords { get; set; }
    public string HotRules { get; set; }

    public int IdleAskMeTime { get; set; }
    public string IdleAskMeMessage { get; set; }

    public CharacterPreset()
    {
        Name = "";
        YourName = "";
        ChatContent = new ChatContent();
        ExceptTextRegexExpression = new List<string>();
        EnabledPlugins = new List<string>();
        HotZhWords = "";
        HotRules = "";
        IdleAskMeTime = -1;
        IdleAskMeMessage = "";
    }

    public CharacterPreset(string name, string yourName, ChatContent chatContent,
        List<string> exceptTextRegexExpression,
        List<string> enabledPlugins, string hotZhWords, string hotRules, string idleAskMeMessage, int idleAskMeTime)
    {
        Name = name;
        YourName = yourName;
        ChatContent = chatContent;
        ExceptTextRegexExpression = exceptTextRegexExpression;
        EnabledPlugins = enabledPlugins;
        HotZhWords = hotZhWords;
        HotRules = hotRules;
        IdleAskMeTime = idleAskMeTime;
        IdleAskMeMessage = idleAskMeMessage;
    }

    public static CharacterPreset ReadConfig(string configFilePath)
    {
        if (!File.Exists(configFilePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configFilePath));
            Log.Information("Config file not found. Creating a new one.");
            var newConfig = new CharacterPreset();
            var json = JsonConvert.SerializeObject(newConfig);
            File.WriteAllText(configFilePath, json);
            return newConfig;
        }

        var config = JsonConvert.DeserializeObject<CharacterPreset>(File.ReadAllText(configFilePath));
        File.WriteAllText(configFilePath, JsonConvert.SerializeObject(config));
        Log.Information("Read config info: {@ConfigManager}", config);

        return config;
    }

    public static void WriteConfig(string configFilePath, CharacterPreset modelParameterConfig)
    {
        File.WriteAllText(configFilePath, JsonConvert.SerializeObject(modelParameterConfig));
        Log.Information("Write config info to file: {@ConfigManager}", modelParameterConfig);
    }

    public static List<string> GetAllConfigFileNames(string configRootPath)
    {
        if (Directory.Exists(configRootPath) == false)
        {
            Directory.CreateDirectory(configRootPath);
        }

        var files = Directory.GetFiles(configRootPath);
        List<string> configFileNames = new();
        foreach (var file in files)
        {
            if (Path.GetExtension(file) == ".json")
            {
                try
                {
                    CharacterPreset.ReadConfig(file);
                    configFileNames.Add(Path.GetRelativePath(configRootPath, file));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        return configFileNames;
    }
}

public struct BertVits2Configuration
{
    public int Id { get; set; }
    public string Format { get; set; }
    public string Lang { get; set; }
    public double Length { get; set; }
    public double Noise { get; set; }
    public double Noisew { get; set; }
    public double SdpRatio { get; set; }
    public int SegmentSize { get; set; }

    public BertVits2Configuration()
    {
        Id = 0;
        Format = "wav";
        Lang = "zh";
        Length = 1.0;
        Noise = 0.33;
        Noisew = 0.4;
        SdpRatio = 0.2;
        SegmentSize = 50;
    }

    public BertVits2Configuration(int id, string format, string lang, double length, double noise, double noisew,
        double sdpRatio, int segmentSize)
    {
        Id = id;
        Format = format;
        Lang = lang;
        Length = length;
        Noise = noise;
        Noisew = noisew;
        SdpRatio = sdpRatio;
        SegmentSize = segmentSize;
    }

    public static BertVits2Configuration ReadConfig(string configFilePath)
    {
        if (!File.Exists(configFilePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configFilePath));
            Log.Information("Config file not found. Creating a new one.");
            var newConfig = new BertVits2Configuration();
            var json = JsonConvert.SerializeObject(newConfig);
            File.WriteAllText(configFilePath, json);
            return newConfig;
        }

        var config = JsonConvert.DeserializeObject<BertVits2Configuration>(File.ReadAllText(configFilePath));
        File.WriteAllText(configFilePath, JsonConvert.SerializeObject(config));
        Log.Information("Read config info: {@ConfigManager}", config);

        return config;
    }

    public static void WriteConfig(string configFilePath, BertVits2Configuration configuration)
    {
        File.WriteAllText(configFilePath, JsonConvert.SerializeObject(configuration));
        Log.Information("Write config info to file: {@ConfigManager}", configuration);
    }
}

public struct ApplicationConfig
{
    public string LastSelectedModelConfigFileName;
    public string LastSelectedCharacterPresetConfigFileName;

    public ModelType LastSelectedModelType;
    public TextInputMode LastSelectedTextInputMode;

    public string LastSelectedModelFileName;

    public ApplicationConfig()
    {
        LastSelectedModelConfigFileName = "";
        LastSelectedCharacterPresetConfigFileName = "";
        LastSelectedModelType = ModelType.Online;
        LastSelectedTextInputMode = TextInputMode.Text;
        LastSelectedModelFileName = "";
    }

    public ApplicationConfig(string lastSelectedModelConfigFileName, string lastSelectedCharacterPresetConfigFileName,
        ModelType modelType, TextInputMode textInputMode, string lastSelectedModelFileName)
    {
        LastSelectedModelConfigFileName = lastSelectedModelConfigFileName;
        LastSelectedCharacterPresetConfigFileName = lastSelectedCharacterPresetConfigFileName;
        LastSelectedModelType = modelType;
        LastSelectedTextInputMode = textInputMode;
        LastSelectedModelFileName = lastSelectedModelFileName;
    }


    public static ApplicationConfig ReadConfig(string configFilePath)
    {
        if (!File.Exists(configFilePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configFilePath));
            Log.Information("Config file not found. Creating a new one.");
            var newConfig = new ApplicationConfig();
            var json = JsonConvert.SerializeObject(newConfig);
            File.WriteAllText(configFilePath, json);
            return newConfig;
        }

        var config = JsonConvert.DeserializeObject<ApplicationConfig>(File.ReadAllText(configFilePath));
        File.WriteAllText(configFilePath, JsonConvert.SerializeObject(config));
        Log.Information("Read config info: {@ConfigManager}", config);

        return config;
    }

    public static void WriteConfig(string configFilePath, ApplicationConfig configuration)
    {
        File.WriteAllText(configFilePath, JsonConvert.SerializeObject(configuration));
        Log.Information("Write config info to file: {@ConfigManager}", configuration);
    }
}