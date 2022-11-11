using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EntityStates;
using MonoMod.Utils;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace SSM24.FadeEmptyChests
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.SSM24.FadeEmptyChests", "Fade Empty Chests", "1.0.0")]
    public class FadeEmptyChests : BaseUnityPlugin
    {
        public static ConfigEntry<float> FadeMultiplier;
        public static ConfigEntry<float> BrightnessMultiplier;
        public static ConfigEntry<float> FadeOutTime;
        public static ConfigEntry<bool> ShouldApplyToMultishops;
        public static ConfigEntry<bool> ShouldApplyToAdaptiveChests;

        const float default_FadeMultiplier = 0.25f;
        const float default_BrightnessMultiplier = 0.5f;
        const float default_FadeOutTime = 1f;
        const bool default_ShouldApplyToMultishops = false;
        const bool default_ShouldApplyToAdaptiveChests = false;

        internal static FadeEmptyChests Instance;

        public void Awake()
        {
            Instance = this;

            FadeMultiplier = Config.Bind(
                "FadeEmptyChests",
                "FadeMultiplier",
                default_FadeMultiplier,
                "How much to fade empty containers. " + 
                "(0 = transparent, 1 = opaque)"
            );
            BrightnessMultiplier = Config.Bind(
                "FadeEmptyChests",
                "BrightnessMultiplier",
                default_BrightnessMultiplier,
                "How much to darken empty containers. " + 
                "(0 = completely black, 1 = normal)"
            );
            FadeOutTime = Config.Bind(
                "FadeEmptyChests",
                "FadeOutTime",
                default_FadeOutTime,
                "How long containers should take to fade out, " +
                "in seconds."
            );

            ShouldApplyToMultishops = Config.Bind(
                "FadeEmptyChests",
                "ShouldApplyToMultishops",
                default_ShouldApplyToMultishops,
                "Whether multishops should fade out after use."
            );
            ShouldApplyToAdaptiveChests = Config.Bind(
                "FadeEmptyChests",
                "ShouldApplyToAdaptiveChests",
                default_ShouldApplyToAdaptiveChests,
                "Whether adaptive chests should fade out after use."
            );

            On.EntityStates.Barrel.Opened.OnEnter += On_Opened_OnEnter;
            // TODO: figure out either clientside methods or a good way to sync it
            On.RoR2.RouletteChestController.Opened.OnEnter += On_Opened_OnEnter;
            On.RoR2.MultiShopController.DisableAllTerminals += On_MultiShopController_DisableAllTerminals;
        }

        internal static void Log(LogLevel level, object data)
        {
            Instance.Logger.Log(level, data);
        }

        private void On_Opened_OnEnter(
            On.EntityStates.Barrel.Opened.orig_OnEnter orig, EntityStates.Barrel.Opened self)
        {
            orig(self);
            Transform transform = self.outer.commonComponents.modelLocator.modelTransform;
            transform.gameObject.AddComponent<FadeObject>();
        }

        private void On_MultiShopController_DisableAllTerminals(
            On.RoR2.MultiShopController.orig_DisableAllTerminals orig, 
            MultiShopController self, Interactor interactor)
        {
            orig(self, interactor);
            if (ShouldApplyToMultishops.Value && NetworkServer.active)
            {
                DynData<MultiShopController> controllerData = new DynData<MultiShopController>(self);
                GameObject[] terminalGameObjects = controllerData.Get<GameObject[]>("terminalGameObjects");
                foreach (GameObject terminalObject in terminalGameObjects)
                {
                    terminalObject.AddComponent<FadeObject>();
                }
            }
        }

        private void On_Opened_OnEnter(On.RoR2.RouletteChestController.Opened.orig_OnEnter orig, EntityState self)
        {
            orig(self);
            if (ShouldApplyToAdaptiveChests.Value && NetworkServer.active)
            {
                Transform transform = self.outer.commonComponents.modelLocator.modelTransform;
                transform.GetChild(1).gameObject.AddComponent<FadeObject>();
            }
        }
    }
}
