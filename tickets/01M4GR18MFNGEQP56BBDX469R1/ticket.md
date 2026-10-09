+++
id = "01M4GR18MFNGEQP56BBDX469R1"
title = "BlockGrid.TrySet can change TypeId to/from Core, desyncing CoreKey and the one-core invariant"
type = "bug"
category = "done"
outcome = "fixed"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:30:40Z"
updated = "2026-10-09T16:53:19Z"
labels = ["origin:auditor", "audit:hullbreach-core"]
scope = ["Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs"]

[[acceptance]]
text = "Given an existing block, when TrySet would change Core-ness, then it returns false"
bound = false
+++

Contract: TryAdd enforces a single core and records CoreKey (BlockGrid.cs:118-123); TryRemove refuses to delete the core (l.144). TrySet (l.174-201) accepts any Block for an existing key with no TypeId check, so TrySet(k, new Block(BlockTypes.Core)) makes a second core without CoreKey update, and TrySet(CoreKey, hullBlock) leaves CoreKey pointing at a non-core, defeating Connectivity.ReachableFromCore. Also a TypeId change is not a topology-neutral edit only if it changes Core-ness. Callers today only use WithDamage/WithModifiers, so it is latent. Fix: in TrySet return false when (old.TypeId==Core) != (block.TypeId==Core) (or document and restrict TrySet to same TypeId, returning false otherwise); add tests.
