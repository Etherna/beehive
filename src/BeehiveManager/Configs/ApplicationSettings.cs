namespace Etherna.BeehiveManager.Configs
{
    public class ApplicationSettings
    {
        // Fields.
        string? _assemblyVersion;

        // Properties.
        public string AssemblyVersion { get => _assemblyVersion ?? "1.0.0"; set => _assemblyVersion = value; }
    }
}
