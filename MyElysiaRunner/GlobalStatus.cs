namespace MyElysiaRunner;

public class GlobalStatus
{
    public bool IsVoiceConnectionEstablished { get; set; }

    public DateTime LastVoiceConnectionTime { get; set; }
    public bool IsBertVitsConnectionEstablished { get; set; }

    public DateTime LastBertVitsConnectionTime { get; set; }

    public bool IsVoiceInputServerOnline { get; set; }

    public bool IsVoiceInputClientOnline { get; set; }

    GlobalStatus()
    {
        IsVoiceConnectionEstablished = false;
        IsBertVitsConnectionEstablished = false;
        LastVoiceConnectionTime = DateTime.Now;
        LastBertVitsConnectionTime = DateTime.Now;
        IsVoiceInputServerOnline = false;
        IsVoiceInputClientOnline = false;
    }

    public static GlobalStatus Instance = new();
}