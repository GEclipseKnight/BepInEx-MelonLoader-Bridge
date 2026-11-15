using MelonLoader.InternalUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
#pragma warning disable 0618

namespace MelonLoader
{
    public abstract class MelonBase
    {
        #region Static

        public static readonly MelonEvent<MelonBase> OnMelonRegistered = new();

        public static readonly MelonEvent<MelonBase> OnMelonUnregistered = new();

        public static readonly MelonEvent<MelonBase> OnMelonInitializing = new();

        public static ReadOnlyCollection<MelonBase> RegisteredMelons => _registeredMelons.AsReadOnly();
        internal static List<MelonBase> _registeredMelons = new();

        public static T CreateWrapper<T>(string name, string author, string version, MelonGameAttribute[] games = null, MelonProcessAttribute[] processes = null, int priority = 0, ConsoleColor? color = null, ConsoleColor? authorColor = null, string id = null) where T : MelonBase, new()
        {
            var melon = new T
            {
                Info = new MelonInfoAttribute(typeof(T), name, version, author),
                MelonAssembly = MelonAssembly.LoadMelonAssembly(null, typeof(T).Assembly),
                Priority = priority,
                ConsoleColor = color ?? MelonLogger.DefaultMelonColor,
                AuthorConsoleColor = authorColor ?? MelonLogger.DefaultTextColor,
                SupportedProcesses = processes,
                Games = games,
                OptionalDependencies = null,
                ID = id
            };

            return melon;
        }

        public static void RegisterSorted<T>(IEnumerable<T> melons) where T : MelonBase
        {
            if (melons == null)
                return;

            var collection = melons.ToList();
            SortMelons(ref collection);

            foreach (var m in melons)
                m.Register();
        }

        private static void SortMelons<T>(ref List<T> melons) where T : MelonBase
        {
            DependencyGraph<T>.TopologicalSort(melons);
            melons = melons.OrderBy(x => x.Priority).ToList();
        }

        #endregion

        #region Instance

        private MelonGameAttribute[] _games = new MelonGameAttribute[0];
        private MelonProcessAttribute[] _processes = new MelonProcessAttribute[0];
        private MelonGameVersionAttribute[] _gameVersions = new MelonGameVersionAttribute[0];

        public readonly MelonEvent OnRegister = new();
        public readonly MelonEvent OnUnregister = new();

        public MelonAssembly MelonAssembly { get; internal set; }

        public int Priority { get; internal set; }

        public ConsoleColor ConsoleColor { get; internal set; }

        public ConsoleColor AuthorConsoleColor { get; internal set; }

        public MelonInfoAttribute Info { get; internal set; }

        public MelonProcessAttribute[] SupportedProcesses
        {
            get => _processes;
            internal set => _processes = (value == null || value.Any(x => x.Universal)) ? new MelonProcessAttribute[0] : value;
        }

        public MelonGameAttribute[] Games
        {
            get => _games;
            internal set => _games = (value == null || value.Any(x => x.Universal)) ? new MelonGameAttribute[0] : value;
        }

        public MelonGameVersionAttribute[] SupportedGameVersions
        {
            get => _gameVersions;
            internal set => _gameVersions = (value == null || value.Any(x => x.Universal)) ? new MelonGameVersionAttribute[0] : value;
        }

        public MelonOptionalDependenciesAttribute OptionalDependencies { get; internal set; }

        public MelonPlatformAttribute SupportedPlatforms { get; internal set; }

        public MelonPlatformDomainAttribute SupportedDomain { get; internal set; }

        public VerifyLoaderVersionAttribute SupportedMLVersion { get; internal set; }

        public VerifyLoaderBuildAttribute SupportedMLBuild { get; internal set; }

        public HarmonyLib.Harmony HarmonyInstance { get; internal set; }

        public MelonLogger.Instance LoggerInstance { get; internal set; }

        public string ID { get; internal set; }

        public bool Registered { get; private set; }

        public abstract string MelonTypeName { get; }

        #region Callbacks

        public virtual void OnPreSupportModule() { }

        public virtual void OnUpdate() { }

        public virtual void OnFixedUpdate() { }

        public virtual void OnLateUpdate() { }

        public virtual void OnGUI() { }

        public virtual void OnApplicationQuit() { }

        public virtual void OnPreferencesSaved() { }

        public virtual void OnPreferencesSaved(string filepath) { }

        public virtual void OnPreferencesLoaded() { }

        public virtual void OnPreferencesLoaded(string filepath) { }

        public virtual void OnEarlyInitializeMelon() { }

        public virtual void OnInitializeMelon() { }

        public virtual void OnLateInitializeMelon() { }

        public virtual void OnDeinitializeMelon() { }

        #endregion

        public Incompatibility[] FindIncompatiblities(MelonGameAttribute game, string processName, string gameVersion,
            string mlVersion, string mlBuildHashCode, MelonPlatformAttribute.CompatiblePlatforms platform,
            MelonPlatformDomainAttribute.CompatibleDomains domain)
        {
            var result = new List<Incompatibility>();
            if (!(Games.Length == 0 || Games.Any(x => x.IsCompatible(game))))
                result.Add(Incompatibility.Game);
            else
            {
                if (!(SupportedGameVersions.Length == 0 || SupportedGameVersions.Any(x => x.Version == gameVersion)))
                    result.Add(Incompatibility.GameVersion);

                if (!(SupportedProcesses.Length == 0 || SupportedProcesses.Any(x => x.IsCompatible(processName))))
                    result.Add(Incompatibility.ProcessName);

                if (!(SupportedPlatforms == null || SupportedPlatforms.IsCompatible(platform)))
                    result.Add(Incompatibility.Platform);

                if (!(SupportedDomain == null || SupportedDomain.IsCompatible(domain)))
                    result.Add(Incompatibility.Domain);
            }
            if (!(SupportedMLVersion == null || SupportedMLVersion.IsCompatible(mlVersion)))
                result.Add(Incompatibility.MLVersion);
            else
            {
                if (!(SupportedMLBuild == null || SupportedMLBuild.IsCompatible(mlBuildHashCode)))
                    result.Add(Incompatibility.MLBuild);
            }

            return result.ToArray();
        }

        public Incompatibility[] FindIncompatiblitiesFromContext()
        {
            return FindIncompatiblities(MelonUtils.CurrentGameAttribute, Process.GetCurrentProcess().ProcessName, MelonUtils.GameVersion, BuildInfo.Version, MelonUtils.HashCode, MelonUtils.CurrentPlatform, MelonUtils.CurrentDomain);
        }

        public static void PrintIncompatibilities(Incompatibility[] incompatibilities, MelonBase melon)
        {
            if (incompatibilities == null || incompatibilities.Length == 0)
                return;

            MelonLogger.WriteLine(ConsoleColor.Red);
            MelonLogger.Msg(ConsoleColor.DarkRed, $"'{melon.Info.Name} v{melon.Info.Version}' is incompatible:");
            if (incompatibilities.Contains(Incompatibility.Game))
            {
                MelonLogger.Msg($"- {melon.Info.Name} is only compatible with the following Games:");

                foreach (var g in melon.Games)
                    MelonLogger.Msg($"    - '{g.Name}' by {g.Developer}");
            }
            if (incompatibilities.Contains(Incompatibility.GameVersion))
            {
                MelonLogger.Msg($"- {melon.Info.Name} is only compatible with the following Game Versions:");

                foreach (var g in melon.SupportedGameVersions)
                    MelonLogger.Msg($"    - {g.Version}");
            }
            if (incompatibilities.Contains(Incompatibility.ProcessName))
            {
                MelonLogger.Msg($"- {melon.Info.Name} is only compatible with the following Process Names:");

                foreach (var p in melon.SupportedProcesses)
                    MelonLogger.Msg($"    - '{p.EXE_Name}'");
            }
            if (incompatibilities.Contains(Incompatibility.Platform))
            {
                MelonLogger.Msg($"- {melon.Info.Name} is only compatible with the following Platforms:");

                foreach (var p in melon.SupportedPlatforms.Platforms)
                    MelonLogger.Msg($"    - {p}");
            }
            if (incompatibilities.Contains(Incompatibility.Domain))
            {
                MelonLogger.Msg($"- {melon.Info.Name} is only compatible with the following Domain:");
                MelonLogger.Msg($"    - {melon.SupportedDomain.Domain}");
            }
            if (incompatibilities.Contains(Incompatibility.MLVersion))
            {
                MelonLogger.Msg($"- {melon.Info.Name}  is only compatible with the following MelonLoader Versions:");
                MelonLogger.Msg($"    - {melon.SupportedMLVersion.SemVer}{(melon.SupportedMLVersion.IsMinimum ? " or higher" : "")}");
            }
            if (incompatibilities.Contains(Incompatibility.MLBuild))
            {
                MelonLogger.Msg($"- {melon.Info.Name} is only compatible with the following MelonLoader Build Hash Codes:");
                MelonLogger.Msg($"    - {melon.SupportedMLBuild.HashCode}");
            }

            MelonLogger.WriteLine(ConsoleColor.Red);
            MelonLogger.WriteSpacer();
        }

        public bool Register()
        {
            if (Registered)
                return false;

            if (FindMelon(Info.Name, Info.Author) != null)
            {
                MelonLogger.Warning($"Failed to register {MelonTypeName} '{Location}': A Melon with the same Name and Author is already registered!");
                return false;
            }

            var comp = FindIncompatiblitiesFromContext();
            if (comp.Length != 0)
            {
                PrintIncompatibilities(comp, this);
                return false;
            }

            OnMelonInitializing.Invoke(this);

            LoggerInstance ??= new MelonLogger.Instance(string.IsNullOrEmpty(ID) ? Info.Name : $"{ID}:{Info.Name}", ConsoleColor);
            HarmonyInstance ??= new HarmonyLib.Harmony($"{Assembly.FullName}:{Info.Name}");

            Registered = true; // this has to be true before the melon can subscribe to any events
            RegisterCallbacks();

            try
            {
                OnEarlyInitializeMelon();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to register {MelonTypeName} '{Location}': Melon failed to initialize!");
                MelonLogger.Error(ex.ToString());
                Registered = false;
                return false;
            }

            if (!RegisterInternal())
                return false;

            _registeredMelons.Add(this);

            PrintLoadInfo();

            OnRegister.Invoke();
            OnMelonRegistered.Invoke(this);

            if (MelonEvents.OnApplicationStart.Disposed)
                LoaderInitialized();
            else
                MelonEvents.OnApplicationStart.Subscribe(LoaderInitialized, Priority, true);

            if (MelonEvents.OnApplicationLateStart.Disposed)
                OnLateInitializeMelon();
            else
                MelonEvents.OnApplicationLateStart.Subscribe(OnLateInitializeMelon, Priority, true);

            return true;
        }

        private void HarmonyInit()
        {
            if (!MelonAssembly.HarmonyDontPatchAll)
                HarmonyInstance.PatchAll(MelonAssembly.Assembly);
        }

        private void LoaderInitialized()
        {
            try
            {
                OnInitializeMelon();
            }
            catch (Exception ex)
            {
                LoggerInstance.Error(ex);
            }
        }

        protected private virtual bool RegisterInternal()
        {
            return true;
        }

        protected private virtual void UnregisterInternal() { }

        protected private virtual void RegisterCallbacks()
        {
            MelonEvents.OnApplicationQuit.Subscribe(OnApplicationQuit, Priority);
            MelonEvents.OnUpdate.Subscribe(OnUpdate, Priority);
            MelonEvents.OnLateUpdate.Subscribe(OnLateUpdate, Priority);
            MelonEvents.OnGUI.Subscribe(OnGUI, Priority);
            MelonEvents.OnFixedUpdate.Subscribe(OnFixedUpdate, Priority);
            MelonEvents.OnApplicationLateStart.Subscribe(OnApplicationLateStart, Priority);

            MelonPreferences.OnPreferencesLoaded.Subscribe(PrefsLoaded, Priority);
            MelonPreferences.OnPreferencesSaved.Subscribe(PrefsSaved, Priority);
        }

        private void PrefsSaved(string path)
        {
            OnPreferencesSaved(path);
            OnPreferencesSaved();
            OnModSettingsApplied();
        }

        private void PrefsLoaded(string path)
        {
            OnPreferencesLoaded(path);
            OnPreferencesLoaded();
        }

        public static MelonBase FindMelon(string melonName, string melonAuthor)
        {
            return _registeredMelons.Find(x => x.Info.Name == melonName && x.Info.Author == melonAuthor);
        }

        public void Unregister(string reason = null, bool silent = false)
        {
            if (!Registered)
                return;

            MelonAssembly.UnregisterMelons(reason, silent);
        }

        internal void UnregisterInstance(string reason, bool silent)
        {
            if (!Registered)
                return;

            try
            {
                OnDeinitializeMelon();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to properly unregister {MelonTypeName} '{Location}': Melon failed to deinitialize!");
                MelonLogger.Error(ex.ToString());
            }

            UnregisterInternal();

            _registeredMelons.Remove(this);
            HarmonyInstance.UnpatchSelf();
            Registered = false;

            if (!silent)
                PrintUnloadInfo(reason);

            OnUnregister.Invoke();
            OnMelonUnregistered.Invoke(this);
        }

        private void PrintLoadInfo()
        {
            MelonLogger.WriteLine(ConsoleColor.DarkGreen);
            
            MelonLogger.Internal_PrintModName(ConsoleColor, AuthorConsoleColor, Info.Name, Info.Author, Info.Version, ID);
            MelonLogger.Msg(ConsoleColor.DarkGray, $"Assembly: {Path.GetFileName(MelonAssembly.Location)}");

            MelonLogger.WriteLine(ConsoleColor.DarkGreen);
        }

        private void PrintUnloadInfo(string reason)
        {
            MelonLogger.WriteLine(ConsoleColor.DarkRed);

            MelonLogger.Msg(ConsoleColor.DarkGray, MelonTypeName + " deinitialized:");
            MelonLogger.Internal_PrintModName(ConsoleColor, AuthorConsoleColor, Info.Name, Info.Author, Info.Version, ID);

            if (!string.IsNullOrEmpty(reason))
            {
                MelonLogger.Msg(string.Empty);
                MelonLogger.Msg($"Reason: '{reason}'");
            }

            MelonLogger.WriteLine(ConsoleColor.DarkRed);
        }

        public static void ExecuteAll(LemonAction<MelonBase> func, bool unregisterOnFail = false, string unregistrationReason = null)
        {
            ExecuteList(func, _registeredMelons, unregisterOnFail, unregistrationReason);
        }

        public static void ExecuteList<T>(LemonAction<T> func, List<T> melons, bool unregisterOnFail = false, string unregistrationReason = null) where T : MelonBase
        {
            var failedMelons = (unregisterOnFail ? new List<T>() : null);

            LemonEnumerator<T> enumerator = new(melons.ToArray());
            while (enumerator.MoveNext())
            {
                var melon = enumerator.Current;
                if (!melon.Registered)
                    continue;

                try { func(melon); }
                catch (Exception ex)
                {
                    melon.LoggerInstance.Error(ex.ToString());
                    if (unregisterOnFail)
                        failedMelons.Add(melon);
                }
            }

            if (unregisterOnFail)
            {
                foreach (var m in failedMelons)
                    m.Unregister(unregistrationReason);
            }
        }

        public static void SendMessageAll(string name, params object[] arguments)
        {
            LemonEnumerator<MelonBase> enumerator = new(_registeredMelons.ToArray());
            while (enumerator.MoveNext())
            {
                var melon = enumerator.Current;
                if (!melon.Registered)
                    continue;

                try { melon.SendMessage(name, arguments); }
                catch (Exception ex) { melon.LoggerInstance.Error(ex.ToString()); }
            }
        }

        public object SendMessage(string name, params object[] arguments)
        {
            var msg = Info.SystemType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            if (msg == null)
                return null;

            return msg.Invoke(msg.IsStatic ? null : this, arguments);
        }
        #endregion

        #region Obsolete Members

        private Harmony.HarmonyInstance _OldHarmonyInstance;

        [Obsolete("Please use either the OnLateInitializeMelon callback, or the 'MelonEvents::OnApplicationLateStart' event instead.")]
        public virtual void OnApplicationLateStart() { }

        [Obsolete("For mods, use OnInitializeMelon instead. For plugins, use OnPreModsLoaded instead.")]
        public virtual void OnApplicationStart() { }

        [Obsolete("Please use OnPreferencesSaved instead.")]
        public virtual void OnModSettingsApplied() { }

        [Obsolete("Please use HarmonyInstance instead.")]
#pragma warning disable IDE1006 // Naming Styles
        public Harmony.HarmonyInstance harmonyInstance { get { _OldHarmonyInstance ??= new Harmony.HarmonyInstance(HarmonyInstance.Id); return _OldHarmonyInstance; } }
#pragma warning restore IDE1006 // Naming Styles

        [Obsolete("Please use HarmonyInstance instead.")]
        public Harmony.HarmonyInstance Harmony { get { _OldHarmonyInstance ??= new Harmony.HarmonyInstance(HarmonyInstance.Id); return _OldHarmonyInstance; } }

        [Obsolete("Please use MelonAssembly.Assembly instead.")]
        public Assembly Assembly => MelonAssembly.Assembly;

        [Obsolete("Please use MelonAssembly.HarmonyDontPatchAll instead.")]
        public bool HarmonyDontPatchAll => MelonAssembly.HarmonyDontPatchAll;

        [Obsolete("Please use MelonAssembly.Hash instead.")]
        public string Hash => MelonAssembly.Hash;

        [Obsolete("Please use MelonAssembly.Location instead.")]
        public string Location => MelonAssembly.Location;

        #endregion


        public enum Incompatibility
        {
            MLVersion,
            MLBuild,
            Game,
            GameVersion,
            ProcessName,
            Domain,
            Platform
        }
    }
}
