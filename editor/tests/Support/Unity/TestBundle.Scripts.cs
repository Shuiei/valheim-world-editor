namespace WorldEditor.Tests.Unity;

/// <summary>
/// The bundle's MonoScripts and the MonoBehaviours that use them, by class.
/// </summary>
public sealed partial class TestBundle
{
    /// <summary>
    /// The MonoScript of a class here (made on first use); its path id.
    /// </summary>
    public long MonoScript(string className)
    {
        if (!_scripts.TryGetValue(className, out long pid))
        {
            _scripts[className] = pid = Assets.Add(UnityClass.MonoScript, UnityTypes.MonoScript, new Fields { ["m_Name"] = className, ["m_ClassName"] = className, ["m_AssemblyName"] = "assembly_valheim.dll" });
        }
        return pid;
    }

    /// <summary>
    /// Where a class's MonoScript is for this bundle's references (here, or in <c>ScriptsFrom</c>): file id and path id.
    /// </summary>
    private (int FileId, long PathId) ScriptOf(string className) => ScriptsFrom is { } other ? (FileOf(other), other.MonoScript(className)) : (0, MonoScript(className));

    /// <summary>
    /// Adds a MonoBehaviour of a script class (its fields from GameScripts unless given) on a GameObject.
    /// </summary>
    /// <param name="go">The GameObject's path id.</param>
    /// <param name="className">The script's class.</param>
    /// <param name="values">Its fields' values.</param>
    /// <param name="enabled">Its m_Enabled.</param>
    /// <param name="pathId">Its path id, when kept already.</param>
    /// <param name="fields">Its fields, when not those of GameScripts.</param>
    public long Mono(long go, string className, Fields? values = null, bool enabled = true, long? pathId = null, UnityField[]? fields = null)
    {
        var v = values ?? [];
        var (file, script) = ScriptOf(className);
        v["m_GameObject"] = Fields.Ref(go);
        v["m_Enabled"] = enabled ? 1 : 0;
        v["m_Script"] = Fields.Ref(script, file);
        return Assets.Add(UnityClass.MonoBehaviour, fields == null ? GameScripts.Tree(className) : UnityTypes.MonoBehaviour(fields), v, Assets.Script(file, script), pathId);
    }
}
