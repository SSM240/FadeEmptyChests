using BepInEx.Logging;
using RoR2;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using MonoMod.Utils;

namespace SSM24.FadeEmptyChests
{
    public class FadeObject : MonoBehaviour
    {
        private float TargetFade => FadeEmptyChests.FadeMultiplier.Value;
        private float TargetBrightness => FadeEmptyChests.BrightnessMultiplier.Value;
        private float FadeOutTime => FadeEmptyChests.FadeOutTime.Value;

        // specialized chests have two renderers
        private Renderer[] renderers;
        private MaterialPropertyBlock propertyStorage;
        private List<Color> originalColors = new List<Color>();

        private float currentFade = 1f;
        private float currentBrightness = 1f;

        private static BindingFlags staticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private static List<DitherModel> instancesList =
            (List<DitherModel>)typeof(DitherModel).GetField("instancesList", staticPrivate).GetValue(null);

        private void Start()
        {
            propertyStorage = new MaterialPropertyBlock();
            renderers = gameObject.GetComponentsInChildren<Renderer>();
            StartCoroutine(WaitUntilVisible());
            StartCoroutine(InterpolateBrightnessAndFade());
        }

        private bool HasDitherModel(Renderer renderer)
        {
            // check for cached result in DynData
            DynData<Renderer> rendererData = new DynData<Renderer>(renderer);
            object hasDitherModelInData = rendererData["hasDitherModel"];
            if (hasDitherModelInData != null)
            {
                return (bool)hasDitherModelInData;
            }
            // if not found, loop through DitherModel instances
            bool foundInDitherModels = false;
            // using DynData doesn't seem to work on static fields, have to use reflection
            foreach (DitherModel ditherModel in instancesList)
            {
                if (ditherModel.renderers.Contains(renderer))
                {
                    foundInDitherModels = true;
                    break;
                }
            }
            FadeEmptyChests.Log(LogLevel.Debug, 
                $"Caching result of HasDitherModel for {gameObject.name}: {foundInDitherModels}");
            rendererData["hasDitherModel"] = foundInDitherModels;
            return foundInDitherModels;
        }

        private void RefreshRenderers()
        {
            renderers = gameObject.GetComponentsInChildren<Renderer>();
            originalColors.Clear();
            foreach (Renderer renderer in renderers)
            {
                originalColors.Add(renderer.material.color);
            }
        }

        private bool RenderersAreVisible()
        {
            bool result = true;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    RefreshRenderers();
                    FadeEmptyChests.Log(LogLevel.Info,
                        "Renderers became null, refreshing reference to renderers");
                    result = false;
                    break;
                }
                else if (!renderer.isVisible)
                {
                    result = false;
                    break;
                }
            }
            return result;
        }

        private IEnumerator InterpolateBrightnessAndFade()
        {
            float currentLerp = 0f;
            while (currentLerp <= 1f)
            {
                currentFade = Mathf.SmoothStep(1f, TargetFade, currentLerp);
                currentBrightness = Mathf.SmoothStep(1f, TargetBrightness, currentLerp);
                currentLerp += Time.deltaTime / FadeOutTime;
                yield return new WaitForEndOfFrame();
            }
            yield break;
        }

        private IEnumerator WaitUntilVisible()
        {
            // delay until container is on-screen or else things break
            // also WaitUntil throws NRE when isVisible becomes true?
            bool ready = false;
            while (!ready)
            {
                yield return new WaitForEndOfFrame();
                ready = RenderersAreVisible();
            }
            // waiting a few frames prevents errors
            yield return new WaitForSecondsRealtime(0.05f);
            RefreshRenderers();
            SceneCamera.onSceneCameraPreRender += OnSceneCameraPreRender;
            yield break;
        }

        private void OnSceneCameraPreRender(SceneCamera _)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                Color color = originalColors[i];
                ChangeColor(renderer, color);
                ChangeFade(renderer);
            }
        }

        private void ChangeColor(Renderer renderer, Color color)
        {
            try
            {
                renderer.material.color = color * currentBrightness;
            }
            catch (NullReferenceException)
            {
                FadeEmptyChests.Log(LogLevel.Info,
                    "Setting color failed, refreshing reference to renderers");
                RefreshRenderers();
                return;
            }
        }

        private void ChangeFade(Renderer renderer)
        {
            try
            {
                renderer.GetPropertyBlock(propertyStorage);
            }
            catch (NullReferenceException)
            {
                FadeEmptyChests.Log(LogLevel.Info,
                    "GetPropertyBlock failed, refreshing reference to renderers");
                RefreshRenderers();
                return;
            }
            if (!HasDitherModel(renderer))
            {
                propertyStorage.SetFloat("_Fade", currentFade);
            }
            else
            {
                float oldFade = propertyStorage.GetFloat("_Fade");
                propertyStorage.SetFloat("_Fade", oldFade * currentFade);
            }
            renderer.SetPropertyBlock(propertyStorage);
        }

        // clean up leftover hooks
        private void OnDestroy()
        {
            SceneCamera.onSceneCameraPreRender -= OnSceneCameraPreRender;
        }
    }
}
