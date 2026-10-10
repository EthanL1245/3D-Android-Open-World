# Manually added Snapper Island rocks

**Current state:** two .blend files were reported added locally under a folder
named "Manually added assets". Those binary Blender files and the user's newest
Scene placements have NOT yet been pushed and cannot be read from GitHub.

In the Unity Editor:
1. Place the Blender models under Assets/Manually Added Assets (Unity requires
   Blender installed for direct .blend imports; alternatively export FBX).
2. Open Assets/Scenes/PrototypeWorld.unity outside Play Mode. Put the placed
   model parents under USER PLACED SCENERY - DO NOT MODIFY BY CODE in the
   Scene Hierarchy to preserve manual ownership.
3. Run Tools > Open World > Manual Scenery > Save Snapper Rocks and Push to GitHub.
4. The tool adds static, precise non-convex MeshColliders to the existing model
   meshes near Snapper. It does NOT change any position, scale, rotation, mesh,
   material or UV. Colliders are idempotent; dynamic Rigidbody meshes are
   skipped because they cannot use non-convex static mesh collision.
5. Confirm the dialog to stage/commit/push the specific .blend source files,
   their .meta files, the actual PrototypeWorld.unity scene, and the generated
   Assets/_Game/EditableFishingMap/ManualSnapperRocks.json inventory.
6. Wait for "Uploaded to GitHub". If Git fails, the Unity scene stays saved
   locally. Use git status and resolve the push; don't run the collider pass
   again just to repeat Git upload.

## Rule for future code changes

**All user-placed scenery is owned by the user.** Do not delete, move, reparent,
rescale, retexture, or replace these rock instances, or procedural-generate new
scenery on top of them. Consult ManualSnapperRocks.json and the Scene's
USER PLACED SCENERY hierarchy for authoritative names, GUIDs, positions,
mesh asset paths and colliders. Local objects are not accessible on GitHub
until the user commits and pushes them.

Code and menu implementation are pushed separately. Actual .blend bytes,
saved scene instance changes and working collision data can only be uploaded
after Unity has saved them on the user's machine.
