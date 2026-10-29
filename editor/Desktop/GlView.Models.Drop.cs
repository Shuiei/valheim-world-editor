using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Letting go of the models and the objects on the graphics card: when the budget is past, or another area is shown.
public sealed partial class GlView
{
    /// <summary>Every model and texture off the graphics card (after DropObjects: no batch uses them).</summary>
    private void DropModels()
    {
        foreach (var (vbo, ebos, _) in _meshGl.Values)
        {
            _own.DeleteBuffer(vbo);
            foreach (uint e in ebos)
            {
                _own.DeleteBuffer(e);
            }
        }
        _meshGl.Clear();
        _pickMeshes.Clear();
        _bothWays.Clear();
        _onGpu.Clear();
        lock (_textures)
        {
            foreach (uint t in _textures.Values)
            {
                _own.DeleteTexture(t);
            }
            _textures.Clear();
            _claimed.Clear();
        }
        Status?.Invoke(Said.Of("world.msg.modelsFreed", _gpuBytes >> 20));
        _gpuBytes = 0;
    }

    /// <summary>Everything read again (after a save): the old batches go.</summary>
    private void DropObjects()
    {
        foreach (var b in _batches)
        {
            _own.DeleteVertexArray(b.Vao);
            _own.DeleteBuffer(b.InstanceVbo);
            if (b.OwnVbo != 0)
            {
                _own.DeleteBuffer(b.OwnVbo);
                _own.DeleteBuffer(b.OwnEbo);
            }
        }
        _batches.Clear();
        _shapeBatches.Clear();
        _shapesDirty = true;
        // The cells' boxes are made again with the objects.
        _cellScene = null;
        while (_ready.TryDequeue(out var dropped))
        {
            UploadTextures(dropped);
        }
        // (The selection went when the area was shown or read again, not here: one made since, as the
        // map's "Edit in 3D" makes at once, is the new area's.)
        _selectionDirty = true;
        lock (_objLock)
        {
            Array.Clear(_known);
        }
    }
}
