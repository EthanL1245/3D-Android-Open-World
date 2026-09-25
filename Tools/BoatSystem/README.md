# Suncrest Marina and deployable boats

## Install

Pull main, open the saved Suncrest Reef / PrototypeWorld scene, exit Play Mode, then click **Tools → Setup Boat System**. The command saves the scene and creates the materials, BaseBoat/Raft, Sailboat and Yacht prefabs in `Assets/Resources/Boats`. The three BoatData assets are included in source control; setup links their generated prefabs. No manual component wiring or layer assignment is needed. Unity-generated prefab/material meta files are created alongside those assets by the editor. Re-running setup replaces only the generated marina/query surface and preserves existing boat prefabs, meshes, prices and tuning.

Play and approach **SUNCREST MARINA**. Press the on-screen MARINA SHOP button (or E), buy a permanent boat, and select hotbar **slot 2**. Aim at water: green means clear; red means blocked/shallow. Click or tap PLACE BOAT. Slot 2 cancels. Buy/equip another owned boat at the marina. Prices: Raft 150, Sailboat 1,800, Yacht 9,000 coins. Existing saves start with no owned boats; no currency or catches are reset.

Approach the stern and press BOARD BOAT if needed. At the seat press DRIVE BOAT; use the existing joystick or WASD. Press LEAVE HELM to walk and fish on the deck while the hull coasts. Jump over the low gunwale to disembark. Only one driver can hold the helm. Capacities include the driver: 2 / 4 / 8. The current game is single-player; this does not add networking or replicated passenger authority.

## Architecture

- `BoatData`: stable ID, cost, speed, capacity, footprint/draft and prefab. Swap artwork under `ModelContainer`; leave the independent deck/hull colliders, DriverSeat and DeckExit in place. If a replacement changes hull size, update the colliders and BoatData footprint/draft together.
- `BoatSystem`: slot selection, mouse/mobile placement, marina UI, one active boat, and travel cleanup. Occupied boats cannot be relocated. Switching boat types deactivates the old hull before spawning the replacement.
- `ShopLedger`: permanent owned boat IDs and equipped ID share the existing shop save/backup with coins. Duplicate purchases, negative prices, insufficient funds and unowned equips are rejected. ShopProgress's read-only recovery protection is respected.
- `BoatClearance`: Water-layer ray, terrain depth samples over the expanded footprint, and an oriented OverlapBox including terrain and non-trigger obstacles. Requires known terrain, excludes ponds, and reserves a 1 m horizontal margin plus 0.8 m under-keel clearance. Placement rejects intervening walls/rocks. A moving hull checks its predicted path/turn and brakes before insufficient clearance.
- `BoatController`: Unity 6 Rigidbody forces/torque, linearDamping/angularDamping (Unity 6 names for drag/angularDrag), wave-height buoyancy and strong lateral resistance. No kinematic steering or hard velocity zeroing for normal stopping.
- `BoatPassenger`: applies the deck's position/rotation delta through CharacterController.Move before player movement. This avoids parenting a CharacterController into the Rigidbody hierarchy. At the helm walking pauses; on deck the existing walking, camera and fishing code remain active.
- `ShopDimensionManager`: every existing teleport destroys the active hull and detaches the passenger. Ownership persists. Return travel to the island uses IslandDockArrival instead of the previous offshore position. Travel to Home/Quay still uses those destinations' own arrival points.
- Setup verifies Water/Terrain layers, searches shoreline candidates against colliders AND renderer bounds (including non-solid bushes/rocks), adds a ramp and a clear arrival point. It never deletes scenery to force a fit. Failure to find a site stops before scene changes with a clear message. The separate Water-layer trigger is only a placement query surface; existing movement and fishing rays ignore triggers.

## Validation

Run `dotnet run --project Tools/BoatSystem/Checks/Checks.csproj -- <repo-root>` for the actual ShopLedger purchase/equip checks and Roslyn syntax checks of project C# sources. These are not a Unity build or physics test.

Unity/device acceptance checks:

1. Run setup twice: one marina, one Water query surface, unchanged boat ownership/assets. Check ramp, lettering, counter and arrival are clear of scenery.
2. Buy, equip, restart: coins charged exactly once, permanent unlock preserved. Try insufficient funds and switching boats.
3. Try dry land, shallow shelf, a rock at the hull edge, pond and open reef water. Only the fully clear/deep footprint may turn green or place. Test both desktop click and mobile PLACE.
4. Place twice: only one hull. Occupied hull cannot move. Board from water, drive, release joystick and observe gliding stop; turn near shore and verify braking.
5. Leave helm while moving: walk/jump and cast from the deck without sliding off or entering swimming. Test reef waves and phone frame rates.
6. Travel while aboard/driving/placing: boat disappears, no stuck input or parenting. Return to Suncrest: stand at marina dock; boat remains owned and can be redeployed.

Unity Editor/Android are not available in the remote code workspace, so generation, full Unity compilation, physics and visual acceptance must be checked in the editor after setup.
