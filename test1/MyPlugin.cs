using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using REPOLib.Modules; 

namespace REPO_MaterialReplacer
{
    [BepInPlugin("Tauhmant.Paintings", "Tauhmant's Paintings", "1.1.0")]
    public class MyPlugin : BaseUnityPlugin
    {
        internal static MyPlugin Instance { get; private set; }
        internal static ManualLogSource LoggerInstance => Instance.Logger;
        // Global list of candidate replacement materials loaded from the AssetBundle.
        public static List<Material> CandidateMaterials = new List<Material>();
        internal Harmony HarmonyInstance;
        // A static Random generator for unique selection.
        public static System.Random random;

        private void Awake()
        {
            Instance = this;
            // Detach the plugin's GameObject so it persists across scene changes.
            this.gameObject.transform.parent = null;
            this.gameObject.hideFlags = HideFlags.HideAndDontSave;

            // Initialize the random generator using a seed derived from the current UTC time (year, month, day, hour, minute).
            string seedStr = DateTime.UtcNow.ToString("yyyyMMddHH");
            int seed = seedStr.GetHashCode();
            random = new System.Random(seed);
            Logger.LogInfo($"Random generator seeded with {seed} (derived from UTC time {seedStr}).");

            LoadAssets();
            ApplyPatches();

            Logger.LogInfo($"Plugin {Info.Metadata.GUID} v{Info.Metadata.Version} loaded!");
        }

        private void ApplyPatches()
        {
            HarmonyInstance = new Harmony(Info.Metadata.GUID);
            HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo("Harmony patches applied.");
        }

        private void LoadAssets()
        {
            // Build the full path to the AssetBundle (adjust filename/extension if needed).
            string assetBundlePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "cormemus");
            Logger.LogInfo("Loading AssetBundle from: " + assetBundlePath);

            AssetBundle bundle = AssetBundle.LoadFromFile(assetBundlePath);
            if (bundle == null)
            {
                Logger.LogError("Failed to load AssetBundle!");
                return;
            }

            // Load all candidate materials from the AssetBundle.
            Material[] allMaterials = bundle.LoadAllAssets<Material>();
            CandidateMaterials = new List<Material>(allMaterials);

            Logger.LogInfo($"Loaded {CandidateMaterials.Count} materials.");
        }

        // Harmony patch: patch the target component's Start method (assumed here to be in class "Module").
        [HarmonyPatch(typeof(Module))]
        [HarmonyPatch("Start")]
        class ModuleStartPatch
        {
            static void Postfix(Component __instance)
            {
                // If no candidates are available, log a warning and exit.
                if(MyPlugin.CandidateMaterials.Count == 0)
                {
                    MyPlugin.LoggerInstance.LogWarning("No materials available!");
                    return;
                }

                // Iterate through all child MeshRenderer components.
                foreach (var meshRenderer in __instance.GetComponentsInChildren<MeshRenderer>())
                {
                    Material[] mats = meshRenderer.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        // Check if the material is one of the painting teacher materials.
                        if (mats[i] != null && mats[i].name.Contains("painting teacher"))
                        {
                            // Only replace if there is at least one candidate left.
                            if(MyPlugin.CandidateMaterials.Count > 0)
                            {
                                // Select a random candidate material using the seeded random generator.
                                int index = MyPlugin.random.Next(MyPlugin.CandidateMaterials.Count);
                                Material chosen = MyPlugin.CandidateMaterials[index];
                                mats[i] = chosen;
                                // Remove the chosen material from the list so it won't be used again.
                                //MyPlugin.CandidateMaterials.RemoveAt(index);
                                MyPlugin.LoggerInstance.LogInfo($"Replaced painting with material: {chosen.name}");
                            }
                        }
                    }
                    meshRenderer.sharedMaterials = mats;
                }
            }
        }
    }
}