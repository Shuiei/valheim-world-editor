// Valheim world editor: a desktop app (its own window, or the browser with --browser) around a local
// web server. With a world folder or --live it opens that world; without, a start page lists worlds.
//   ValheimWorldEditor [worldFolder] [--port 5180] [--browser]
//   ValheimWorldEditor --live http://127.0.0.1:5182 --token <token>      (WorldEditorBridge plugin)
return await TerrainEditor.App.AppHost.Run(args);
