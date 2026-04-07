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
            On.RoR2.DelusionChestController.ResetChestForDelusion += On_DelusionChestController_ResetChestForDelusion;
            // TODO: figure out either clientside methods or a good way to sync it
            On.RoR2.RouletteChestController.Opened.OnEnter += On_Opened_OnEnter;
            On.RoR2.MultiShopController.OnPurchase += On_MultiShopController_OnPurchase;
        }

        private void On_Opened_OnEnter(
            On.EntityStates.Barrel.Opened.orig_OnEnter orig, EntityStates.Barrel.Opened self)
        {
            orig(self);
            Transform transform = self.outer.commonComponents.modelLocator.modelTransform;
            transform.gameObject.AddComponent<FadeObject>();
        }

        private void On_MultiShopController_OnPurchase(
            On.RoR2.MultiShopController.orig_OnPurchase orig,
            MultiShopController self, CostTypeDef.PayCostContext payCostContext, CostTypeDef.PayCostResults payCostResult)
        {
            orig(self, payCostContext, payCostResult);
            if (self.isTripleDroneVendor)
            {
                return;
            }
            if (ShouldApplyToMultishops.Value && NetworkServer.active)
            {
                // logic basically just copypasted from MultiShopController
                bool noCard = false;
                for (int i = 0; i < self.terminalGameObjects.Length; i++)
                {
                    GameObject terminalObject = self.terminalGameObjects[i];
                    PurchaseInteraction interaction = terminalObject.GetComponent<PurchaseInteraction>();
                    if (payCostContext.purchaseInteraction == interaction)
                    {
                        terminalObject.AddComponent<FadeObject>();
                        noCard = self.doCloseOnTerminalPurchase[i];
                    }
                }
                if (noCard)
                {
                    foreach (GameObject terminalObject in self.terminalGameObjects)
                    {
                        // don't double-add
                        if (terminalObject.GetComponent<FadeObject>() == null)
                        {
                            terminalObject.AddComponent<FadeObject>();
                        }
                    }
                }
            }
        }

        private void On_Opened_OnEnter(On.RoR2.RouletteChestController.Opened.orig_OnEnter orig, RouletteChestController.Opened self)
        {
            orig(self);
            if (ShouldApplyToAdaptiveChests.Value && NetworkServer.active)
            {
                Transform transform = self.outer.commonComponents.modelLocator.modelTransform;
                // apparently 0 and 1 got switched around in an update or smth
                // i can't be bothered to figure out a more proper way to do this so both of them get it :)
                transform.GetChild(0).gameObject.AddComponent<FadeObject>();
                transform.GetChild(1).gameObject.AddComponent<FadeObject>();
            }
        }

        private void On_DelusionChestController_ResetChestForDelusion(
            On.RoR2.DelusionChestController.orig_ResetChestForDelusion orig, DelusionChestController self)
        {
            orig(self);
            FadeObject fade = self.gameObject.GetComponent<ModelLocator>().modelTransform.GetComponent<FadeObject>();
            if (fade != null)
            {
                Destroy(fade);
            }
        }
    }
}
