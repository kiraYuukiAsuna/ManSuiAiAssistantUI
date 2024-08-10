using System.Text;
using Newtonsoft.Json;
using NAudio.Wave;

namespace MyElysiaRunner;

public class BertVitsConnectionHandler
{
    private HttpClient _httpClient;
    private string _url;

    private string CurrentWorkingDirectory;
    private string TTSConfigPath;

    private BertVits2Configuration _ttsConfiguration;
    
    private Object _lock = new Object();

    public BertVitsConnectionHandler(string url)
    {
        _url = url;
        _httpClient = new HttpClient();

        CurrentWorkingDirectory = System.IO.Directory.GetCurrentDirectory();
        TTSConfigPath = CurrentWorkingDirectory + "/Config/ApplicationConfig/BertVits2Config.json";

        lock (_lock)
        {
            _ttsConfiguration = BertVits2Configuration.ReadConfig(TTSConfigPath);
        }
    }

    public void reloadConfig()
    {
        lock (_lock)
        {
            _ttsConfiguration = BertVits2Configuration.ReadConfig(TTSConfigPath);
        }
    }
    
    public async Task<bool> sendIsVoiceServiceOnline()
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(_url + "/voice/is_voice_service_online");

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Response: " + responseContent);
                var responseObject = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseContent);
                if (responseObject["status"] == "success")
                {
                    return true;
                }
            }
            else
            {
                Console.WriteLine($"Request failed with status code {response.StatusCode}");
                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Response: " + responseContent);
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Request error: {e.Message}");
            return false;
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
            return false;
        }

        return false;
    }

    public async Task<KeyValuePair<bool, string>> SendHttpRequestForAudioAsync(String text)
    {
        /*var requestData = new
        {
            text,
            id = 0,
            format = "wav",
            lang = "zh",
            length = 1.0,
            noise = 0.33,
            noisew = 0.4,
            sdp_ratio = 0.2,
            segment_size = 50,
            streaming = true,
        };*/

        var requestData = new
        {
            text,
            id = _ttsConfiguration.Id,
            format = _ttsConfiguration.Format,
            lang = _ttsConfiguration.Lang,
            length = _ttsConfiguration.Length,
            noise = _ttsConfiguration.Noise,
            noisew = _ttsConfiguration.Noisew,
            sdp_ratio = _ttsConfiguration.SdpRatio,
            segment_size = _ttsConfiguration.SegmentSize,
            streaming = true,
        };

        try
        {
            // 将数据序列化为JSON
            string jsonData = JsonConvert.SerializeObject(requestData);
            Console.WriteLine("Request: " + jsonData);
            StringContent content = new StringContent(jsonData, Encoding.UTF8, "application/json");

            // 设置Content-Type头信息
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // 发送POST请求
            HttpResponseMessage response = await _httpClient.PostAsync(_url + "/voice/bert-vits2", content);

            // 检查响应状态码
            if (response.IsSuccessStatusCode)
            {
                // 读取响应内容
                byte[] audioData = await response.Content.ReadAsByteArrayAsync();

                // 播放音频
                Task.Run(() => { PlayAudio(audioData); });
                // PlayAudio(audioData);

                // 保存音频文件
                // await File.WriteAllBytesAsync("output.mp3", audioData);
                // Console.WriteLine("Audio saved as output.mp3");
            }
            else
            {
                Console.WriteLine($"Request failed with status code {response.StatusCode}");
                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Response: " + responseContent);
                return new KeyValuePair<bool, string>(false,
                    $"Request failed with status code {response.StatusCode}" + "; Response: " + responseContent);
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Request error: {e.Message}");
            return new KeyValuePair<bool, string>(false, e.Message);
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
            return new KeyValuePair<bool, string>(false, e.Message);
        }

        return new KeyValuePair<bool, string>(true, "");
    }

    private void PlayAudio(byte[] audioData)
    {
        using (var ms = new MemoryStream(audioData))
        using (var rdr = new Mp3FileReader(ms))
        using (var waveOut = new WaveOutEvent())
        {
            waveOut.Init(rdr);
            waveOut.Play();
            while (waveOut.PlaybackState == PlaybackState.Playing)
            {
                Task.Delay(100).Wait(); // 保持播放状态直到播放完成
            }
        }
    }

    public void StartServerThread(String text)
    {
        // 这里只是一个示例，您可能需要根据实际情况来设计线程的启动和运行逻辑
        Task.Run(async () => { await SendHttpRequestForAudioAsync(text); }).GetAwaiter().GetResult();
    }
}