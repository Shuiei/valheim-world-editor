
namespace TerrainEditor.Save;

public sealed partial class LiveBridge : IDisposable
{
    /// <summary>A POST request with the token and a binary body; the caller disposes the answer.</summary>
    /// <exception cref="InvalidOperationException">
    /// The bridge answered with an error status (404 means the plugin is too old for that endpoint).
    /// </exception>
    private async Task<HttpResponseMessage> Post(string path, byte[] body)
    {
        HttpResponseMessage res = await Send(HttpMethod.Post, path, body);
        if (!res.IsSuccessStatusCode)
        {
            using (res)
            {
                string text = await res.Content.ReadAsStringAsync();
                throw new InvalidOperationException(res.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? $"the WorldEditorBridge plugin on the server is too old for {path}: update it"
                    : $"bridge {path}: {(int)res.StatusCode} {text}");
            }
        }
        return res;
    }
}
