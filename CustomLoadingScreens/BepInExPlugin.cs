using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CustomLoadingScreens
{
    [BepInPlugin("cjayride.CustomLoadingScreens", "Custom Loading Screens", "0.8.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static readonly bool isDebug = true;
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

        public static List<string> loadingScreens = new List<string>();
        public static Dictionary<string, string> loadingScreens2 = new Dictionary<string, string>();
        public static Dictionary<string, DateTime> fileWriteTimes = new Dictionary<string, DateTime>();
        public static List<string> screensToLoad = new List<string>();
        public static string[] loadingTips = new string[0];
        public static Dictionary<string, Texture2D> cachedScreens = new Dictionary<string, Texture2D>();

        private static Sprite loadingSprite;
        private static Sprite loadingSprite2;
        private static string loadingTip;
        private static string loadingTip2;
        public static ConfigEntry<string> imagePath;
        public static ConfigEntry<string> tipsPath;

        private void Awake()
        {
            context = this;

            modEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            differentSpawnScreen = Config.Bind<bool>("General", "DifferentSpawnScreen", true, "Use a different screen for the spawn part");
            differentSpawnTip = Config.Bind<bool>("General", "DifferentSpawnTip", true, "Use a different tip for the spawn part");
            spawnColorMask = Config.Bind<Color>("General", "SpawnColorMask", new Color(0.532f, 0.588f, 0.853f, 1f), "Change the color mask of the spawn screen (set last number to 0 to disable)");
            loadingColorMask = Config.Bind<Color>("General", "LoadingColorMask", Color.white, "Change the color mask of the initial loading screen (set to white to disable)");
            removeVignette = Config.Bind<bool>("General", "RemoveMask", true, "Remove dark edges for the spawn part");
            loadingText = Config.Bind<string>("General", "LoadingText", "Loading...", "Custom Loading... text");
            loadingTextColor = Config.Bind<Color>("General", "LoadingTextColor", new Color(1, 0.641f, 0, 1), "Custom Loading... text color");
            tipTextColor = Config.Bind<Color>("General", "TipTextColor", Color.white, "Custom tip text color");
            imagePath = Config.Bind<String>("General", "PathToImagesFolder", "plugins\\cjayride-CustomLoadingScreens\\CustomLoadingScreens", "Path to image files. Example: config\\CustomLoadingScreens or plugins\\cjayride-CustomLoadingScreens\\CustomLoadingScreens");
            tipsPath = Config.Bind<String>("General", "PathToTipsFolder", "plugins\\cjayride-CustomLoadingScreens\\CustomLoadingScreens", "Path to tips file. Example: config\\CustomLoadingScreens or plugins\\cjayride-CustomLoadingScreens\\CustomLoadingScreens");

            if (!modEnabled.Value)
                return;

            LoadCustomLoadingScreens();
            LoadTips();

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        private void LoadTips()
        {

            string parent_directory_path, second_parent_dir_path, third_parent_dir_path;
            parent_directory_path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            second_parent_dir_path = Path.GetDirectoryName(parent_directory_path);
            third_parent_dir_path = Path.GetDirectoryName(second_parent_dir_path);
            string path = Path.Combine(third_parent_dir_path, tipsPath.Value, "tips.txt");

            if (!File.Exists(path))
            {
                File.Create(path);
                return;
            }
            loadingTips = File.ReadAllLines(path);
        }

        private static void LoadCustomLoadingScreens()
        {
            loadingScreens.Clear();
            string parent_directory_path, second_parent_dir_path, third_parent_dir_path;
            parent_directory_path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            second_parent_dir_path = Path.GetDirectoryName(parent_directory_path);
            third_parent_dir_path = Path.GetDirectoryName(second_parent_dir_path);
            string path = Path.Combine(third_parent_dir_path, imagePath.Value);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                return;
            }

            foreach (string file in Directory.GetFiles(path, "*.png", SearchOption.AllDirectories))
            {
                loadingScreens.Add(file);
            }
        }

        private static Sprite GetRandomLoadingScreen()
        {
            if (!loadingScreens.Any())
                return null;

            Texture2D tex = new Texture2D(2, 2);
            byte[] imageData = File.ReadAllBytes(loadingScreens[UnityEngine.Random.Range(0, loadingScreens.Count)]);
            tex.LoadImage(imageData);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, 1);
        }

        [HarmonyPatch(typeof(FejdStartup), "LoadMainScene")]
        public static class LoadMainScene_Patch
        {

            public static void Prefix(FejdStartup __instance)
            {
                loadingSprite = GetRandomLoadingScreen();
                if (differentSpawnScreen.Value)
                    loadingSprite2 = GetRandomLoadingScreen();

                if (loadingTips.Any())
                {
                    loadingTip = loadingTips[UnityEngine.Random.Range(0, loadingTips.Length)];
                    if (differentSpawnTip.Value)
                        loadingTip2 = loadingTips[UnityEngine.Random.Range(0, loadingTips.Length)];
                }


                Image image = Instantiate(__instance.m_loading.transform.Find("Bkg").GetComponent<Image>(), __instance.m_loading.transform);
                if (image == null)
                {
                    return;
                }

                image.sprite = loadingSprite;
                image.color = loadingColorMask.Value;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;

            }
        }
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(ZNet), "RPC_ClientHandshake")]
        public static class ZNet_RPC_ClientHandshake_Patch
        {
            public static bool Prefix(ZNet __instance, ZRpc rpc, bool needPassword)
            {

                if (!__instance.IsServer())
                {
                    Image image = Instantiate(Hud.instance.transform.Find("LoadingBlack").Find("Bkg").GetComponent<Image>(), Hud.instance.transform.Find("LoadingBlack").transform);
                    if (image == null)
                    {
                        return true;
                    }

                    image.sprite = loadingSprite;
                    image.color = loadingColorMask.Value;
                    image.type = Image.Type.Simple;
                    image.preserveAspect = true;
                    if (loadingTips.Any())
                    {
                        Instantiate(Hud.instance.m_loadingTip.transform.parent.Find("panel_separator"), Hud.instance.transform.Find("LoadingBlack").transform);
                        TMP_Text text = Instantiate(Hud.instance.m_loadingTip.gameObject, Hud.instance.transform.Find("LoadingBlack").transform).GetComponent<TMP_Text>();
                        if (text != null)
                        {
                            text.text = loadingTip;
                            text.color = tipTextColor.Value;
                        }
                    }

                }

                return true;
            }
        }
        [HarmonyPatch(typeof(Hud), "UpdateBlackScreen")]
        public static class UpdateBlackScreen_Patch
        {

            public static void Prefix(Hud __instance, bool ___m_haveSetupLoadScreen, ref bool __state)
            {
                __state = !___m_haveSetupLoadScreen;
            }
            public static void Postfix(Hud __instance, bool ___m_haveSetupLoadScreen, ref bool __state)
            {
                if (__state && ___m_haveSetupLoadScreen)
                {
                    __instance.m_loadingImage.sprite = differentSpawnScreen.Value ? loadingSprite2 : loadingSprite;
                    __instance.m_loadingImage.color = spawnColorMask.Value;

                    if (loadingTips.Any())
                    {
                        __instance.m_loadingTip.text = differentSpawnTip.Value ? loadingTip2 : loadingTip;
                    }
                    __instance.m_loadingTip.color = tipTextColor.Value;

                    if (removeVignette.Value)
                    {
                        __instance.m_loadingProgress.transform.Find("TopFade").gameObject.SetActive(false);
                        __instance.m_loadingProgress.transform.Find("BottomFade").gameObject.SetActive(false);
                        __instance.m_loadingProgress.transform.Find("text_darken").gameObject.SetActive(false);
                    }
                }
            }
        }       
    }
}
