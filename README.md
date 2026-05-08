[한국어](README.ko.md) | [English](README.md)

# RplusPatcher
`RplusPatcher` is a [BepInEx](https://github.com/BepInEx/BepInEx) prepatcher for the PC client of `LastOrigin`, published by `VALOFE`.
The purpose of this project is to enable and force skins into R+ mode.

This prepatcher does not replace any assets; it only activates R+ mode.
Therefore, to actually see R+ skins, you need AssetBundles that contain the R+ data.
Using AssetLoader is recommended.

## Installation
※ If you have already installed BepInEx, start from step `3.`.

1. Download the **BepInEx-Unity.Mono-win-x64** version of [BepInEx](https://github.com/BepInEx/BepInEx) 6 from the [download page](https://github.com/BepInEx/BepInEx/releases/tag/v6.0.0-pre.2).
2. Unzip the downloaded file and paste its contents into the installation folder where `LastOrigin.exe` is located.\
At this point, the `winhttp.dll` file and the `LastOrigin.exe` file must be in the same folder.
3. Download `RplusPatcher.dll` from the [Releases page](https://github.com/WolfgangKurz/LastOriginRplusPatcher/releases), then place it inside the `BepInEx/patchers` folder.

## License
`RplusPatcher` is distributed under the `LGPL-2.1 license`.
