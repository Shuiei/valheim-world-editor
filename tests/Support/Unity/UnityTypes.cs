using static WorldEditor.Tests.Unity.UnityField;

namespace WorldEditor.Tests.Unity;

/// <summary>
/// The type trees of Unity's own classes the test files hold, in Unity 2022's field order (the editor's raw readers
/// rely on it: a GameObject's components first, a Transform's GameObject, rotation, position, scale, children, parent;
/// a MonoBehaviour's GameObject then m_Enabled), with only the fields the readers use after the ones they step over.
/// </summary>
public static class UnityTypes
{
    /// <summary>
    /// A GameObject: its components, layer, name, tag and whether it is active.
    /// </summary>
    public static readonly UnityField GameObject = Struct("GameObject", "Base",
        List("m_Component", Struct("ComponentPair", "data", Ref("component"))),
        Of("unsigned int", "m_Layer"),
        Text("m_Name"),
        Of("UInt16", "m_Tag"),
        Of("bool", "m_IsActive", align: true));

    /// <summary>
    /// A Transform: its GameObject, local rotation, position and scale, children and parent.
    /// </summary>
    public static readonly UnityField Transform = Struct("Transform", "Base",
        Ref("m_GameObject"),
        Quaternion("m_LocalRotation"),
        Vector3("m_LocalPosition"),
        Vector3("m_LocalScale"),
        List("m_Children", Ref("data")),
        Ref("m_Father"));

    /// <summary>
    /// The fields every MonoBehaviour starts with: its GameObject, whether it is enabled, its script and name.
    /// </summary>
    public static readonly UnityField[] MonoBehaviourBase =
    [
        Ref("m_GameObject"),
        Of("UInt8", "m_Enabled", align: true),
        Ref("m_Script"),
        Text("m_Name"),
    ];

    /// <summary>
    /// A MonoBehaviour of a script with these fields (after the base ones).
    /// </summary>
    public static UnityField MonoBehaviour(params UnityField[] fields) => Struct("MonoBehaviour", "Base", [.. MonoBehaviourBase, .. fields]);

    /// <summary>
    /// A script: its name and class.
    /// </summary>
    public static readonly UnityField MonoScript = Struct("MonoScript", "Base",
        Text("m_Name"),
        Of("int", "m_ExecutionOrder"),
        Text("m_ClassName"),
        Text("m_Namespace"),
        Text("m_AssemblyName"));

    /// <summary>
    /// A bundle's table of contents: each asset's path and where it is.
    /// </summary>
    public static readonly UnityField AssetBundle = Struct("AssetBundle", "Base",
        Text("m_Name"),
        List("m_Container", Struct("pair", "data",
            Text("first"),
            Struct("AssetInfo", "second", Of("int", "preloadIndex"), Of("int", "preloadSize"), Ref("asset")))));

    /// <summary>
    /// Where data kept out of an object is: its offset and size in a resource file, and that file's path.
    /// </summary>
    private static UnityField StreamData() => Struct("StreamingInfo", "m_StreamData", Of("UInt64", "offset"), Of("unsigned int", "size"), Text("path"));

    /// <summary>
    /// An axis-aligned box (centre, extent).
    /// </summary>
    private static UnityField Aabb(string name) => Struct("AABB", name, Vector3("m_Center"), Vector3("m_Extent"));

    /// <summary>
    /// Numbers packed in bits (Unity's PackedBitVector): how many, their range and start, the bits, bits per number.
    /// </summary>
    private static UnityField PackedFloats(string name) =>
        Struct("PackedBitVector", name, Of("unsigned int", "m_NumItems"), Of("float", "m_Range"), Of("float", "m_Start"), List("m_Data", Of("UInt8", "data"), align: true), Of("UInt8", "m_BitSize", align: true));

    /// <summary>
    /// Integers packed in bits (Unity's PackedBitVector without range): how many, the bits, bits per number.
    /// </summary>
    private static UnityField PackedInts(string name) =>
        Struct("PackedBitVector", name, Of("unsigned int", "m_NumItems"), List("m_Data", Of("UInt8", "data"), align: true), Of("UInt8", "m_BitSize", align: true));

    /// <summary>
    /// A mesh: its name, submeshes, index format and buffer, vertex data (channels and bytes), compressed mesh
    /// (positions, uv, normals, their signs and triangles packed in bits), bounds and data kept out of it.
    /// </summary>
    public static readonly UnityField Mesh = Struct("Mesh", "Base",
        Text("m_Name"),
        List("m_SubMeshes", Struct("SubMesh", "data",
            Of("unsigned int", "firstByte"),
            Of("unsigned int", "indexCount"),
            Of("int", "topology"),
            Of("unsigned int", "baseVertex"),
            Of("unsigned int", "firstVertex"),
            Of("unsigned int", "vertexCount"),
            Aabb("localAABB"))),
        Of("int", "m_IndexFormat"),
        List("m_IndexBuffer", Of("UInt8", "data"), align: true),
        Struct("VertexData", "m_VertexData",
            Of("unsigned int", "m_VertexCount"),
            List("m_Channels", Struct("ChannelInfo", "data", Of("UInt8", "stream"), Of("UInt8", "offset"), Of("UInt8", "format"), Of("UInt8", "dimension"))),
            Bytes("m_DataSize")),
        Struct("CompressedMesh", "m_CompressedMesh",
            PackedFloats("m_Vertices"), PackedFloats("m_UV"), PackedFloats("m_Normals"), PackedInts("m_NormalSigns"), PackedInts("m_Triangles")),
        Aabb("m_LocalAABB"),
        StreamData());

    /// <summary>
    /// A mesh as another Unity version lays it out: one more field after its name, so read with <see cref="Mesh"/>'s
    /// layout it is misread (as the mods' bundles, built by different Unity versions, would be).
    /// </summary>
    public static readonly UnityField MeshOfAnotherVersion = Struct("Mesh", "Base",
        Text("m_Name"),
        Of("int", "m_AnotherVersionsField"),
        List("m_SubMeshes", Struct("SubMesh", "data",
            Of("unsigned int", "firstByte"),
            Of("unsigned int", "indexCount"),
            Of("int", "topology"),
            Of("unsigned int", "baseVertex"),
            Of("unsigned int", "firstVertex"),
            Of("unsigned int", "vertexCount"),
            Aabb("localAABB"))),
        Of("int", "m_IndexFormat"),
        List("m_IndexBuffer", Of("UInt8", "data"), align: true),
        Struct("VertexData", "m_VertexData",
            Of("unsigned int", "m_VertexCount"),
            List("m_Channels", Struct("ChannelInfo", "data", Of("UInt8", "stream"), Of("UInt8", "offset"), Of("UInt8", "format"), Of("UInt8", "dimension"))),
            Bytes("m_DataSize")),
        Struct("CompressedMesh", "m_CompressedMesh",
            PackedFloats("m_Vertices"), PackedFloats("m_UV"), PackedFloats("m_Normals"), PackedInts("m_NormalSigns"), PackedInts("m_Triangles")),
        Aabb("m_LocalAABB"),
        StreamData());

    /// <summary>
    /// A texture: its name, size, format, mip count, pixels and data kept out of it.
    /// </summary>
    public static readonly UnityField Texture2D = Struct("Texture2D", "Base",
        Text("m_Name"),
        Of("int", "m_Width"),
        Of("int", "m_Height"),
        Of("int", "m_TextureFormat"),
        Of("int", "m_MipCount"),
        Bytes("image data"),
        StreamData());

    /// <summary>
    /// A texture array: its name, graphics format, size, slices, pixels and data kept out of it.
    /// </summary>
    public static readonly UnityField Texture2DArray = Struct("Texture2DArray", "Base",
        Text("m_Name"),
        Of("int", "m_Format"),
        Of("int", "m_Width"),
        Of("int", "m_Height"),
        Of("int", "m_Depth"),
        Bytes("image data"),
        StreamData());

    /// <summary>
    /// A material: its name, shader and saved properties (texture slots, floats, colours).
    /// </summary>
    public static readonly UnityField Material = Struct("Material", "Base",
        Text("m_Name"),
        Ref("m_Shader"),
        Struct("UnityPropertySheet", "m_SavedProperties",
            List("m_TexEnvs", Struct("pair", "data", Text("first"), Struct("UnityTexEnv", "second", Ref("m_Texture"), Vector2("m_Scale"), Vector2("m_Offset")))),
            List("m_Floats", Struct("pair", "data", Text("first"), Of("float", "second"))),
            List("m_Colors", Struct("pair", "data", Text("first"), Struct("ColorRGBA", "second", Of("float", "r"), Of("float", "g"), Of("float", "b"), Of("float", "a"))))));

    /// <summary>
    /// A list of lists of unsigned numbers (a shader's offsets and lengths per platform).
    /// </summary>
    private static UnityField Numbers2(string name) => List(name, List("data", Of("unsigned int", "data")));

    /// <summary>
    /// A shader: its parsed form (name, sub-shaders' passes with their vertex programs) and its programs per platform
    /// (LZ4 blobs: offsets, lengths, the blob).
    /// </summary>
    public static readonly UnityField Shader = Struct("Shader", "Base",
        Struct("SerializedShader", "m_ParsedForm",
            Text("m_Name"),
            List("m_SubShaders", Struct("SerializedSubShader", "data",
                List("m_Passes", Struct("SerializedPass", "data",
                    Struct("SerializedShaderState", "m_State", Text("m_Name")),
                    Struct("SerializedProgram", "progVertex",
                        List("m_PlayerSubPrograms", List("data", Struct("SerializedPlayerSubProgram", "data",
                            Of("unsigned int", "m_BlobIndex"),
                            List("m_KeywordIndices", Of("UInt16", "data"), align: true),
                            Of("SInt64", "m_ShaderRequirements"),
                            Of("int", "m_GpuProgramType")))),
                        Numbers2("m_ParameterBlobIndices"))))))),
        List("platforms", Of("unsigned int", "data")),
        Numbers2("offsets"),
        Numbers2("compressedLengths"),
        Numbers2("decompressedLengths"),
        List("compressedBlob", Of("UInt8", "data"), align: true));

    /// <summary>
    /// A text asset: its name and text.
    /// </summary>
    public static readonly UnityField TextAsset = Struct("TextAsset", "Base", Text("m_Name"), Text("m_Script"));

    /// <summary>
    /// A component's first fields: its GameObject.
    /// </summary>
    private static UnityField Component(string type, params UnityField[] fields) => Struct(type, "Base", [Ref("m_GameObject"), .. fields]);

    /// <summary>
    /// A collider's common fields: enabled, trigger.
    /// </summary>
    private static UnityField[] Collider(params UnityField[] fields) => [Of("bool", "m_Enabled", align: true), Of("bool", "m_IsTrigger", align: true), .. fields];

    /// <summary>
    /// A box collider: centre and size.
    /// </summary>
    public static readonly UnityField BoxCollider = Component("BoxCollider", Collider(Vector3("m_Size"), Vector3("m_Center")));

    /// <summary>
    /// A sphere collider: centre and radius.
    /// </summary>
    public static readonly UnityField SphereCollider = Component("SphereCollider", Collider(Of("float", "m_Radius"), Vector3("m_Center")));

    /// <summary>
    /// A capsule collider: centre, radius, height and axis (0 x, 1 y, 2 z).
    /// </summary>
    public static readonly UnityField CapsuleCollider = Component("CapsuleCollider", Collider(Of("float", "m_Radius"), Of("float", "m_Height"), Of("int", "m_Direction"), Vector3("m_Center")));

    /// <summary>
    /// A mesh collider: convex or not, and its mesh.
    /// </summary>
    public static readonly UnityField MeshCollider = Component("MeshCollider", Collider(Of("bool", "m_Convex", align: true), Ref("m_Mesh")));

    /// <summary>
    /// A mesh filter: its mesh.
    /// </summary>
    public static readonly UnityField MeshFilter = Component("MeshFilter", Ref("m_Mesh"));

    /// <summary>
    /// A mesh renderer: enabled, and its materials (one per submesh).
    /// </summary>
    public static readonly UnityField MeshRenderer = Component("MeshRenderer", Of("bool", "m_Enabled", align: true), List("m_Materials", Ref("data")));

    /// <summary>
    /// A skinned mesh renderer: enabled, its materials and its mesh.
    /// </summary>
    public static readonly UnityField SkinnedMeshRenderer = Component("SkinnedMeshRenderer", Of("bool", "m_Enabled", align: true), List("m_Materials", Ref("data")), Ref("m_Mesh"));

    /// <summary>
    /// A LOD group: its levels, each with its renderers.
    /// </summary>
    public static readonly UnityField LodGroup = Component("LODGroup", List("m_LODs", Struct("LOD", "data", Of("float", "screenRelativeHeight"), List("renderers", Struct("LODRenderer", "data", Ref("renderer"))))));
}
