# v0.9.6
- Fix spawn loading screen never applying when Valheim fast-load leaves m_haveSetupLoadScreen already true
- Write directly to Hud.m_loadingImage again (removed overlay-only path); restore FejdStartup Bkg clone
- Re-apply custom sprite while the loading overlay is visible if the game resets it
- Startup warmup decode log for easier troubleshooting

# v0.9.5
- Thunderstore zip layout: flat mod root (`CustomLoadingScreens.dll` at archive root, not under `plugins/`)

# v0.9.4
- Fixed missing screens when images sit next to the DLL (Thunderstore/Gale layout) instead of only in an empty CustomLoadingScreens subfolder
- Do not treat an empty image folder as a valid content path
- Restored texture decode (removed Apply that could blank Unity 6 textures)
- Log how many images/tips were found at startup

# v0.9.3
- Version bump for Thunderstore (0.9.2 was already published there); same build as v0.9.2

# v0.9.2
- Verified Harmony patches against Valheim 1.0.21 (Unity 6000.0.75f1)
- Load only the chosen screenshot textures (small LRU cache, non-readable GPU textures)
- Skip dedicated-server / headless init
- Reuse overlay Image instances instead of cloning every handshake
- Cheap early-out on Hud.UpdateBlackScreen after the spawn screen is applied
- Null-safe HUD lookups using m_loadingScreen when present
- Bumped BepInExPack_Valheim dependency to 5.4.2360

# v0.9.1
- Fixed Harmony patch on ZNet.RPC_ClientHandshake for Valheim 1.0

# v0.9.0
- Updated for Valheim 1.0
- Bumped BepInExPack_Valheim dependency to 5.4.2350

# v0.8.0
- Removed terminal reload
- Added compatibility for v0.217.46

# v0.7.1
- Fixed issue with missing assembly MonoMod

# v0.7.0
- Two changes (commits) by "aedenthorn" on Aug 25, 2023 and Oct 6, 2023
- Keeps the cjayride changes from previous versions

# v0.5.2
- Fixed issue with default filepath in config file
- Renamed config file to cjayride.CustomLoadingScreens
- Added free images and viking quotes you can use (or not)

# v0.5.1
- Update for cjaycraft modpack
- Re-added two configuration parameters to the configuration file: PathToImagesFolder and PathToTipsFolder --- This is to allow for easier customization with modpacks

# v0.5.0
- Fix for mistlands update

# v0.4.2
- Changed default image folder
- Can now specify an image directory path in either plugins folder or config folder -- anywhere inside the BepInEx folder!
- Received report on Discord that mod wasn't working. Make sure you generate the image folder and place your images in the folder before launching the game. The game will load these images into the game during startup.