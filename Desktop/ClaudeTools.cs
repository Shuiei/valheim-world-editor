using System.ComponentModel;
using System.Text.Json;
using Avalonia.Threading;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

// What Claude can do in the editor (ClaudeServer): look at what is open, and change it as the user would,
// on the window's thread. Coordinates are the game's: x east, z north, y up, in metres. Every change
// stays pending, one step of the history each: the user saves, applies live or saves the blueprint.
[McpServerToolType]
public sealed class ClaudeTools
{
	private readonly MainWindow _w;

	internal ClaudeTools(MainWindow window)
	{
		_w = window;
	}

	private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

	internal static string ToJson(object o) => JsonSerializer.Serialize(o, Json);

	// On the window's thread, as a click would be.
	private static Task<T> OnUi<T>(Func<Task<T>> f) => Dispatcher.UIThread.InvokeAsync(f);

	private static Task<T> OnUi<T>(Func<T> f) => Dispatcher.UIThread.InvokeAsync(f).GetTask();

	[McpServerTool(Name = "editor_state", ReadOnly = true, Title = "What the editor shows")]
	[Description("What the editor shows now: the start page, a world's map, an area of a world in 3D, or the Workshop (a blank plot to build a blueprint on); the world, the area's bounds in world coordinates, the changes not saved yet, and the editor's last message. Call it first.")]
	public Task<string> EditorState() => OnUi(() => ToJson(StateOf(_w)));

	internal static object StateOf(MainWindow w)
	{
		var s = w.Session;
		var scene = s?.Scene;
		string page = w.MapShown ? "map" : w.InWorkshop ? "workshop" : s != null ? "area" : "start";
		return new
		{
			page,
			world = w.World is { } world ? new { name = world.World.Name, folder = world.World.Directory, live = world.IsLive, autoApply = world.IsLive && w.Settings.AutoApply } : null,
			area = scene == null ? null : new
			{
				name = scene.Name,
				minX = scene.X0 * 64f - 32f,
				minZ = scene.Z0 * 64f - 32f,
				maxX = scene.X0 * 64f - 32f + scene.Size * 64f,
				maxZ = scene.Z0 * 64f - 32f + scene.Size * 64f,
				water = scene.Water,
				objects = scene.Things.Count(t => !t.Gone),
			},
			pending = w.World is { } pw ? new { zones = pw.Pending.Zones, added = pw.Pending.Added, deleted = pw.Pending.Deleted } : null,
			canUndo = s?.CanUndo ?? false,
			message = w.MessageText.Text ?? "",
		};
	}
}
