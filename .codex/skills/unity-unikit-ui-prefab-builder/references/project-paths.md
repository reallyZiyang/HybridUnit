# Project Paths

Use these paths for this Hybrid Unity project.

## UI Resources

- View prefabs: `Assets/Res/UI/Views/{Menu|Battle|Result}/Prefabs/Views`
- Panel prefabs: `Assets/Res/UI/Views/{Menu|Battle|Result}/Prefabs/Panels`
- Item prefabs: `Assets/Res/UI/Views/{Menu|Battle|Result}/Prefabs/Items`
- Common prefabs: `Assets/Res/UI/Common/Prefabs/{Views|Panels|Items}`
- Common icons: `Assets/Res/UI/Common/Sprites/Icon`
- Common misc sprites: `Assets/Res/UI/Common/Sprites/Misc`
- Menu sprites: `Assets/Res/UI/Views/Menu/Sprites`
- Battle sprites: `Assets/Res/UI/Views/Battle/Sprites`
- Result sprites: `Assets/Res/UI/Views/Result/Sprites`
- Common fonts: `Assets/Res/UI/Common/Font`
- Common materials: `Assets/Res/UI/Common/Material`
- Common shaders: `Assets/Res/UI/Common/Shader`
- Sprite atlases: `Assets/Res/UI/Atlas`

## Runtime Code

- UI feature code: `Assets/Scripts/Game/Play/Runtime/UI/View`
- UI extensions: `Assets/Scripts/Game/Play/Runtime/UI/Extensions`
- UniKit UI package: `Packages/UniKit-UI`

## Current UI View Buckets

The current UI buckets are `Menu`, `Battle`, and `Result`. If a new screen does not fit one of these buckets, add a new bucket under `Assets/Res/UI/Views/{Name}` with the same `Prefabs/{Views|Panels|Items}` and `Sprites` structure, then ensure it is covered by the existing `Assets/Res/UI/Views` collector and create a matching atlas if it needs view-specific sprites.
