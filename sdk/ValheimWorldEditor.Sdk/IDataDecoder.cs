namespace ValheimWorldEditor.Sdk;

/// <summary>Turns a byte-array value into text for the inspector (see <see cref="IExtensionRegistry.AddDecoder"/>).</summary>
public interface IDataDecoder
{
    /// <summary>The value as text (a few short lines at most); null when the bytes are not in a form it reads.</summary>
    /// <param name="obj">The object holding the value.</param>
    /// <param name="value">The value.</param>
    string? Describe(IObjectView obj, byte[] value);
}
