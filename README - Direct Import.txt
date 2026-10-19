FEMALE IDLE - DIRECT UNITY IMPORT
--------------------------------
1. Close the broken Unity Import Package dialog. Exit Unity if it is spamming errors.
2. Extract this ZIP directly into your Unity project ROOT folder.
   This will create Assets/Manually Added Assets/Female Idle.fbx and its .meta file.
3. Reopen Unity and let assets import and scripts compile.
4. Optionally run Tools > Open World > Rebuild Female Multiplayer Avatar.
5. Run: git add -A -- "Assets/Manually Added Assets/Female Idle.fbx" "Assets/Manually Added Assets/Female Idle.fbx.meta"
6. Run: git commit -m "Add female idle animation"
7. Run: git push upstream main
Do not overwrite Female Character Model.fbx or its material assets.
