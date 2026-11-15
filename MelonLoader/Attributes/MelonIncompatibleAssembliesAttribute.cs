using System;

namespace MelonLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class MelonIncompatibleAssembliesAttribute : Attribute
    {
        public string[] AssemblyNames { get; internal set; }

        public MelonIncompatibleAssembliesAttribute(params string[] assemblyNames) { AssemblyNames = assemblyNames; }
    }
}
