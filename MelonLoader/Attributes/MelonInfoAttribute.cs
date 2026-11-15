using Semver;
using System;

namespace MelonLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class MelonInfoAttribute : Attribute
    {
        public Type SystemType { get; internal set; }

        public string Name { get; internal set; }

        public string Version { get; internal set; }

        public SemVersion SemanticVersion { get; internal set; }

        public string Author { get; internal set; } // This used to be optional, but is now required

        public string DownloadLink { get; internal set; } // Might get Removed. Not sure yet.

        public MelonInfoAttribute(Type type, string name, string version, string author, string downloadLink = null) 
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            SystemType = type;
            Name = name ?? "UNKNOWN";
            Author = author ?? "UNKNOWN";
            DownloadLink = downloadLink; // Might get Removed. Not sure yet.

            if (string.IsNullOrEmpty(version))
                Version = "1.0.0";
            else
                Version = version;

            if (SemVersion.TryParse(Version, out SemVersion semver))
                SemanticVersion = semver;
        }

        public MelonInfoAttribute(Type type, string name, int versionMajor, int versionMinor, int versionRevision, string versionIdentifier, string author, string downloadLink = null)
            : this(type, name, $"{versionMajor}.{versionMinor}.{versionRevision}{(string.IsNullOrEmpty(versionIdentifier) ? "" : versionIdentifier)}", author, downloadLink) { }

        public MelonInfoAttribute(Type type, string name, int versionMajor, int versionMinor, int versionRevision, string author, string downloadLink = null)
            : this(type, name, versionMajor, versionMinor, versionRevision, null, author, downloadLink) { }
    }
}
