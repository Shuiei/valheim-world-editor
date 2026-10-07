using System.Net;
using System.Net.Sockets;

namespace TerrainEditor.App;

// Where things are on this computer, shared by the web app and the native app: the game's world
// folders, and a free local port.
public static class Places
{
	public static IEnumerable<string> WorldRoots()
	{
		string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (OperatingSystem.IsWindows())
		{
			yield return Path.Combine(home, "AppData", "LocalLow", "IronGate", "Valheim", "worlds_local");
			yield return Path.Combine(home, "AppData", "LocalLow", "IronGate", "Valheim", "worlds");
		}
		else
		{
			yield return Path.Combine(home, ".config", "unity3d", "IronGate", "Valheim", "worlds_local");
			yield return Path.Combine(home, ".config", "unity3d", "IronGate", "Valheim", "worlds");
			// Valheim through Proton (Steam Play) keeps its Windows-style folder inside the prefix.
			foreach (string steam in new[] { Path.Combine(home, ".steam", "steam"), Path.Combine(home, ".local", "share", "Steam") })
			{
				yield return Path.Combine(steam, "steamapps", "compatdata", GameLook.ValheimAppId.ToString(), "pfx", "drive_c", "users", "steamuser", "AppData", "LocalLow", "IronGate", "Valheim", "worlds_local");
			}
		}
	}

	public static int FreePort(int from)
	{
		for (int p = from; p < from + 50; p++)
		{
			try
			{
				var l = new TcpListener(IPAddress.Loopback, p);
				l.Start();
				l.Stop();
				return p;
			}
			catch (SocketException)
			{
			}
		}
		var any = new TcpListener(IPAddress.Loopback, 0);
		any.Start();
		int port = ((IPEndPoint)any.LocalEndpoint).Port;
		any.Stop();
		return port;
	}
}
