using System;
using System.IO;
using System.Reflection;
using BepInEx.Unity.IL2CPP;
using BepInEx;
using MLCore = MelonLoader.Core;


namespace BepInEx.MelonLoader.Loader.IL2CPP
{
    [BepInPlugin("com.geclipseknight.melonloader.bridge.il2cpp", "MelonLoader 0.7.3 Bridge (GEclipseKnight)", "3.0.0")]
    public class Plugin : BasePlugin
    {
        public override void Load()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                // First check if the assembly is already loaded
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.FullName == args.Name)
                        return asm;
                }
                
                // Then try to resolve from MelonLoader assemblies
                if (args.Name.Contains("MelonLoader"))
                    return typeof(MLCore).Assembly;
                    
                // Try to load from BepInEx core and interop directories
                string assemblyName = new AssemblyName(args.Name).Name + ".dll";
                string corePath = Path.Combine(Paths.BepInExRootPath, "core", assemblyName);
                string interopPath = Path.Combine(Paths.BepInExRootPath, "interop", assemblyName);
                
                if (File.Exists(corePath))
                    return Assembly.LoadFrom(corePath);
                    
                if (File.Exists(interopPath))
                    return Assembly.LoadFrom(interopPath);
                    
                return null;
            };

            MLCore.Initialize(Config, true);
            MLCore.PreStart();
            MLCore.Start();
        }
    }
}
