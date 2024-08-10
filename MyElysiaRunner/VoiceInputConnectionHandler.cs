using System.Net;
using System.Text;
using System.Timers;
using MyElysiaCore;
using Newtonsoft.Json;
using Serilog;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Timer = System.Threading.Timer;

namespace MyElysiaRunner;

public class VoiceInputConnectionHandler
{
    public readonly HttpListener _listener;
    public readonly Dictionary<string, Func<HttpListenerRequest, Task<string>>> _routes;
    public String VoiceText = "";

    public readonly object _lock = new object();

    public void CheckVoiceClientConnection()
    {
        if (DateTime.Now - GlobalStatus.Instance.LastVoiceConnectionTime > System.TimeSpan.FromSeconds(30))
        {
            GlobalStatus.Instance.IsVoiceConnectionEstablished = false;
            Util.LoggerVoiceInputClient.Information("Timeout! Voice input connection not established.");
            Thread.Sleep(TimeSpan.FromSeconds(4));
        }
        else
        {
            if (GlobalStatus.Instance.IsVoiceConnectionEstablished == false)
            {
                Util.LoggerVoiceInputClient.Information("Voice input connection not established.");
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }
    }

    public VoiceInputConnectionHandler(string[] prefixes)
    {
        if (!HttpListener.IsSupported)
        {
            throw new NotSupportedException("HttpListener is not supported on this platform.");
        }

        _listener = new HttpListener();
        foreach (string prefix in prefixes)
        {
            _listener.Prefixes.Add(prefix);
        }

        _routes = new Dictionary<string, Func<HttpListenerRequest, Task<string>>>();

        AddRoute("/", HandleRoot);
        AddRoute("/is_llm_service_online", HandleIsLlmServiceOnline);
        AddRoute("/voice_input", HandleVoiceInput);
    }

    private void AddRoute(string path, Func<HttpListenerRequest, Task<string>> handler)
    {
        _routes[path] = handler;
    }

    public async Task ProcessRequest(HttpListenerContext context)
    {
        try
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            if (_routes.TryGetValue(request.Url.AbsolutePath, out var handler))
            {
                string responseString = await handler(request);
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);

                response.ContentLength64 = buffer.Length;
                response.ContentType = "application/json";
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { message = "Not Found" }));
                response.ContentLength64 = buffer.Length;
                response.ContentType = "application/json";
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }

            response.OutputStream.Close();
        }
        catch (Exception e)
        {
            Log.Error("Error processing request: {0}", e.Message);
            throw;
        }
    }

    private async Task<string> HandleVoiceInput(HttpListenerRequest request)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            string requestBody = await reader.ReadToEndAsync();

            var json = JsonConvert.DeserializeObject<Dictionary<string, string>>(requestBody);

            Console.WriteLine("Received voice input: " + json["text"]);

            lock (_lock)
            {
                VoiceText = json["text"];
            }

            var responseData = new
            {
                status = "success"
            };

            return JsonSerializer.Serialize(responseData);
        }
        catch (Exception e)
        {
            Util.LoggerVoiceInputClient.Error(e.Message);
            return JsonSerializer.Serialize(new { message = e.Message });
        }
    }

    private async Task<string> HandleRoot(HttpListenerRequest request)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            string requestBody = await reader.ReadToEndAsync();

            var responseData = new
            {
                message = "Hello, World!"
            };

            return JsonSerializer.Serialize(responseData);
        }
        catch (Exception e)
        {
            Util.LoggerVoiceInputClient.Error(e.Message);
            return JsonSerializer.Serialize(new { message = e.Message });
        }
    }

    private async Task<string> HandleIsLlmServiceOnline(HttpListenerRequest request)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            string requestBody = await reader.ReadToEndAsync();

            var responseData = new
            {
                status = "success",
            };

            GlobalStatus.Instance.IsVoiceConnectionEstablished = true;
            GlobalStatus.Instance.LastVoiceConnectionTime = DateTime.Now;

            Util.LoggerVoiceInputClient.Information("Voice input connection established.");

            return JsonSerializer.Serialize(responseData);
        }
        catch (Exception e)
        {
            Util.LoggerVoiceInputClient.Error(e.Message);
            return JsonSerializer.Serialize(new { message = e.Message });
        }
    }

    public void Stop()
    {
        _listener.Stop();
        Console.WriteLine("HTTP Server stopped.");
    }
}