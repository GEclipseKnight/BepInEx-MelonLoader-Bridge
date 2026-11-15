using System;

namespace MelonLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class MelonAdditionalDependenciesAttribute : Attribute
    {
        public string[] AssemblyNames { get; internal set; }

        public MelonAdditionalDependenciesAttribute(params string[] assemblyNames) { AssemblyNames = assemblyNames; }
    }
}
