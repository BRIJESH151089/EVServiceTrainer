# Asset credits

All 3D models, materials, textures and sounds in `Assets/EVTrainer/` are self-made (Unity primitives and
script-generated meshes, a script-generated texture atlas with self-made graphics and text, procedurally
generated audio), except the items listed below.

| Asset | Source | Licence | Location |
|---|---|---|---|
| Liberation Sans font (TextMesh Pro SDF font asset) | TextMesh Pro Essential Resources, bundled with `com.unity.ugui` 2.0.0 | SIL Open Font License 1.1 (licence text included next to the font) | `Assets/TextMesh Pro/Fonts/`, `Assets/TextMesh Pro/Resources/Fonts & Materials/` |
| Left and right hand meshes (`LeftHand.fbx`, `RightHand.fbx`) | XR Hands 1.7.2 package sample "Hand Visualizer" (`Samples~/HandVisualizer/Models/`), © Unity Technologies | Licensed with the `com.unity.xr.hands` package: the package's LICENSE.md states the Unity Package Distribution License (https://unity3d.com/legal/licenses/Unity_Package_Distribution_License) | `Assets/EVTrainer/Rendering/ThirdParty/XRHandsHandVisualizer/` |

Only the two hand meshes were copied from the sample; the sample's scenes, scripts, prefabs and materials were
not imported. The hands use a self-made URP glove material.

The TextMesh Pro shaders and settings in `Assets/TextMesh Pro/` are Unity package resources
(Unity Companion License). The EmojiOne sprite sheet from the TMP essentials was not imported.
