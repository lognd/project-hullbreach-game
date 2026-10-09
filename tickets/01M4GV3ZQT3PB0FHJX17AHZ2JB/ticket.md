+++
id = "01M4GV3ZQT3PB0FHJX17AHZ2JB"
title = "Restore C# frob:tests bindings for the physics audit tests after the frob 0.533.0 pin"
type = "task"
category = "todo"
priority = "low"
reporter = "GingerVHS"
created = "2026-10-09T17:24:35Z"
updated = "2026-10-09T17:24:35Z"
labels = ["owner:GingerVHS", "game"]

[[acceptance]]
text = "Every binding listed in the body is back on its test and frob check passes on the pinned release."
bound = false
+++

The GingerVHS audit-fix tests carried these C# frob:tests bindings. They were removed in 48d4714 because frob 0.532.0 (the CI pin) cannot resolve C# test functions (TEST001). frob-v2 ~HT9HBY2 adds C# test_items; re-add the bindings once the pin moves to 0.533.0 or newer:

- // frob:tests Assets/Scripts/Hullbreach.Core/Grid/BlockKey.cs::BlockKey.Neighbors
- // frob:tests Assets/Scripts/Hullbreach.Game/GravityWorld.cs::GravityWorld
- // frob:tests Assets/Scripts/Hullbreach.Game/Powerup.cs::Powerup
- // frob:tests Assets/Scripts/Hullbreach.Game/Projectile.cs::Projectile
- // frob:tests Assets/Scripts/Hullbreach.Game/ProjectileSpawner.cs::ProjectileSpawner
- // frob:tests Assets/Scripts/Hullbreach.Game/ShipController.cs::ShipController.ReplaceBlocks
- // frob:tests Assets/Scripts/Hullbreach.Game/ShipStructure.cs::ShipStructure
- // frob:tests Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.RebuildDerivedViews
- // frob:tests Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.Step
- // frob:tests Assets/Scripts/Hullbreach.Ship/ShipContacts.cs::ShipContacts.Resolve
- // frob:tests Assets/Scripts/Hullbreach.Structure/Fem/LoadVector.cs::LoadVector.AddPointForce
- // frob:tests Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.Tick
- // frob:tests Assets/Scripts/Hullbreach.World/OrbitHelper.cs::OrbitHelper.TryCircularOrbitVelocity
