using BepInEx;
using BepInEx.Configuration;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using RoR2;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace SSM24.FadeEmptyChests
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.SSM24.FadeEmptyChests", "Fade Empty Chests", "1.0.0")]
    [BepInDependency("com.rune580.riskofoptions")]
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
            Log.Init(Logger);

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

            ModSettingsManager.AddOption(new SliderOption(FadeMultiplier, new SliderConfig
            { 
                name = "Fade Multiplier",
                description = "How much to fade empty containers.\n\n0% is fully transparent\n100% is fully opaque",
                max = 1, 
                FormatString = "{0:0%}" 
            }));
            ModSettingsManager.AddOption(new SliderOption(BrightnessMultiplier, new SliderConfig
            {
                name = "Brightness Multiplier",
                description = "How much to darken empty containers.\n\n0% is fully black\n100% is normal brightness",
                max = 1,
                FormatString = "{0:0%}"
            }));
            ModSettingsManager.AddOption(new SliderOption(FadeOutTime, new SliderConfig
            {
                name = "Fade Out Time",
                max = 5,
                FormatString = "{0:0.00}s"
            }));

            ModSettingsManager.AddOption(new CheckBoxOption(ShouldApplyToMultishops, new CheckBoxConfig 
            {
                name = "Should Apply to Multishops"
            }));
            ModSettingsManager.AddOption(new CheckBoxOption(ShouldApplyToAdaptiveChests, new CheckBoxConfig
            {
                name = "Should Apply to Adaptive Chests"
            }));

            // create icon from file
            // mostly taken from https://github.com/Vl4dimyr/CaptainShotgunModes/blob/fdf828e/RiskOfOptionsMod.cs#L36-L48
            // i have NO clue what this code is doing but it seems to work so... cool?
            try
            {
                using Stream stream = File.OpenRead(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location), "icon.png"));
                Texture2D texture = new Texture2D(0, 0);
                byte[] imgData = new byte[stream.Length];

                stream.Read(imgData, 0, (int)stream.Length);

                if (ImageConversion.LoadImage(texture, imgData))
                {
                    ModSettingsManager.SetModIcon(
                        Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0, 0))
                    );
                }
            }
            catch (FileNotFoundException)
            {
            }

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
