using Avalonia.OpenGL;
using Silk.NET.OpenGL;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The OpenGL context's life: set up when the context is made, and everything made with it deleted when it is lost.
public sealed partial class GlView
{
    /// <summary>Sets up the OpenGL calls and the shader programs.</summary>
    protected override void OnOpenGlInit(GlInterface gl)
    {
        _gl = GL.GetApi(gl.GetProcAddress);
        _own = new GlObjects(_gl);
        _u = new GlUniforms(_gl);
        _es = GlVersion.Type == GlProfileType.OpenGLES;
        _terrainProg = Program(Shaders.TerrainVs, Shaders.TerrainFs);
        _objectProg = Program(Shaders.ObjectVs, Shaders.ObjectFs);
        _waterProg = Program(Shaders.WaterVs, Shaders.WaterFs);
        Status?.Invoke(Said.Raw($"OpenGL {(_es ? "ES " : "")}{GlVersion.Major}.{GlVersion.Minor}: {_gl.GetStringS(StringName.Renderer)}"));
    }

    /// <summary>
    /// The view left the window (the map page is shown): Avalonia drops its OpenGL context and makes a
    /// new one when it comes back. Its buffers, textures and programs are shared with Avalonia's own
    /// context, so they outlive it: all are deleted here (the context is still current), then every name
    /// is forgotten and the next frame builds them again in the new one. Keeping the names drew with
    /// names that meant nothing, or something else, in the new context.
    /// </summary>
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        DropLooks();
        _look?.Delete();
        _own.DeleteAll();
        _terrainEbo = _terrainBiomeVbo = _waterVbo = _waterEbo = 0;
        _terrainProg = _objectProg = _waterProg = _lineProg = 0;
        _terrainVao = _terrainIndexCount = _waterVao = _terrainVbo = _terrainExtraVbo = _terrainCutVbo = 0;
        _ghostVbo = 0;
        _roomsMade.Forget();
        _locationsMade.Forget();
        _pathVao = _pathVbo = _pathEdgeVao = _pathEdgeVbo = _pathDotVao = _pathDotVbo = 0;
        _placeRingVao = _placeRingVbo = _hoverBoxVao = _hoverBoxVbo = _previewVao = _previewVbo = 0;
        _dungeonVao = _dungeonVbo = _dungeonPickVao = _dungeonPickVbo = _dungeonAddVao = _dungeonAddVbo = 0;
        _dungeonDoorVao = _dungeonDoorVbo = _dungeonJointVao = _dungeonJointVbo = 0;
        // A kept name drew the spawn markers into whatever the new context had given that number (an object batch:
        // models torn into spikes, an orange wedge across the view) until the view was opened again.
        _spawnMarkGl.Clear();
        _pathBandVao = _pathBandVbo = _pathSoftVao = _pathSoftVbo = _pathPreviewVao = _pathPreviewVbo = _pathPreviewSoftVao = _pathPreviewSoftVbo = 0;
        _markVao = _markVbo = _markProg = 0;
        _markCount = 0;
        _marksDirty = true;
        _placeVao = _placeVbo = _placeShapeVao = _placeShapeVbo = 0;
        _areaVao = _areaVbo = _resetVao = _resetVbo = 0;
        _measureVao = _measureVbo = _lassoVao = _lassoVbo = _ringVao = _ringVbo = _lineVao = _lineVbo = 0;
        _playersVao = _playersVbo = 0;
        _lowFbo = _lowColor = _lowDepth = 0;
        _lowW = _lowH = 0;
        _lowFailed = null;
        _batches.Clear();
        // The extensions' shapes went with the context: made again for the next one.
        _shapeBatches.Clear();
        _shapesDirty = true;
        _shapesGeneration = -1;
        _meshGl.Clear();
        _pickMeshes.Clear();
        _bothWays.Clear();
        _onGpu.Clear();
        _gpuBytes = 0;
        lock (_textures)
        {
            _textures.Clear();
            // Textures being read for the old context: read again for the new one (else every model
            // came back without its textures: trees as white leaf cards).
            _claimed.Clear();
        }
        _gizmoGl.Clear();
        _overlayGl.Clear();
        _look = null;
        _selectionDirty = true;
        _overlaysDirty = true;
        _sceneDirty = true;
    }
}
