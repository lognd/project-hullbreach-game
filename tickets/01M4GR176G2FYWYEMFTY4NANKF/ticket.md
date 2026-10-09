+++
id = "01M4GR176G2FYWYEMFTY4NANKF"
title = "OrbitHelper.CircularOrbitVelocity returns NaN for negative-Mu bodies and silent zero on invalid index"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:30:39Z"
updated = "2026-10-09T17:04:07Z"
labels = ["origin:auditor"]
scope = ["Assets/Scripts/Hullbreach.World/OrbitHelper.cs"]

[[acceptance]]
text = "Given a bad index, center position, or non-positive Mu, when TryCircularOrbitVelocity runs, then it returns false with zero velocity and no NaN"
bound = true
+++

OrbitHelper.cs:221-233. (1) Failure is indistinguishable from success: invalid bodyIndex (line 223) or position at center (226) returns float2.zero, so Game/OrbitStarter.cs:30 and Game/DemoMode.cs:173 call ResetTo with zero velocity silently (serialized bodyIndex goes stale since GravityField.Remove shifts indices). (2) Negative Mu (repulsive wells are supported, see AntiGravityGunBehaviour WellMu=-40) makes AccelerationMagnitude negative, so math.sqrt(accel*dist) at line 229 is NaN, which then poisons ship state. Fix: return bool TryCircularOrbitVelocity(field, idx, pos, out float2 v) that fails for bad index, center, or Mu<=0 (no circular orbit exists around a repeller); callers log and skip on failure; add tests for each failure.
