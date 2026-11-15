using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;

namespace MelonLoader.MonoInternals.ResolveInternals
{
    internal static class AssemblyManager
    {
        internal static Dictionary<string, AssemblyResolveInfo> InfoDict = new Dictionary<string, AssemblyResolveInfo>();

        internal static bool Setup()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                if (MelonUtils.IsGameIl2Cpp() || args == null || string.IsNullOrEmpty(args.Name))
                    return null;
                    
                if (!Utility.TryParseAssemblyName(args.Name, out var assemblyName))
                    return null;
                    
                return Resolve(assemblyName.Name, (ushort)assemblyName.Version.Major, (ushort)assemblyName.Version.Minor, (ushort)assemblyName.Version.Build, (ushort)assemblyName.Version.Revision, true);
            };

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                LoadInfo(assembly);

            return true;
        }

        internal static AssemblyResolveInfo GetInfo(string name)
        {
            if (InfoDict.TryGetValue(name, out AssemblyResolveInfo resolveInfo))
                return resolveInfo;
            lock (InfoDict)
                InfoDict[name] = new AssemblyResolveInfo();
            return InfoDict[name];
        }

        private static Assembly Resolve(string requested_name, ushort major, ushort minor, ushort build, ushort revision, bool is_preload)
        {
            Version requested_version = new Version(major, minor, build, revision);
            return Resolve(requested_name, requested_version, is_preload);
        }
        private static Assembly Resolve(string requested_name, Version requested_version, bool is_preload)
        {
            AssemblyResolveInfo resolveInfo = GetInfo(requested_name);

            Assembly assembly = resolveInfo.Resolve(requested_version);

            if (assembly == null)
                assembly = MonoResolveManager.SafeInvoke_OnAssemblyResolve(requested_name, requested_version);

            if (is_preload && (assembly == null))
                assembly = SearchDirectoryManager.Scan(requested_name);

            if (assembly != null)
                LoadInfo(assembly);

            return assembly;
        }

        internal static void LoadInfo(Assembly assembly)
        {
            AssemblyName assemblyName = assembly.GetName();

            AssemblyResolveInfo resolveInfo = GetInfo(assemblyName.Name);

            resolveInfo.SetVersionSpecific(assemblyName.Version, assembly);

            MonoResolveManager.SafeInvoke_OnAssemblyLoad(assembly);
        }
    }
}
