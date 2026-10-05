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

	// New terrain data for zones (TerrainComp.Save format), as a ZPackage: count, then x, z, byte array.
	public async Task<string> ApplyTerrain(IReadOnlyList<(int X, int Z, byte[] Data)> zones)
	{
		using MemoryStream ms = new();
		using (BinaryWriter w = new(ms, System.Text.Encoding.UTF8, leaveOpen: true))
		{
			w.Write(zones.Count);
			foreach (var (x, z, data) in zones)
			{
				w.Write(x);
				w.Write(z);
				w.Write(data.Length);
				w.Write(data);
			}
		}
		using HttpRequestMessage req = new(HttpMethod.Post, "terrain") { Content = new ByteArrayContent(ms.ToArray()) };
		req.Headers.Add("X-Bridge-Token", token);
		HttpResponseMessage res = await _http.SendAsync(req);
		string body = await res.Content.ReadAsStringAsync();
		if (!res.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"bridge terrain: {(int)res.StatusCode} {body}");
		}
		return body;
	}

	// Objects to destroy (live ZDOIDs) and to create (save-format bytes); returns the plugin's JSON.
	public async Task<string> ApplyObjects(IReadOnlyList<(long User, uint Id)> destroy, IReadOnlyList<byte[]> create)
	{
		using MemoryStream ms = new();
		using (BinaryWriter w = new(ms, System.Text.Encoding.UTF8, leaveOpen: true))
		{
			w.Write(destroy.Count);
			foreach (var (user, id) in destroy)
			{
				w.Write(user);
				w.Write(id);
			}
			w.Write(create.Count);
			foreach (byte[] o in create)
			{
				w.Write(o.Length);
				w.Write(o);
			}
		}
		using HttpRequestMessage req = new(HttpMethod.Post, "objects") { Content = new ByteArrayContent(ms.ToArray()) };
		req.Headers.Add("X-Bridge-Token", token);
		HttpResponseMessage res = await _http.SendAsync(req);
		string body = await res.Content.ReadAsStringAsync();
		if (!res.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"bridge objects: {(int)res.StatusCode} {body}");
		}
		return body;
	}

	public async Task<WorldSave> LoadWorld() => WorldSave.LoadLive(await Snapshot(), "live: " + Url);
}
