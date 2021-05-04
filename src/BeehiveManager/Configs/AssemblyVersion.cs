using System;
using System.Reflection;

namespace Etherna.BeehiveManager.Configs
{
    public class AssemblyVersion
    {
        public AssemblyVersion(Assembly assembly)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
            SimpleVersion = Version.Split('+')[0];
        }

        public string SimpleVersion { get; }
        public string Version { get; }
    }
}
