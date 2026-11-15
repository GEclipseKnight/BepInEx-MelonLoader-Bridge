using System;
using MLCore = MelonLoader.Core;

namespace BepInEx.MelonLoader.Loader.UnityMono;

[BepInPlugin("BepInEx.MelonLoader.Loader.UnityMono", "MelonLoader 0.7.3 Bridge (GEclipseKnight)", "3.0.0")]
public class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            if (args.Name.Contains("MelonLoader"))
                return typeof(MLCore).Assembly;
            return null;
        };

        MLCore.Initialize(Config, false);
        MLCore.PreStart();
        MLCore.Start();
    }
}
