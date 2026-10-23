# Suncrest Custom Paintable Terrain Textures

These two layers use the **exact textures provided in ChatGPT**:

- **Suncrest Meadow Detail** (6m tiling): an overhead natural meadow grass surface.
- **Suncrest Warm Brown Rock** (5m tiling): cracked brown stone.

The original full-resolution PNG images are distributed in the accompanying `Suncrest_Custom_Terrain_Textures.unitypackage`, because the binary PNG files are not part of this Git commit. Import the package using **Assets > Import Package > Custom Package** in Unity. Both PNGs and their GUID-preserving meta files will appear alongside these two layers in this directory. Before importing the package, the new layers will appear with missing diffuse texture references.

To paint, select **SuncrestReef > Terrain** in the Hierarchy, then **Inspector > Paint Terrain > Paint Texture > Edit Terrain Layers > Add Layer**, choosing one of these `.terrainlayer` assets. Paint manually; importing does not change your current terrain heights or existing texture paint.

If you want these PNG files to be on GitHub and shared across devices, after importing and saving them run:
`git add -A -- "Assets/_Game/Environment/TerrainLayers/Suncrest Custom Textures"`
then commit and push.
