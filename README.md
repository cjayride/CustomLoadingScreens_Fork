# CustomLoadingScreens

Updated for Valheim 1.0.21

A fork of [aedenthorn/CustomLoadingScreens](https://github.com/aedenthorn/ValheimMods/tree/master/CustomLoadingScreens) 

[ThunderStore](https://thunderstore.io/c/valheim/p/cjayride/CustomLoadingScreens) | [GitHub](https://github.com/cjayride/CustomLoadingScreens_Fork)

# Custom Loading Screens and Tips

Use your own images as custom loading screens.

Recommended image size: 1920x1080

# Setup

- Launch the game once, to generate the config file and default image folder.

# Configuration

**DEFAULT IMAGE FOLDER:**

Thunderstore / Gale / r2modman unpack the zip into one mod folder under BepInEx `plugins`, for example:

`BepInEx\plugins\cjayride-CustomLoadingScreens\`

That folder contains `CustomLoadingScreens.dll`, `manifest.json`, and a `CustomLoadingScreens` subfolder with the bundled `.png` / `.jpg` files and `tips.txt`. You can also put images next to the DLL in the same mod folder.

The zip itself is flat (no `plugins` folder inside the archive).

- Optionally, specify another directory inside the BepInEx folder (see config file).

**EDIT CONFIG FILE:**

config\cjayride.CustomLoadingScreens.cfg

- Example: You can also specify a directory in the config folder to save your images: config\mypack\CustomLoadingScreens

- You can specify any directory path inside the BepInEx folder.

# Known Issues

- On dedicated servers, if `DifferentSpawnScreen` / `DifferentSpawnTip` are disabled, the same image picked at game launch is reused for death respawn and portal travel until you restart the client.

# Contact
- 𝕏: x.com/cjayride

- Discord: discord.gg/cjayride (find me at the top of the user list) "cjayride"

- Twitch: twitch.tv/cjayride

# AI Generated

This code was not AI Generated, however, AI was used to verify that it works with the new version of the game.

