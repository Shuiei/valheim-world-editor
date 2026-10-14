namespace ValheimWorldEditor.Sdk;

/// <summary>The extension API's version.</summary>
public static class SdkInfo
{
    /// <summary>
    /// The API version this SDK is: an extension built against it runs in an editor whose
    /// <see cref="IExtensionRegistry.ApiVersion"/> is this or later. Raised only when the API changes in a way that
    /// breaks extensions; additions keep it.
    /// </summary>
    public const int ApiVersion = 1;
}
