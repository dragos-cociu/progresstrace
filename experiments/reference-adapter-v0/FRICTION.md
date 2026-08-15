# Dogfood friction

All five projections passed all three CLI stages. The main friction was manual: obligations and signals cannot be derived from invocation metadata, so each needed an explicitly authored projection. Observed closeout metadata and inferred termination provenance also required careful separation. The canonical coherence rule required the explicitly projected harness stop to use harness-lifecycle provenance; other declarations use adapter-inference. The runner performs one Release CLI build, then uses `--no-build` so each retained invocation stays focused on evaluation.
