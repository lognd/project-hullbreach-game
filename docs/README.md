# Docs index

- `getting-started.md`: read this if you have never used Unity or written
  code and just want to install the project, play the demo, and make
  small changes safely.
- `architecture.md`: read this if you are ready to read the C# code and
  want to understand how the project is split up and why.
- `adding-a-block-behaviour.md`: read this if you want to add a new
  weapon/thruster variant, or a whole new block type, in code.
- `demo-scene.md`: read this if you need the exact object layout of
  `DemoScene.unity`, including every wired-up Inspector reference.
- `testing.md`: read this if you are writing or running the automated
  tests.
- `netcode.md`: read this if you are working on `Hullbreach.Net` or the
  authoritative-server story (S47/S48); it covers the wire message
  design and quantization, and what is not wired up yet.
- `roadmap.md`: read this if you want to know what is built, in
  progress, or not started yet, against the course's story numbers.
- `design/ui-port.md`: read this if you are working on the HUD or any
  new screen; it is the spec for moving the IMGUI HUD to Unity UI
  (uGUI prefabs), who owns which part, and the dated plan for the
  screens that build on it.
- `design/frob-and-backlog.md`: read this before touching tickets, frob
  directives or code comments; it specs the frob wiring, the comment
  style sweep, and how the Jira backlog maps onto frob tickets.
- `frob.md`: read this if you have never used frob; it is the
  teammate-facing companion to `design/frob-and-backlog.md` above --
  what frob is here, the comment rules, the ticket workflow, and the
  current CHECK001 blocker.
- `backlog.md`: the Jira -> frob ticket cross-reference for this repo,
  one row per imported issue.
- `reference/`: one page per assembly, the target of every
  `// frob:doc` link in the code -- read the one for the assembly you are
  touching: [hullbreach-core.md](reference/hullbreach-core.md),
  [hullbreach-world.md](reference/hullbreach-world.md),
  [hullbreach-structure.md](reference/hullbreach-structure.md),
  [hullbreach-ship.md](reference/hullbreach-ship.md),
  [hullbreach-builder.md](reference/hullbreach-builder.md),
  [hullbreach-net.md](reference/hullbreach-net.md),
  [hullbreach-game.md](reference/hullbreach-game.md),
  [hullbreach-editor.md](reference/hullbreach-editor.md).
