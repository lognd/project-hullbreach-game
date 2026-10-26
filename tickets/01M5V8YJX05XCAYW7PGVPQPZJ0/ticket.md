+++
id = "01M5V8YJX05XCAYW7PGVPQPZJ0"
title = "Upstream frob: implement the unity test provider (E-TESTS-UNITY-PROVIDER in 0.532.0)"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-26T04:54:24Z"
updated = "2026-10-26T04:54:24Z"
labels = ["upstream-frob"]

[[acceptance]]
text = "frob test --base runs or delegates Unity asmdef tests and records evidence instead of refusing."
bound = false
+++

frob test --base refuses with E-TESTS-UNITY-PROVIDER when the touched set selects a test under a Unity .asmdef (crates/frob-tests/src/lib.rs, unity_groups). Gap is in frob core (not modified here). Workaround: command provider with run_tests.sh, see docs/frob.md. Found while working ~XC7G41Z.
