using System;

namespace MelonLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class MelonPlatformDomainAttribute : Attribute
    {
        public MelonPlatformDomainAttribute(CompatibleDomains domain = CompatibleDomains.UNIVERSAL) => Domain = domain;

        public enum CompatibleDomains
        {
            UNIVERSAL,
            MONO,
            IL2CPP
        };

        public CompatibleDomains Domain { get; internal set; }

        public bool IsCompatible(CompatibleDomains domain)
            => Domain == CompatibleDomains.UNIVERSAL || domain == CompatibleDomains.UNIVERSAL || Domain == domain;
    }
}
