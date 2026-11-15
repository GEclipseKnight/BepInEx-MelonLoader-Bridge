using System;
using System.Linq;

namespace MelonLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class MelonPlatformAttribute : Attribute
    {
        public MelonPlatformAttribute(params CompatiblePlatforms[] platforms) => Platforms = platforms;

        public enum CompatiblePlatforms
        {
            UNIVERSAL,
            WINDOWS_X86,
            WINDOWS_X64
        };

        public CompatiblePlatforms[] Platforms { get; internal set; }

        public bool IsCompatible(CompatiblePlatforms platform)
            => Platforms == null || Platforms.Length == 0 || Platforms.Contains(platform);
    }
}
