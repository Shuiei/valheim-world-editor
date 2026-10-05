namespace TerrainEditor.Save;

// Client for the WorldEditorBridge BepInEx plugin running in the game (usually on the dedicated
// server, reached through an SSH tunnel such as ssh -L 5182:127.0.0.1:5182 user@server).
public sealed class LiveBridge(string url, string token)
{
	private readonly HttpClient _http = new() { BaseAddress = new Uri(url.TrimEnd('/') + "/"), Timeout = TimeSpan.FromMinutes(3) };

	public string Url { get; } = url;

	private async Task<HttpResponseMessage> Get(string path)
	{
		using HttpRequestMessage req = new(HttpMethod.Get, path);
		req.Headers.Add("X-Bridge-Token", token);
		HttpResponseMessage res = await _http.SendAsync(req);
		if (!res.IsSuccessStatusCode)
		{
			string body = await res.Content.ReadAsStringAsync();
			throw new InvalidOperationException($"bridge {path}: {(int)res.StatusCode} {body}");
		}
		return res;
	}

	public async Task<byte[]> Snapshot() => await (await Get("snapshot")).Content.ReadAsByteArrayAsync();

	public async Task<string> Players() => await (await Get("players")).Content.ReadAsStringAsync();

	public async Task<string> Status() => await (await Get("status")).Content.ReadAsStringAsync();

	public async Task<WorldSave> LoadWorld() => WorldSave.LoadLive(await Snapshot(), "live: " + Url);
}
