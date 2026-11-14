using Semver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using MelonLoader.Utils;
#if NET6_0_OR_GREATER
using System.Runtime.Loader;
#endif

namespace MelonLoader
{
    public sealed class MelonAssembly
    {
        #region Static

        public static readonly MelonEvent<Assembly> OnAssemblyResolving = new();

        public static event LemonFunc<Assembly, ResolvedMelons> CustomMelonResolvers;

        internal static List<MelonAssembly> loadedAssemblies = new();

        public static ReadOnlyCollection<MelonAssembly> LoadedAssemblies => loadedAssemblies.AsReadOnly();

        public static T FindMelonInstance<T>() where T : MelonBase
        {
            foreach (var asm in loadedAssemblies)
            {
                foreach (var melon in asm.loadedMelons)
                {
                    if (melon is T teaMelon)
                        return teaMelon;
                }
            }

            return null;
        }

        public static MelonAssembly GetMelonAssemblyOfMember(MemberInfo member, object obj = null)
        {
            if (member == null)
                return null;

            if (obj != null && obj is MelonBase melon)
                return melon.MelonAssembly;

            var name = member.DeclaringType.Assembly.FullName;
            var ma = loadedAssemblies.Find(x => x.Assembly.FullName == name);
            return ma;
        }

        public static MelonAssembly LoadMelonAssembly(string path, bool loadMelons = true)
        {
            if (path == null)
            {
                MelonLogger.Error("Failed to load a Melon Assembly: Path cannot be null.");
                return null;
            }

            path = Path.GetFullPath(path);

            try
            {
#if NET6_0_OR_GREATER
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
#else
                var assembly = Assembly.LoadFrom(path);
#endif
                return LoadMelonAssembly(path, assembly, loadMelons);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to load Melon Assembly from '{path}':\n{ex}");
                return null;
            }
        }

        public static MelonAssembly LoadRawMelonAssembly(string path, byte[] assemblyData, byte[] symbolsData = null, bool loadMelons = true)
        {
            if (assemblyData == null)
            {
                MelonLogger.Error("Failed to load a Melon Assembly: assemblyData cannot be null.");
                return null;
            }

            try
            {
#if NET6_0_OR_GREATER
                var fileStream = new MemoryStream(assemblyData);
                var symStream = symbolsData == null ? null : new MemoryStream(symbolsData);

                var assembly = AssemblyLoadContext.Default.LoadFromStream(fileStream, symStream);
#else
                var assembly = symbolsData != null ? Assembly.Load(assemblyData, symbolsData) : Assembly.Load(assemblyData);
#endif
                return LoadMelonAssembly(path, assembly, loadMelons);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to load Melon Assembly from raw Assembly Data (length {assemblyData.Length}):\n{ex}");
                return null;
            }
        }

        public static MelonAssembly LoadMelonAssembly(string path, Assembly assembly, bool loadMelons = true)
        {
            if (!File.Exists(path))
                path = assembly.Location;

            if (assembly == null)
            {
                MelonLogger.Error("Failed to load a Melon Assembly: Assembly cannot be null.");
                return null;
            }

            var ma = loadedAssemblies.Find(x => x.Assembly.FullName == assembly.FullName);
            if (ma != null)
                return ma;

            var shortPath = path;
            if (shortPath.StartsWith(MelonUtils.BaseDirectory))
                shortPath = "." + shortPath.Remove(0, MelonUtils.BaseDirectory.Length);

            OnAssemblyResolving.Invoke(assembly);
            ma = new MelonAssembly(assembly, path);
            loadedAssemblies.Add(ma);

            if (loadMelons)
                ma.LoadMelons();

            MelonLogger.Msg($"Melon Assembly loaded: '{shortPath}'");
            MelonLogger.Msg($"SHA256 Hash: '{ma.Hash}'");
            return ma;
        }

        #endregion

        #region Instance

        private bool melonsLoaded;

        private readonly List<MelonBase> loadedMelons = new();
        private readonly List<RottenMelon> rottenMelons = new();

        public readonly MelonEvent OnUnregister = new();

        public bool HarmonyDontPatchAll { get; private set; } = true;

        public string Hash { get; private set; }

        public Assembly Assembly { get; private set; }

        public string Location { get; private set; }

        public ReadOnlyCollection<MelonBase> LoadedMelons => loadedMelons.AsReadOnly();

        public ReadOnlyCollection<RottenMelon> RottenMelons => rottenMelons.AsReadOnly();

        private MelonAssembly(Assembly assembly, string location)
        {
            Assembly = assembly;
            Location = location ?? "";
            Hash = MelonUtils.ComputeSimpleSHA256Hash(Location);
        }

        public void UnregisterMelons(string reason = null, bool silent = false)
        {
            foreach (var m in loadedMelons)
                m.UnregisterInstance(reason, silent);

            OnUnregister.Invoke();
        }

        private void OnApplicationQuit()
        {
            UnregisterMelons("MelonLoader is deinitializing.", true);
        }

        private T SafeGetAttribute<T>(bool rotten = false) where T : Attribute
        {
            try
            {
                var att = MelonUtils.PullAttributeFromAssembly<T>(Assembly);
                return att;
            }
            catch (Exception ex)
            {
                if (rotten)
                    rottenMelons.Add(new RottenMelon((Type)null, $"Failed to Pull Attribute '{typeof(T).Name}' from the Melon.", ex));
                else
                {
                    MelonLogger.Error($"Failed to Pull Attribute '{typeof(T).Name}' from the Melon.");
                    MelonLogger.Error(ex.ToString());
                }
                return null;
            }
        }

        private T[] SafeGetAllAttributes<T>(bool rotten = false) where T : Attribute
        {
            try
            {
                var att = MelonUtils.PullAttributesFromAssembly<T>(Assembly);
                return att;
            }
            catch (Exception ex)
            {
                if (rotten)
                    rottenMelons.Add(new RottenMelon((Type)null, $"Failed to Pull Attribute '{typeof(T).Name}' from the Melon.", ex));
                else
                {
                    MelonLogger.Error($"Failed to Pull Attribute '{typeof(T).Name}' from the Melon.");
                    MelonLogger.Error(ex.ToString());
                }
                return null;
            }
        }

        public void LoadMelons()
        {
            if (melonsLoaded)
                return;

            melonsLoaded = true;

            var resolvers = CustomMelonResolvers?.GetInvocationList();
            if (resolvers != null)
            {
                foreach (LemonFunc<Assembly, ResolvedMelons> r in resolvers)
                {
                    var customMelon = r.Invoke(Assembly);
                    loadedMelons.AddRange(customMelon.loadedMelons);
                    rottenMelons.AddRange(customMelon.rottenMelons);
                }
            }

            var info = SafeGetAttribute<MelonInfoAttribute>(true);
            if (info != null && info.SystemType != null && info.SystemType.IsSubclassOf(typeof(MelonBase)))
            {
                var baseType = typeof(MelonBase);
                var baseTypeMod = typeof(MelonMod);
                var baseTypePlugin = typeof(MelonPlugin);

                if (info.SystemType == baseType || info.SystemType == baseTypeMod || info.SystemType == baseTypePlugin)
                {
                    rottenMelons.Add(new RottenMelon(info.SystemType, $"{info.SystemType.FullName} cannot be used for MelonInfoAttribute.SystemType"));
                    return;
                }

                MelonBase melon;
                try
                {
                    melon = (MelonBase)Activator.CreateInstance(info.SystemType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, null, null);
                }
                catch (Exception ex)
                {
                    melon = null;
                    rottenMelons.Add(new RottenMelon(info.SystemType, "Failed to create an instance of the Melon.", ex));
                }

                if (melon != null)
                {
                    var priorityAttr = SafeGetAttribute<MelonPriorityAttribute>();
                    var colorAttr = SafeGetAttribute<MelonColorAttribute>();
                    var authorColorAttr = SafeGetAttribute<MelonAuthorColorAttribute>();
                    var procAttrs = SafeGetAllAttributes<MelonProcessAttribute>();
                    var gameAttrs = SafeGetAllAttributes<MelonGameAttribute>();
                    var optionalDependenciesAttr = SafeGetAttribute<MelonOptionalDependenciesAttribute>();
                    var idAttr = SafeGetAttribute<MelonIDAttribute>();
                    var gameVersionAttrs = SafeGetAllAttributes<MelonGameVersionAttribute>();
                    var platformAttr = SafeGetAttribute<MelonPlatformAttribute>();
                    var domainAttr = SafeGetAttribute<MelonPlatformDomainAttribute>();
                    var mlVersionAttr = SafeGetAttribute<VerifyLoaderVersionAttribute>();
                    var mlBuildAttr = SafeGetAttribute<VerifyLoaderBuildAttribute>();
                    var harmonyDPAAttr = SafeGetAttribute<HarmonyDontPatchAllAttribute>();

                    melon.Info = info;
                    melon.MelonAssembly = this;
                    melon.Priority = priorityAttr?.Priority ?? 0;
                    melon.ConsoleColor = colorAttr?.Color ?? MelonLogger.DefaultMelonColor;
                    melon.AuthorConsoleColor = authorColorAttr?.Color ?? MelonLogger.DefaultTextColor;
                    melon.SupportedProcesses = procAttrs;
                    melon.Games = gameAttrs;
                    melon.SupportedGameVersions = gameVersionAttrs;
                    melon.SupportedPlatforms = platformAttr;
                    melon.SupportedDomain = domainAttr;
                    melon.SupportedMLVersion = mlVersionAttr;
                    melon.SupportedMLBuild = mlBuildAttr;
                    melon.OptionalDependencies = optionalDependenciesAttr;
                    melon.ID = idAttr?.ID;
                    HarmonyDontPatchAll = harmonyDPAAttr != null;

                    loadedMelons.Add(melon);

                    if (!SemVersion.TryParse(info.Version, out _))
                        MelonLogger.Warning($"==Normal users can ignore this warning==\nMelon '{info.Name}' by '{info.Author}' has version '{info.Version}' which does not use the Semantic Versioning format. Versions using formats other than the Semantic Versioning format will not be supported in the future versions of MelonLoader.\nFor more details, see: https://semver.org");
                }
            }

#if NET6_0_OR_GREATER
            RegisterTypeInIl2Cpp.RegisterAssembly(Assembly);
            RegisterTypeInIl2CppWithInterfaces.RegisterAssembly(Assembly);
#endif

            if (rottenMelons.Count != 0)
            {
                MelonLogger.Error($"Failed to load {rottenMelons.Count} {"Melon".MakePlural(rottenMelons.Count)} from {Path.GetFileName(Location)}:");
                foreach (var r in rottenMelons)
                {
                    if (r.type != null)
                        MelonLogger.Error($"Failed to load Melon '{r.type.FullName}': {r.errorMessage}");
                    else if (r.assembly != null)
                        MelonLogger.Error($"Failed to load Melon '{r.assembly.Assembly.GetName().Name}': {r.errorMessage}");
                    if (r.exception != null)
                        MelonLogger.Error(r.exception.ToString());
                }
            }
        }

        #endregion
    }
}
