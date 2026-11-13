#if UNITY_EDITOR
using EVTrainer.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace EVTrainer.App
{
    /// <summary>
    /// Editor-only: when entering Play Mode with no XR headset active, spawns the XRI
    /// "XR Interaction Simulator" prefab if that package sample has been imported into Assets/Samples.
    /// Holds no serialized reference to sample assets, so nothing breaks when the sample is absent.
    /// Toggle with the menu EVTrainer > XR Simulator in Play Mode.
    /// </summary>
    internal static class EditorSimulatorBootstrap
    {
        private const string PrefabName = "XR Interaction Simulator";
        private const string SamplesFolder = "Assets/Samples";
        private const string MenuPath = "EVTrainer/XR Simulator in Play Mode";
        private const string EnabledPrefKey = "EVTrainer.XRSimulatorInPlayMode";

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledPrefKey, true);
            set => EditorPrefs.SetBool(EnabledPrefKey, value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SpawnSimulatorIfNeeded()
        {
            if (!Enabled) return;

            // A real headset (e.g. Quest Link) is running: never interfere with it.
            if (XRSettings.isDeviceActive) return;

            // Test runner scenes manage their own input.
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.name.StartsWith("InitTestScene")) return;

            if (GameObject.Find(PrefabName) != null) return;

            var prefab = FindSimulatorPrefab();
            if (prefab == null)
            {
                DevLog.Info($"'{PrefabName}' sample not found under {SamplesFolder}; running without the simulator. " +
                          "Import it via Window > Package Manager > XR Interaction Toolkit > Samples.");
                return;
            }

            var instance = Object.Instantiate(prefab);
            instance.name = prefab.name;
            Object.DontDestroyOnLoad(instance);
            DevLog.Info($"No XR headset active: spawned '{PrefabName}' for in-Editor testing.");
        }

        private static GameObject FindSimulatorPrefab()
        {
            if (!AssetDatabase.IsValidFolder(SamplesFolder)) return null;

            foreach (var guid in AssetDatabase.FindAssets($"t:Prefab \"{PrefabName}\"", new[] { SamplesFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && prefab.name == PrefabName) return prefab;
            }
            return null;
        }

        [MenuItem(MenuPath)]
        private static void ToggleEnabled() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        private static bool ToggleEnabledValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
#endif
