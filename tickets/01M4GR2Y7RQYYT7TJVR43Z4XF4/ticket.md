+++
id = "01M4GR2Y7RQYYT7TJVR43Z4XF4"
title = "Pin tools/plaincs dependency clone to a commit hash"
type = "security"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:31:35Z"
updated = "2026-10-09T16:31:35Z"
labels = ["origin:auditor", "security"]
scope = ["tools/plaincs/fetch_deps.sh"]

[[acceptance]]
text = "fetch_deps.sh checks out a pinned 40-hex commit and verifies it; no INV-004 policy failure"
bound = false
+++

origin: auditor. Invariant INV-004; policy rule POL-fetch-pinned-commit. git clone --branch 1.2.5 follows a movable tag and the result is compiled in CI. Fix direction: clone then git checkout <sha> and verify rev-parse. Leave frob:invariant INV-004 anchor.
