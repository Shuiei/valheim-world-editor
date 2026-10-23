using System.Numerics;
using Silk.NET.OpenGL;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Drawing the area: the ground, the objects, the rooms, the water, and the tools' marks over them.
public sealed partial class GlView
{
    /// <summary>
    /// Draws the area: the ground, the objects (opaque, then see-through buildings, then glass), the dungeon rooms,
    /// ruins and the stands' items, the water, and the tools' marks.
    /// </summary>
    /// <param name="s">The area shown.</param>
    /// <param name="vp">The camera's view × projection of the frame.</param>
    /// <param name="eye">The eye's position.</param>
    /// <param name="time">The view's clock, in seconds (the game's look animates with it).</param>
    /// <param name="rh">The picture's height in pixels.</param>
    private void DrawScene(WorldScene s, Matrix4x4 vp, Vector3 eye, float time, int rh)
    {
        // The game's look, unless switched off in View.
        var look = _gameLookOn ? _look : null;
        DrawGround(vp, eye, time, look);
        var u = SetObjectUniforms(s, vp, eye);
        bool seeThrough = _seeThrough;
        int drawn = DrawObjectBatches(u, seeThrough);
        if (_shown[(int)ObjectKind.Dungeons])
        {
            DrawRooms(s);
        }
        if (_shown[(int)ObjectKind.Ruins])
        {
            DrawLocations(s);
        }
        DrawStandItems(s);
        _drawnInstances = drawn + DrawGlass(u, seeThrough);
        DrawWater(vp, eye, time, look);
        _ghostsDrawn = DrawGhosts(s, vp);
        DrawToolMarks(s, vp, rh);
    }

    /// <summary>The ground: the game's look (sky and terrain) when it is on, else the plain terrain.</summary>
    /// <param name="vp">The camera's view × projection of the frame.</param>
    /// <param name="eye">The eye's position.</param>
    /// <param name="time">The view's clock, in seconds.</param>
    /// <param name="look">The game's look, null when it is off.</param>
    private unsafe void DrawGround(Matrix4x4 vp, Vector3 eye, float time, GameLookGl? look)
    {
        if (look != null)
        {
            look.DrawSky(vp, eye, time);
            look.UseTerrain(vp, eye, time, slope: _slope, contour: ContourStep, limit: _limit);
            // The game's mesh is seen from both sides (look.js: DoubleSide).
            _gl.Disable(EnableCap.CullFace);
        }
        else
        {
            UsePlainTerrain(vp);
        }
        uint program = look?.TerrainShader() ?? _terrainProg;
        if (Scene is { } s)
        {
            look?.HoleOffset();
            SetHoles(s, program, true, look?.FreeUnit ?? 0);
        }
        _gl.BindVertexArray(_terrainVao);
        _gl.DrawElements(PrimitiveType.Triangles, _terrainIndexCount, DrawElementsType.UnsignedInt, (void*)0);
        if (Scene is { } scene)
        {
            DrawLayers(scene, vp, look);
        }
    }

    /// <summary>
    /// Mods' extensions' layers, right after the ground: those with the game's look with its program (when it is on),
    /// the others with the plain one.
    /// </summary>
    private unsafe void DrawLayers(WorldScene s, Matrix4x4 vp, GameLookGl? look)
    {
        if (_layerMeshes.Count == 0)
        {
            return;
        }
        if (look != null)
        {
            DrawLayerMeshes(s, look.TerrainShader(), look, m => m.Look);
        }
        UsePlainTerrain(vp);
        DrawLayerMeshes(s, _terrainProg, null, m => look == null || !m.Look);
        if (look != null)
        {
            _gl.Disable(EnableCap.CullFace);
        }
    }

    /// <summary>
    /// The water, before the previews and the tools' marks so they show over it: the game's water when the look is
    /// on, else a plain blue plane.
    /// </summary>
    /// <param name="vp">The camera's view × projection of the frame.</param>
    /// <param name="eye">The eye's position.</param>
    /// <param name="time">The view's clock, in seconds.</param>
    /// <param name="look">The game's look, null when it is off.</param>
    private unsafe void DrawWater(Matrix4x4 vp, Vector3 eye, float time, GameLookGl? look)
    {
        if (!ShowWater)
        {
            return;
        }
        if (look != null)
        {
            look.DrawWater(vp, eye, time);
            return;
        }
        _gl.Disable(EnableCap.CullFace);
        GlBlend.Over(_gl);
        _gl.DepthMask(false);
        _gl.UseProgram(_waterProg);
        _gl.UniformMatrix4(_u.At(_waterProg, "uViewProj"), 1, false, (float*)&vp);
        _gl.BindVertexArray(_waterVao);
        _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
        _gl.DepthMask(true);
        _gl.Disable(EnableCap.Blend);
    }

    /// <summary>The tools' marks over the area: the overlays, selection, brush, measure, path, area, place and the rest.</summary>
    /// <param name="s">The area shown.</param>
    /// <param name="vp">The camera's view × projection of the frame.</param>
    /// <param name="rh">The picture's height in pixels.</param>
    private void DrawToolMarks(WorldScene s, Matrix4x4 vp, int rh)
    {
        DrawOverlays(vp);
        DrawSelection(vp);
        DrawHoverObject(vp);
        DrawBrush(s, vp);
        DrawLasso(s, vp);
        DrawScreenBox(vp);
        DrawGizmo(s, vp);
        DrawMeasure(s, vp);
        DrawPlayers(s, vp);
        DrawPath(s, vp);
        DrawArea(s, vp);
        DrawPlace(s, vp);
        DrawDungeon(s, vp);
        DrawModPreview(vp);
        // Over everything, the water too.
        DrawSpawnMarkers(s, vp);
        if (ShowNewMarkers)
        {
            DrawNewMarkers(s, vp, rh);
        }
    }

    /// <summary>The plain ground program (the game's look off), its faces culled, with the view and the sun.</summary>
    private unsafe void UsePlainTerrain(Matrix4x4 vp)
    {
        var sun = GameLookGl.SunDirView;
        _gl.Enable(EnableCap.CullFace);
        _gl.UseProgram(_terrainProg);
        _gl.UniformMatrix4(_u.At(_terrainProg, "uViewProj"), 1, false, (float*)&vp);
        _gl.Uniform3(_u.At(_terrainProg, "uSun"), sun.X, sun.Y, sun.Z);
    }
}
