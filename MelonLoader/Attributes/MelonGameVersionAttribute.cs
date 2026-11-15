using System;

namespace MelonLoader
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class MelonGameVersionAttribute : Attribute
    {
        public MelonGameVersionAttribute(string version = null)
            => Version = version;

        public string Version { get; internal set; }

        public bool Universal { get => string.IsNullOrEmpty(Version); }
    }
}
