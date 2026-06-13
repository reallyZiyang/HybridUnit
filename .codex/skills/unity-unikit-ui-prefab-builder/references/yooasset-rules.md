# YooAsset Rules

The project uses YooAsset package `GameArt`.

Current package flags:

- `EnableAddressable`: true
- `SupportExtensionless`: true
- `LocationToLower`: false
- `IncludeAssetGUID`: false
- `AutoCollectShaders`: true

Runtime keys should be extensionless file names, not paths, because the collector uses `AddressByFileName`.

Examples:

- `UI_MainMenuView`
- `SA_UI_Menu`
- `SA_UI_Battle`
- `SA_UI_Result`
- `SA_UI_Icon`
- `SA_UI_Misc`
- `UI_Palette`

## Collector Groups

- `Assets/Res/Data`: `CollectAll`, `AddressByFileName`, `PackDirectory`
- `Assets/Res/Render`: `CollectAll`, `AddressByFileName`, `PackDirectory`
- `Assets/Res/Shader`: `CollectAll`, `AddressByFileName`, `PackDirectory`

UI collectors:

- `Assets/Res/UI/Atlas`: `CollectAll`
- `Assets/Res/UI/Views`: `CollectPrefab`
- `Assets/Res/UI/Common`: `CollectPrefab`
- `Assets/Res/UI/Common/Font`: `CollectAll`
- `Assets/Res/UI/Common/Material`: `CollectAll`
- `Assets/Res/UI/Common/Shader`: `CollectAll`

## Sprite Atlas Mapping

Sprite files are not collected directly under UI sprite folders. They are included through sprite atlases:

- `SA_UI_Icon` packs `Assets/Res/UI/Common/Sprites/Icon`
- `SA_UI_Misc` packs `Assets/Res/UI/Common/Sprites/Misc`
- `SA_UI_Menu` packs `Assets/Res/UI/Views/Menu/Sprites`
- `SA_UI_Battle` packs `Assets/Res/UI/Views/Battle/Sprites`
- `SA_UI_Result` packs `Assets/Res/UI/Views/Result/Sprites`

Do not add loose UI sprites outside these atlas-backed folders unless the collector settings are intentionally updated.

After adding or changing collected assets, rebuild YooAsset bundles before expecting runtime loads or build artifacts to reflect the change.
