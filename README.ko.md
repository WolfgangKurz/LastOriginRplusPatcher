[한국어](README.ko.md) | [English](README.md)

# RplusPatcher
`RplusPatcher`는 `VALOFE`에서 서비스하는 `LastOrigin` PC 클라이언트용 [BepInEx](https://github.com/BepInEx/BepInEx) 프리패처입니다.
이 프로젝트의 목적은 스킨의 R+ 모드를 활성화하고 강제로 적용하는 것입니다.

이 프리패처는 에셋을 교체하지 않으며, R+ 모드를 활성화만 합니다.
따라서 R+ 스킨을 실제로 보려면 R+ 데이터가 포함된 AssetBundle이 필요합니다.
AssetLoader 사용을 권장합니다.

## 설치 방법
※ 이미 BepInEx를 설치했다면 `3.` 단계부터 진행하세요.

1. [BepInEx 다운로드 페이지](https://github.com/BepInEx/BepInEx/releases/tag/v6.0.0-pre.2)에서 BepInEx 6의 **BepInEx-Unity.Mono-win-x64** 버전을 다운로드합니다.
2. 다운로드한 파일의 압축을 해제한 뒤, 그 안의 파일들을 `LastOrigin.exe`가 있는 설치 폴더에 복사합니다.\
이때 `winhttp.dll` 파일과 `LastOrigin.exe` 파일이 같은 폴더에 있어야 합니다.
3. [Releases 페이지](https://github.com/WolfgangKurz/LastOriginRplusPatcher/releases)에서 다운로드한 `RplusPatcher.dll` 파일을 `BepInEx/patchers` 폴더 안에 넣습니다.

## 라이선스
`RplusPatcher`는 `LGPL-2.1 license`로 배포됩니다.
