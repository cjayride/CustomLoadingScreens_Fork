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
    [BepInPlugin("cjayride.CustomLoadingScreens", "Custom Loading Screens", "0.9.6")]
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
        private static bool vignetteRemovedThisSession;
        private static Sprite lastAppliedSpawnSprite;
        private static MethodInfo loadImageBytes;

        private const int MaxCachedSprites = 4;
        private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>(MaxCachedSprites);
        private static readonly Queue<string> spriteCacheOrder = new Queue<string>(MaxCachedSprites);

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

            loadImageBytes = FindLoadImageMethod();
            if (loadImageBytes == null)
                Logger.LogError("Could not find ImageConversion.LoadImage(Texture2D, byte[]). Screens will not decode.");

            LoadCustomLoadingScreens();
            LoadTips();
            Logger.LogInfo("Found " + loadingScreens.Count + " loading images and " + loadingTips.Length + " tips.");
            if (loadingScreens.Count > 0)
            {
                Sprite warmup = GetOrLoadSprite(loadingScreens[0]);
                if (warmup != null && warmup.texture != null)
                    Logger.LogInfo("Warmup decode OK: " + warmup.texture.width + "x" + warmup.texture.height);
                else
                    Logger.LogWarning("Warmup decode failed for " + loadingScreens[0]);
            }

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        private static MethodInfo FindLoadImageMethod()
        {
            MethodInfo[] methods = typeof(ImageConversion).GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != "LoadImage")
                    continue;
                ParameterInfo[] p = methods[i].GetParameters();
                if (p.Length == 2 && p[0].ParameterType == typeof(Texture2D) && p[1].ParameterType == typeof(byte[]))
                    return methods[i];
            }
            return null;
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

        private static bool IsImageFile(string file)
        {
            string name = Path.GetFileName(file);
            if (name.Equals("icon.png", StringComparison.OrdinalIgnoreCase))
                return false;
            string ext = Path.GetExtension(file);
            return ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
        }

        private static bool DirectoryHasImages(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return false;
            try
            {
                string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    if (IsImageFile(files[i]))
                        return true;
                }
            }
            catch (Exception ex)
            {
                context.Logger.LogWarning("Could not scan " + path + ": " + ex.Message);
            }
            return false;
        }

        private static string GetContentDirectory(string configuredRelativePath)
        {
            string pluginDir = GetPluginDirectory();
            string besideDll = Path.Combine(pluginDir, "CustomLoadingScreens");
            string configured = Path.Combine(GetBepInExRoot(), configuredRelativePath);

            if (DirectoryHasImages(besideDll))
                return besideDll;
            if (DirectoryHasImages(pluginDir))
                return pluginDir;
            if (DirectoryHasImages(configured))
                return configured;

            Directory.CreateDirectory(besideDll);
            return besideDll;
        }

        private void LoadTips()
        {
            string dir = GetContentDirectory(tipsPath.Value);
            string path = Path.Combine(dir, "tips.txt");
            if (!File.Exists(path))
            {
                string beside = Path.Combine(GetPluginDirectory(), "tips.txt");
                if (File.Exists(beside))
                    path = beside;
            }

            if (!File.Exists(path))
            {
                File.WriteAllText(Path.Combine(dir, "tips.txt"), string.Empty);
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
                context.Logger.LogWarning("No loading images found. Put png/jpg files in " + path);
                return;
            }

            string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                if (IsImageFile(files[i]))
                    loadingScreens.Add(files[i]);
            }

            if (loadingScreens.Count == 0)
                context.Logger.LogWarning("No png/jpg loading images in " + path);
            else
                context.Logger.LogInfo("Using loading images from " + path);
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
            try
            {
                object result = loadImageBytes.Invoke(null, new object[] { tex, imageData });
                return result is bool ok && ok;
            }
            catch (Exception ex)
            {
                context.Logger.LogWarning("LoadImage failed: " + ex.Message);
                return false;
            }
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

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
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
            vignetteRemovedThisSession = false;
            lastAppliedSpawnSprite = null;
            if (loadingScreens.Count == 0)
                LoadCustomLoadingScreens();

            loadingSprite = GetRandomLoadingScreen();
            loadingSprite2 = differentSpawnScreen.Value ? GetRandomLoadingScreen() : loadingSprite;
            if (loadingSprite != null && loadingSprite.texture != null)
                context.Logger.LogInfo("Picked loading sprites " + loadingSprite.texture.width + "x" + loadingSprite.texture.height);
            else
                context.Logger.LogWarning("PickScreensAndTips: no loading sprite (check png/jpg decode).");

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

        private static bool IsHudLoadingVisible(Hud hud)
        {
            if (hud == null)
                return false;
            if (hud.m_loadingScreen != null && hud.m_loadingScreen.gameObject.activeInHierarchy && hud.m_loadingScreen.alpha > 0.05f)
                return true;
            return hud.m_loadingImage != null && hud.m_loadingImage.isActiveAndEnabled;
        }

        private static void ApplyToLoadingImage(Image image, Sprite sprite, Color color, bool preserveAspect)
        {
            if (image == null || sprite == null)
                return;
            image.sprite = sprite;
            image.color = color;
            image.type = Image.Type.Simple;
            image.preserveAspect = preserveAspect;
            image.enabled = true;
            image.transform.SetAsLastSibling();
        }

        private static void ApplySpawnLoadingScreen(Hud hud, Sprite sprite)
        {
            if (hud == null || sprite == null)
                return;

            ApplyToLoadingImage(hud.m_loadingImage, sprite, spawnColorMask.Value, true);

            if (hud.m_loadingTip != null && loadingTips.Length > 0)
            {
                hud.m_loadingTip.text = differentSpawnTip.Value ? loadingTip2 : loadingTip;
                hud.m_loadingTip.color = tipTextColor.Value;
            }

            if (!vignetteRemovedThisSession && removeVignette.Value && hud.m_loadingProgress != null)
            {
                Transform progress = hud.m_loadingProgress.transform;
                DisableChild(progress, "TopFade");
                DisableChild(progress, "BottomFade");
                DisableChild(progress, "text_darken");
                vignetteRemovedThisSession = true;
            }

            if (lastAppliedSpawnSprite != sprite)
            {
                lastAppliedSpawnSprite = sprite;
                context.Logger.LogInfo("Applied spawn loading screen to Hud.m_loadingImage.");
            }
        }

        private static void DisableChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
                child.gameObject.SetActive(false);
        }

        [HarmonyPatch(typeof(FejdStartup), "LoadMainScene")]
        public static class LoadMainScene_Patch
        {
            public static void Prefix(FejdStartup __instance)
            {
                PickScreensAndTips();

                if (__instance == null || __instance.m_loading == null || loadingSprite == null)
                    return;

                Transform bkg = __instance.m_loading.transform.Find("Bkg");
                if (bkg == null)
                {
                    context.Logger.LogWarning("FejdStartup.m_loading has no Bkg child.");
                    return;
                }

                Image src = bkg.GetComponent<Image>();
                if (src == null)
                    return;

                Image image = Instantiate(src, __instance.m_loading.transform);
                ApplyToLoadingImage(image, loadingSprite, loadingColorMask.Value, true);
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

                if (loadingSprite == null)
                    PickScreensAndTips();

                Hud hud = Hud.instance;
                if (hud == null || loadingSprite == null)
                    return;

                ApplyToLoadingImage(hud.m_loadingImage, loadingSprite, loadingColorMask.Value, true);

                if (!showTipsOnLoadingScreen.Value || loadingTips.Length == 0 || string.IsNullOrEmpty(loadingTip) || hud.m_loadingTip == null)
                    return;

                hud.m_loadingTip.text = loadingTip;
                hud.m_loadingTip.color = tipTextColor.Value;
            }
        }

        [HarmonyPatch(typeof(Hud), "UpdateBlackScreen")]
        public static class UpdateBlackScreen_Patch
        {
            public static void Prefix(Hud __instance, bool ___m_haveSetupLoadScreen, ref bool __state)
            {
                __state = !___m_haveSetupLoadScreen;
            }

            public static void Postfix(Hud __instance, bool ___m_haveSetupLoadScreen, bool __state)
            {
                if (__instance == null)
                    return;

                if (loadingSprite == null)
                    PickScreensAndTips();

                Sprite sprite = differentSpawnScreen.Value ? loadingSprite2 : loadingSprite;
                if (sprite == null)
                    return;

                bool setupMoment = __state && ___m_haveSetupLoadScreen;
                bool loadingVisible = IsHudLoadingVisible(__instance);

                if (!setupMoment && !loadingVisible)
                    return;

                if (__instance.m_loadingImage != null && __instance.m_loadingImage.sprite != sprite)
                    ApplySpawnLoadingScreen(__instance, sprite);
                else if (setupMoment)
                    ApplySpawnLoadingScreen(__instance, sprite);
            }
        }
    }
}
