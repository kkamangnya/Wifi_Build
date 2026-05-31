# TODO Roadmap

## Completed In Current MVP

- Dedicated wall endpoint handles, bounding-box resize handles, rotate handles, drag move, Shift-axis constraint, Ctrl-drag duplicate, Delete, Undo/Redo.
- Visual layer panel with heatmap, structures, objects, AP, and user show/hide toggles plus per-selection visible/locked state.
- Recent project persistence and autosave recovery file support.
- Full AP inspector editing for channel, bandwidth, antenna gain, coverage target, Tx power, and enabled state.
- Route editing mode for placing path points directly on the canvas.
- Door/window partial attenuation support in the RF material-loss path.
- Region-aware heatmap cache invalidation API.
- CSV analysis, SVG plan, PNG heatmap, and PDF summary export.
- Beginner wizard starter workflow.
- Multi-floor project schema fields with active-floor normalization.
- Material library import/export.
- Co-channel and adjacent-channel interference penalties.
- AP count recommendation, channel recommendation, and first-pass Tx power tuning.
- Route simulation markers for handover and dead-zone samples.

## Next Hardening

- Replace the simple wizard command with a multi-step Fluent dialog.
- Add editable dedicated door/window tools instead of representing most openings as objects.
- Use partial tile recomputation, not only region-aware cache eviction.
- Add richer PDF report layout with embedded plan and heatmap images.
- Add visual channel-planning report and AP power-tuning controls.
- Add multi-floor rendering/navigation and vertical propagation modeling.
