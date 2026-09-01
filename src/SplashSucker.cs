using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using static UnityEngine.Object;
using UnityEngine.UI;

namespace TFROnlineMenu;

internal static class SplashSucker
{
    const float SecondScreenHoldSeconds = 5f;
    const float SecondScreenFadeOutSeconds = 1.25f;
    const float VersionBand = 88f;
    const float EdgePad = 20f;

    static LemonAction? _syncAlpha;
    static int _secondScreenPhase;
    static float _holdUntil;
    static float _fadeOutStart;

    [HarmonyPatch(typeof(FRMain), nameof(FRMain.Start))]
    internal static class MainsceneLoadingNoticePatch
    {
        static void Postfix()
        {
            GameObject.Find("Camera").GetComponent<Camera>().backgroundColor = Color.black;
            var loadingTmp = GameObject.Find("Text (TMP)").GetComponent<TextMeshProUGUI>();
            loadingTmp.rectTransform.sizeDelta = new Vector2(900f, 220f);
            loadingTmp.rectTransform.anchoredPosition = new Vector2(0f, 40f);
            loadingTmp.text = "This game is a fan-work of Touhou Project\n" +
                              "Touhou Project belongs to Team Shangai Alice ( ZUN ), \n"+
                              "Touhou Project \"FumoFumo\" designed by ROYALCAT, products from GIFT.\n" +
                              "They are NOT affiliated with this fan game. \n" +
                              "All content is the property of their respective owners.";
            loadingTmp.alignment = TextAlignmentOptions.Center;
            loadingTmp.fontSize = 18f;

            var nowTmp = Instantiate(loadingTmp, loadingTmp.rectTransform.parent);
            nowTmp.rectTransform.sizeDelta = new Vector2(600f, 48f);
            nowTmp.rectTransform.anchoredPosition = new Vector2(0f, -200f);
            nowTmp.text = "Now Loading, , , ";
            nowTmp.fontSize = 32f;
            nowTmp.alignment = TextAlignmentOptions.Center;
            nowTmp.color = Color.white;
        }
    }

    [HarmonyPatch(typeof(SplashScript), nameof(SplashScript.Start))]
    internal static class SplashSecondScreenPatch
    {
        static void Postfix(SplashScript __instance)
        {
            if (Application.isBatchMode) return;

            var canvas = GameObject.Find("Canvas").transform;
            var noticeTmp = canvas.Find("Text (TMP)").gameObject.GetComponent<TextMeshProUGUI>();
            noticeTmp.text = string.Empty;

            __instance.CancelInvoke();
            __instance.Invoke(nameof(SplashScript.EndSplash), 22f);

            var sprite = ModUiUtils.LoadEmbeddedSprite("LinkSplash.jpg");
            var font = ModUiUtils.LoadTmpFont("Alphakind") ?? noticeTmp.font;
            var logoImage = GameObject.Find("Logo").GetComponent<Image>();

            var overlayGo = Instantiate(logoImage.gameObject, canvas);
            overlayGo.name = "OnlineMenuSplashOverlay";
            Destroy(overlayGo.GetComponent<Image>());

            var overlayRect = overlayGo.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.localScale = Vector3.one;
            overlayRect.localRotation = Quaternion.identity;
            overlayRect.SetAsLastSibling();

            var group = overlayGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var versionTmp = Instantiate(noticeTmp, overlayRect);
            var versionRect = versionTmp.rectTransform;
            versionRect.anchorMin = new Vector2(0f, 1f);
            versionRect.anchorMax = new Vector2(1f, 1f);
            versionRect.pivot = new Vector2(0.5f, 1f);
            versionRect.anchoredPosition = new Vector2(0f, -EdgePad);
            versionRect.sizeDelta = new Vector2(-(EdgePad * 2f), 56f);
            versionTmp.font = font;
            versionTmp.text = $"OnlineMenuMod V.{ModUiUtils.GetModVersion()}";
            versionTmp.fontSize = 36f;
            versionTmp.alignment = TextAlignmentOptions.Center;
            versionTmp.color = Color.white;

            var imageHostGo = Instantiate(logoImage.gameObject, overlayRect);
            Destroy(imageHostGo.GetComponent<Image>());

            var imageHostRect = imageHostGo.GetComponent<RectTransform>();
            imageHostRect.anchorMin = Vector2.zero;
            imageHostRect.anchorMax = Vector2.one;
            imageHostRect.offsetMin = new Vector2(EdgePad, EdgePad);
            imageHostRect.offsetMax = new Vector2(-EdgePad, -VersionBand);
            imageHostRect.localScale = Vector3.one;

            var image = Instantiate(logoImage, imageHostRect);
            var imageRect = image.rectTransform;

            image.preserveAspect = false;
            image.color = Color.white;
            image.sprite = sprite;
            if (sprite is null) return;
            var host = imageHostRect.rect;
            imageRect!.sizeDelta = new Vector2(host.height * sprite.texture.width / sprite.texture.height, host.height);
            _syncAlpha = () => FuckSecondScreen(noticeTmp, group, __instance);
            MelonEvents.OnUpdate.Subscribe(_syncAlpha);
        }
    }

    static void FuckSecondScreen(TextMeshProUGUI driver, CanvasGroup group, SplashScript splash)
    {
        if (_secondScreenPhase == 0)
        {
            group.alpha = driver.color.a;
            if (driver.color.a < 0.95f) return;

            _secondScreenPhase = 1;
            _holdUntil = Time.unscaledTime + SecondScreenHoldSeconds;
            group.alpha = 1f;
        }
        else if (_secondScreenPhase == 1)
        {
            group.alpha = 1f;
            if (Time.unscaledTime < _holdUntil) return;

            _secondScreenPhase = 2;
            _fadeOutStart = Time.unscaledTime;
        }
        else
        {
            var t = (Time.unscaledTime - _fadeOutStart) / SecondScreenFadeOutSeconds;
            group.alpha = Mathf.Clamp01(1f - t);
            if (t < 1f) return;

            FuckSplash();
            splash.CancelInvoke();
            splash.EndSplash();
        }
    }

    public static void FuckSplash()
    {
        MelonEvents.OnUpdate.Unsubscribe(_syncAlpha);
    }
}
