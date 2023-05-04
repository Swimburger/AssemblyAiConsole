using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AssemblyAiConsole
{
    public class Program
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        public static async Task Main(string[] args)
        {
            // Your API token is already set in this variable
            var apiToken = Environment.GetEnvironmentVariable("ASSEMBLY_AI_API_KEY"); // only pre-modification for git
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(apiToken);

            // -----------------------------------------------------------------------------
            // Update the file path here, pointing to a local audio or video file.
            // If you don't have one, download a sample file: https://storage.googleapis.com/aai-web-samples/espn-bears.m4a
            // You may also remove the upload step and update the 'audio_url' parameter in the
            // 'GetTranscriptAsync' function to point to a remote audio or video file.
            // -----------------------------------------------------------------------------
            var path = "C:\\Users\\niels\\Downloads\\espn-bears.m4a";
            var uploadedFileUrl = await UploadFileAsync(path);
            var transcript = await GetTranscriptAsync(uploadedFileUrl);
            Console.WriteLine("Transcript:\n" + transcript);
        }

        private static async Task<string> UploadFileAsync(string path)
        {
            using var fileStream = File.OpenRead(path);
            using var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var response = await HttpClient.PostAsync("https://api.assemblyai.com/v2/upload", fileContent);

            if (response.IsSuccessStatusCode == false)
                throw new Exception($"Error: {response.StatusCode} - {response.ReasonPhrase}");

            var jsonDoc = await response.Content.ReadFromJsonAsync<JsonDocument>();
            return jsonDoc.RootElement.GetProperty("upload_url").GetString();
        }

        private static async Task<string> GetTranscriptAsync(string audioUrl)
        {
            var data = new {audio_url = audioUrl};
            var content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");

            using var response = await HttpClient.PostAsync("https://api.assemblyai.com/v2/transcript", content);
            var responseJson = await response.Content.ReadFromJsonAsync<JsonDocument>();

            var transcriptId = responseJson.RootElement.GetProperty("id").GetString();
            var pollingEndpoint = $"https://api.assemblyai.com/v2/transcript/{transcriptId}";

            while (true)
            {
                var pollingResponse = await HttpClient.GetAsync(pollingEndpoint);
                var pollingJsonDocument = await pollingResponse.Content.ReadFromJsonAsync<JsonDocument>();
                var pollingJsonObject = pollingJsonDocument.RootElement;

                var status = pollingJsonObject.GetProperty("status").GetString();
                switch (status)
                {
                    case "processing":
                        await Task.Delay(TimeSpan.FromSeconds(3));
                        break;
                    case "completed":
                        return pollingJsonObject.GetProperty("text").GetString();
                    case "error":
                        var error = pollingJsonObject.GetProperty("error").GetString();
                        throw new Exception($"Transcription failed: {error}");
                    default:
                        throw new Exception("This code should not be reachable.");
                }
            }
        }
    }
}