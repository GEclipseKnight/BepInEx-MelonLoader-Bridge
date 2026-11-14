using System.Collections;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace MelonLoader.Support
{
    internal static class Main
    {
        private static ISupportModule_To Initialize(ISupportModule_From from)
        {
            ClassInjector.RegisterTypeInIl2Cpp<MelonLoaderComponent>();
            
            GameObject go = new GameObject("MelonLoaderSupportModule");
            GameObject.DontDestroyOnLoad(go);
            MelonLoaderComponent component = go.AddComponent<MelonLoaderComponent>();
            component.From = from;
            from.OnApplicationLateStart();
            
            return new SupportModuleTo(component);
        }
    }
    
    internal class MelonLoaderComponent : MonoBehaviour
    {
        internal ISupportModule_From From;
        private UnityAction<Scene, LoadSceneMode> sceneLoadedHandler;
        private UnityAction<Scene> sceneUnloadedHandler;
        
        void Update()
        {
            if (!System.Object.ReferenceEquals(From, null))
                From.Update();
        }
        
        void FixedUpdate()
        {
            if (!System.Object.ReferenceEquals(From, null))
                From.FixedUpdate();
        }
        
        void LateUpdate()
        {
            if (!System.Object.ReferenceEquals(From, null))
                From.LateUpdate();
        }
        
        void OnGUI()
        {
            if (!System.Object.ReferenceEquals(From, null))
                From.OnGUI();
        }
        
        void OnDestroy()
        {
            if (!System.Object.ReferenceEquals(From, null))
                From.DefiniteQuit();
        }
        
        void OnEnable()
        {
            sceneLoadedHandler = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction<Scene, LoadSceneMode>>((System.Action<Scene, LoadSceneMode>)OnSceneLoaded);
            sceneUnloadedHandler = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction<Scene>>((System.Action<Scene>)OnSceneUnloaded);
            
            if (!System.Object.ReferenceEquals(sceneLoadedHandler, null))
                SceneManager.sceneLoaded += sceneLoadedHandler;
            
            if (!System.Object.ReferenceEquals(sceneUnloadedHandler, null))
                SceneManager.sceneUnloaded += sceneUnloadedHandler;
        }
        
        void OnDisable()
        {
            if (!System.Object.ReferenceEquals(sceneLoadedHandler, null))
                SceneManager.sceneLoaded -= sceneLoadedHandler;
            
            if (!System.Object.ReferenceEquals(sceneUnloadedHandler, null))
                SceneManager.sceneUnloaded -= sceneUnloadedHandler;
        }
        
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!System.Object.ReferenceEquals(From, null))
                From.OnSceneWasLoaded(scene.buildIndex, scene.name);
        }
        
        private void OnSceneUnloaded(Scene scene)
        {
            if (!System.Object.ReferenceEquals(From, null))
                From.OnSceneWasUnloaded(scene.buildIndex, scene.name);
        }
    }
    
    internal class SupportModuleTo : ISupportModule_To
    {
        private MelonLoaderComponent component;
        
        public SupportModuleTo(MelonLoaderComponent component)
        {
            this.component = component;
        }
        
        public object StartCoroutine(IEnumerator coroutine)
        {
            var wrappedCoroutine = BepInEx.Unity.IL2CPP.Utils.Collections.CollectionExtensions.WrapToIl2Cpp(coroutine);
            
            if (!System.Object.ReferenceEquals(component, null))
            {
                return component.StartCoroutine(wrappedCoroutine);
            }
            
            return wrappedCoroutine;
        }
        
        public void StopCoroutine(object coroutineToken)
        {
            if (!System.Object.ReferenceEquals(component, null) && coroutineToken is Coroutine c)
                component.StopCoroutine(c);
        }
        
        public void UnityDebugLog(string msg)
        {
            UnityEngine.Debug.Log(msg);
        }
    }
}
