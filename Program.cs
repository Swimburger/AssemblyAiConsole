// Welcome to AssemblyAI! Get started with the API by transcribing
// a file using C#.
//
// In this example, we'll transcribe a local file. Get started by
// downloading the snippet, then update the 'filename' variable
// to point to the local path of the file you want to upload and
// use the API to transcribe.
//
// IMPORTANT: Update line 130 to point to a local file.
//
// Have fun!

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FileUploader
{
    // Function to upload a local file to the AssemblyAI API
    public async Task<string> UploadFileAsync(string apiToken, string path)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(apiToken);

        using var fileContent = new ByteArrayContent(File.ReadAllBytes(path));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync("https://api.assemblyai.com/v2/upload", fileContent);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error: {e.Message}");
            return null;
        }

        if (response.IsSuccessStatusCode)
        {
            string responseBody = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseBody);
            return json["upload_url"].ToString();
        }
        else
        {
            Console.Error.WriteLine($"Error: {response.StatusCode} - {response.ReasonPhrase}");
            return null;
        }
    }
}

// Upload a file and create a transcript using the AssemblyAI API
public class TranscriptFetcher
{
    // Function to fetch transcript asynchronously
    public async Task<dynamic> GetTranscriptAsync(string apiToken, string audioUrl)
    {
        string url = "https://api.assemblyai.com/v2/transcript";

        var data = new Dictionary<string, string>()
        {
            { "audio_url", audioUrl }
        };

        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("authorization", apiToken);

            var content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(url, content);

            var responseContent = await response.Content.ReadAsStringAsync();

            var responseJson = JsonConvert.DeserializeObject<dynamic>(responseContent);

            string transcriptId = responseJson.id;

            string pollingEndpoint = $"https://api.assemblyai.com/v2/transcript/{transcriptId}";

            while (true)
            {
                var pollingResponse = await client.GetAsync(pollingEndpoint);

                var pollingResponseContent = await pollingResponse.Content.ReadAsStringAsync();

                var pollingResponseJson = JsonConvert.DeserializeObject<dynamic>(pollingResponseContent);

                if (pollingResponseJson.status == "completed")
                {
                    return pollingResponseJson;
                }
                else if (pollingResponseJson.status == "error")
                {
                    throw new Exception($"Transcription failed: {pollingResponseJson.error}");
                }
                else
                {
                    Thread.Sleep(3000);
                }
            }
        }
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        // Your API token is already set in this variable
        string apiToken = Environment.GetEnvironmentVariable("ASSEMBLY_AI_API_KEY"); // only pre-modification for git

        // -----------------------------------------------------------------------------
        // Update the file path here, pointing to a local audio or video file.
        // If you don't have one, download a sample file: https://storage.googleapis.com/aai-web-samples/espn-bears.m4a
        // You may also remove the upload step and update the 'audio_url' parameter in the
        // 'GetTranscriptAsync' function to point to a remote audio or video file.
        // -----------------------------------------------------------------------------
        var path = "./espn-bears.m4a";
        var fileUploader = new FileUploader();
        var uploadUrl = await fileUploader.UploadFileAsync(apiToken, path);

        TranscriptFetcher transcriptFetcher = new TranscriptFetcher();

        try
        {
            dynamic transcript = await transcriptFetcher.GetTranscriptAsync(apiToken, uploadUrl);

            Console.WriteLine("Transcript:\n" + transcript.text);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}