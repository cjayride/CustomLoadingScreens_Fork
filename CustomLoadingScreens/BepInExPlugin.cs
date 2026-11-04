using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CustomLoadingScreens
{
    [BepInPlugin("cjayride.CustomLoadingScreens", "Custom Loading Screens", "0.9.3")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;
        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<string> loadingText;
        public static ConfigEntry<bool> differentSpawnScreen;
        public static ConfigEntry<bool> differentSpawnTip;
        public static ConfigEntry<bool> showTipsOnLoadingScreen;
        public static ConfigEntry<bool> removeVignette;
        public static ConfigEntry<Color> spawnColorMask;
        public static ConfigEntry<Color> loadingColorMask;
        public static ConfigEntry<Color> loadingTextColor;
        public static ConfigEntry<Color> tipTextColor;
        public static ConfigEntry<string> imagePath;
        public static ConfigEntry<string> tipsPath;

        public static List<string> loadingScreens = new List<string>();
        public static string[] loadingTips = new string[0];

        private static Sprite loadingSprite;
        private static Sprite loadingSprite2;
        private static string loadingTip;
        private static string loadingTip2;
        private static bool spawnScreenApplied;
        private static Image menuOverlay;
        private static Image hudOverlay;
        private static TMP_Text hudOverlayTip;

        private const int MaxCachedSprites = 4;
        private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>(MaxCachedSprites);
        private static readonly Queue<string> spriteCacheOrder = new Queue<string>(MaxCachedSprites);
        private static readonly MethodInfo loadImageBytes = typeof(ImageConversion).GetMethod(
            "LoadImage",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new Type[] { typeof(Texture2D), typeof(byte[]) },
            null);

        private void Awake()
        {
            context = this;

            modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
            differentSpawnScreen = Config.Bind("General", "DifferentSpawnScreen", true, "Use a different screen for the spawn part");
            differentSpawnTip = Config.Bind("General", "DifferentSpawnTip", true, "Use a different tip for the spawn part");
            spawnColorMask = Config.Bind("General", "SpawnColorMask", new Color(0.532f, 0.588f, 0.853f, 1f), "Change the color mask of the spawn screen (set last number to 0 to disable)");
            loadingColorMask = Config.Bind("General", "LoadingColorMask", Color.white, "Change the color mask of the initial loading screen (set to white to disable)");
            removeVignette = Config.Bind("General", "RemoveMask", true, "Remove dark edges for the spawn part");
            loadingText = Config.Bind("General", "LoadingText", "Loading...", "Custom Loading... text");
            loadingTextColor = Config.Bind("General", "LoadingTextColor", new Color(1, 0.641f, 0, 1), "Custom Loading... text color");
            tipTextColor = Config.Bind("General", "TipTextColor", Color.white, "Custom tip text color");
            showTipsOnLoadingScreen = Config.Bind("General", "ShowTipsOnLoadingScreen", true, "Show a tip on the initial loading overlay");
            imagePath = Config.Bind("General", "PathToImagesFolder", "plugins\\cjayride-CustomLoadingScreens\\CustomLoadingScreens", "Path to image files. Example: config\\CustomLoadingScreens or plugins\\cjayride-CustomLoadingScreens\\CustomLoadingScreens");
            tipsPath = Config.Bind("General", "PathToTipsFolder", "plugins\\cjayride-CustomLoadingScreens\\CustomLoadingScreens", "Path to tips file. Example: config\\CustomLoadingScreens or plugins\\cjayride-CustomLoadingScreens\\CustomLoadingScreens");

            if (!modEnabled.Value)
                return;

            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return;

            LoadCustomLoadingScreens();
            LoadTips();

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        private static string GetPluginDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        private static string GetBepInExRoot()
        {
            string pluginDir = GetPluginDirectory();
            string pluginsRoot = Path.GetDirectoryName(pluginDir);
            if (string.Equals(Path.GetFileName(pluginDir), "plugins", StringComparison.OrdinalIgnoreCase))
                return pluginsRoot;
            return Path.GetDirectoryName(pluginsRoot);
        }

        private static string GetContentDirectory(string configuredRelativePath)
        {
            string besideDll = Path.Combine(GetPluginDirectory(), "CustomLoadingScreens");
            if (Directory.Exists(besideDll))
                return besideDll;

            string configured = Path.Combine(GetBepInExRoot(), configuredRelativePath);
            if (Directory.Exists(configured))
                return configured;

            Directory.CreateDirectory(besideDll);
            return besideDll;
        }

        private void LoadTips()
        {
            string path = Path.Combine(GetContentDirectory(tipsPath.Value), "tips.txt");

            if (!File.Exists(path))
            {
                File.WriteAllText(path, string.Empty);
                loadingTips = new string[0];
                return;
            }

            string[] lines = File.ReadAllLines(path);
            int count = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    count++;
            }

            if (count == 0)
            {
                loadingTips = new string[0];
                return;
            }

            string[] tips = new string[count];
            int n = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    tips[n++] = lines[i];
            }
            loadingTips = tips;
        }

        private static void LoadCustomLoadingScreens()
        {
            loadingScreens.Clear();
            string path = GetContentDirectory(imagePath.Value);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                return;
            }

            string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string ext = Path.GetExtension(files[i]);
                if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                    loadingScreens.Add(files[i]);
            }
        }

        private static Sprite GetRandomLoadingScreen()
        {
            int count = loadingScreens.Count;
            if (count == 0)
                return null;

            string path = loadingScreens[UnityEngine.Random.Range(0, count)];
            return GetOrLoadSprite(path);
        }

        private static bool TryLoadImage(Texture2D tex, byte[] imageData)
        {
            if (loadImageBytes == null)
                return false;
            object result = loadImageBytes.Invoke(null, new object[] { tex, imageData });
            return result is bool ok && ok;
        }

        private static Sprite GetOrLoadSprite(string path)
        {
            Sprite cached;
            if (spriteCache.TryGetValue(path, out cached) && cached != null && cached.texture != null)
                return cached;

            byte[] imageData;
            try
            {
                imageData = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                context.Logger.LogWarning("Failed to read loading screen " + path + ": " + ex.Message);
                return null;
            }

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 0;
            if (!TryLoadImage(tex, imageData))
            {
                UnityEngine.Object.Destroy(tex);
                context.Logger.LogWarning("Failed to decode loading screen " + path);
                return null;
            }
            tex.Apply(false, true);

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
            sprite.name = "CLS_" + Path.GetFileName(path);

            while (spriteCache.Count >= MaxCachedSprites && spriteCacheOrder.Count > 0)
            {
                string oldest = spriteCacheOrder.Dequeue();
                Sprite old;
                if (spriteCache.TryGetValue(oldest, out old))
                {
                    spriteCache.Remove(oldest);
                    if (old != null && old != loadingSprite && old != loadingSprite2)
                    {
                        if (old.texture != null)
                            UnityEngine.Object.Destroy(old.texture);
                        UnityEngine.Object.Destroy(old);
                    }
                }
            }

            spriteCache[path] = sprite;
            spriteCacheOrder.Enqueue(path);
            return sprite;
        }

        private static void PickScreensAndTips()
        {
            spawnScreenApplied = false;
            loadingSprite = GetRandomLoadingScreen();
            loadingSprite2 = differentSpawnScreen.Value ? GetRandomLoadingScreen() : loadingSprite;

            if (loadingTips.Length > 0)
            {
                loadingTip = loadingTips[UnityEngine.Random.Range(0, loadingTips.Length)];
                loadingTip2 = differentSpawnTip.Value
                    ? loadingTips[UnityEngine.Random.Range(0, loadingTips.Length)]
                    : loadingTip;
            }
            else
            {
                loadingTip = null;
                loadingTip2 = null;
            }
        }

        private static Image EnsureOverlay(Transform parent, ref Image cached)
        {
            if (cached != null)
                return cached;

            if (parent == null)
                return null;

            Transform existing = parent.Find("CLS_Overlay");
            if (existing != null)
            {
                cached = existing.GetComponent<Image>();
                return cached;
            }

            Transform bkg = parent.Find("Bkg");
            if (bkg == null)
                return null;

            Image src = bkg.GetComponent<Image>();
            if (src == null)
                return null;

            cached = Instantiate(src, parent);
            cached.gameObject.name = "CLS_Overlay";
            cached.type = Image.Type.Simple;
            cached.preserveAspect = true;
            return cached;
        }

        private static Transform GetHudLoadingRoot(Hud hud)
        {
            if (hud == null)
                return null;
            if (hud.m_loadingScreen != null)
                return hud.m_loadingScreen.transform;
            return hud.transform.Find("LoadingBlack");
        }

        [HarmonyPatch(typeof(FejdStartup), "LoadMainScene")]
        public static class LoadMainScene_Patch
        {
            public static void Prefix(FejdStartup __instance)
            {
                PickScreensAndTips();

                if (__instance == null || __instance.m_loading == null || loadingSprite == null)
                    return;

                Image image = EnsureOverlay(__instance.m_loading.transform, ref menuOverlay);
                if (image == null)
                    return;

                image.sprite = loadingSprite;
                image.color = loadingColorMask.Value;
            }
        }

        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(ZNet), "RPC_ClientHandshake")]
        public static class ZNet_RPC_ClientHandshake_Patch
        {
            public static void Prefix(ZNet __instance)
            {
                if (__instance == null || __instance.IsServer())
                    return;

                Hud hud = Hud.instance;
                Transform root = GetHudLoadingRoot(hud);
                if (root == null || loadingSprite == null)
                    return;

                Image image = EnsureOverlay(root, ref hudOverlay);
                if (image == null)
                    return;

                image.sprite = loadingSprite;
                image.color = loadingColorMask.Value;

                if (!showTipsOnLoadingScreen.Value || loadingTips.Length == 0 || string.IsNullOrEmpty(loadingTip))
                    return;

                if (hudOverlayTip == null && hud.m_loadingTip != null)
                {
                    Transform sepParent = hud.m_loadingTip.transform.parent;
                    if (sepParent != null)
                    {
                        Transform sep = sepParent.Find("panel_separator");
                        if (sep != null && root.Find("CLS_Separator") == null)
                        {
                            Transform clone = Instantiate(sep, root);
                            clone.name = "CLS_Separator";
                        }
                    }

                    TMP_Text text = Instantiate(hud.m_loadingTip.gameObject, root).GetComponent<TMP_Text>();
                    if (text != null)
                    {
                        text.gameObject.name = "CLS_Tip";
                        hudOverlayTip = text;
                    }
                }

                if (hudOverlayTip != null)
                {
                    hudOverlayTip.text = loadingTip;
                    hudOverlayTip.color = tipTextColor.Value;
                }
            }
        }

        [HarmonyPatch(typeof(Hud), "UpdateBlackScreen")]
        public static class UpdateBlackScreen_Patch
        {
            public static void Prefix(bool ___m_haveSetupLoadScreen, ref bool __state)
            {
                if (spawnScreenApplied)
                {
                    __state = false;
                    return;
                }
                __state = !___m_haveSetupLoadScreen;
            }

            public static void Postfix(Hud __instance, bool ___m_haveSetupLoadScreen, bool __state)
            {
                if (spawnScreenApplied || !__state || !___m_haveSetupLoadScreen || __instance == null)
                    return;

                if (__instance.m_loadingImage != null && loadingSprite != null)
                {
                    __instance.m_loadingImage.sprite = differentSpawnScreen.Value ? loadingSprite2 : loadingSprite;
                    __instance.m_loadingImage.color = spawnColorMask.Value;
                }

                if (__instance.m_loadingTip != null)
                {
                    if (loadingTips.Length > 0)
                        __instance.m_loadingTip.text = differentSpawnTip.Value ? loadingTip2 : loadingTip;
                    __instance.m_loadingTip.color = tipTextColor.Value;
                }

                if (removeVignette.Value && __instance.m_loadingProgress != null)
                {
                    Transform progress = __instance.m_loadingProgress.transform;
                    DisableChild(progress, "TopFade");
                    DisableChild(progress, "BottomFade");
                    DisableChild(progress, "text_darken");
                }

                spawnScreenApplied = true;
            }

            private static void DisableChild(Transform parent, string name)
            {
                Transform child = parent.Find(name);
                if (child != null)
                    child.gameObject.SetActive(false);
            }
        }
    }
}
