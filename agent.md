# Agent Handoff: PCR Plate Setup

Last updated: 2026-10-05

## Project at a glance

- Unity project root: `/home/waheedianho/Documents/unity/PCR_Plate_Setup`
- Unity version observed: `6000.0.84f1`
- Active scene observed: `Assets/Scenes/StarterScene.unity`
- Main animated model: `pcr_plate_setup_animation` (Animator)
- Step-by-step guide content: `clipguide.md` in the project root
- This handoff reflects the editor state inspected on 2026-10-05. Re-check the open project and Console before making assumptions about later changes.

## Current implementation

### Animation guide

The guide is built with UI Toolkit:

- `Assets/UI/PCRPlateGuide.uxml` — guide layout
- `Assets/UI/PCRPlateGuide.uss` — visual styles and ray-focus states
- `Assets/Script/PCRGuideUIController.cs` — step content, narration, buttons, and Animator playback
- `Assets/Script/PCRGuideWorldPanelControls.cs` — XR ray hit targets and focus feedback for the rendered UI

The UI is presented beside the plate in world space. A World Space Canvas displays a RenderTexture populated by the UI Toolkit panel. The scene has a `PCR Guide World Panel`, a tracked-device UI raycaster, an XR UI input module, and XR ray interactors with UI interaction enabled. The style uses a light clinical palette with mint and dark-green accents.

The controller exposes UI action methods including `ShowStep(int)`, `ConfirmGuideAction()`, `AdvanceGuideStep()`, `ReturnToPreviousStep()`, `ReplayGuideNarration()`, `PlayGuideDemonstration()`, and `ToggleChecklistItem(int)`. The demonstration action plays the animation state associated with the selected guide step. The eight narration clips are assigned in the controller. Narration starts when the learner selects Listen; changing steps does not currently auto-play narration.

### Cover interaction

The `Cover` object is configured with an XR Grab Interactable and a `PCRCoverAttemptOpener` component. The component listens for a grab attempt, disables the normal grab interaction, and plays the authored cover-opening state on the plate Animator: `Base Layer.01_Meet_the_Plate`. That clip contains animation curves for the `Cover` hierarchy, so the authored animation controls the opening motion.



### Cover pose synchronization (2026-10-05)

The opening animation 01_Meet_the_Plate_Interactive moves the cover from its starting pose to its parked side pose. Controller states for steps 2–8 now use editable standalone clips in Assets/Animation/*_CoverSide.anim; each holds the cover position, rotation, and scale constant at the opening clip's final pose. These copies preserve the other authored animation curves. The original clips embedded in Assets/Prefabs/pcr_plate_setup_animation.fbx were left as source assets. Verify the cover visually in Play Mode/headset after future model or animation edits.

### Make-the-map interaction (guide step 2)

- `Assets/Script/PCRWellMapStep.cs` is attached to `pcr_plate_setup_animation` and references the guide controller and `Plate_Root`.
- The plate model has 96 well transforms named `Cone.001` through `Cone.096` under `Plate_Root`. A separate `Cone` transform is not one of the 96 usable wells.
- At runtime the component adds a SphereCollider and XR Simple Interactable to each well renderer object.
- Wells are addressed row-major: `Cone.001` = A1, `Cone.002` = A2, etc. Step 2 asks the learner to select A1 through D6 in order (24 wells). The expected targets are indices 1–6, 13–18, 25–30, and 37–42.
- The current target is highlighted mint; hover feedback is blue; correctly selected wells stay green. Selecting an unexpected well displays feedback naming the expected coordinate. Completing all 24 targets calls the guide controller to complete the action and unlock the next step.
- The controller has `CurrentStepNumber` and `SetStepActionFeedback(string)` support for this interaction.

The 96-well indexing and A1–D6 orientation were inferred from hierarchy names and transform ordering. They have not yet been verified in Play Mode or in-headset.

## Important content gap

`clipguide.md` says wells should be colored according to a confirmed plate map, but the actual biological allocation has not been supplied/confirmed. The current map interaction is a coordinate-finding exercise; it does not assign samples, PTC/NTC controls, or duplicates. Do not invent or encode those assignments. Confirm the approved plate map with the project owner before implementing sample/control allocation.

## Recommended next steps

1. Open `Assets/Scenes/StarterScene.unity`, inspect Console, and enter Play Mode with the target XR setup.
2. Verify XR selection reaches each well, the collider size is comfortable, and the A1–D6 direction matches what the learner sees from the intended viewpoint. Adjust well-index mapping or interaction colliders if needed.
3. Confirm how the guide should behave when the learner selects a wrong well (current behavior gives feedback but does not penalize or reset).
4. Get the approved plate allocation, then implement the map coloring/legend and validation for sample wells, positive/negative controls, and duplicates.
5. Connect remaining learner actions to their matching animation/audio: pipetting master mix, adding controls and samples, duplicate wells, checklist verification, sealing the plate, and handoff. Use the recorded narration and authored clips; verify clip-to-step naming in the Animator before wiring.
6. Decide whether narration should remain learner-triggered with Listen or start automatically on each step.
7. Re-check the guide UI and XR ray hover/focus behavior in the headset at the panel's final placement.

## Working notes for the next agent

- Start by loading the Unity project guidelines and inspecting the active scene, hierarchy, and current scripts; this document can become stale.
- The root-level guide source is `clipguide.md`. Read it before changing instructional content.
- Preserve existing Unity scene references and Animator state names. Verify serialized references after edits.
- At the last inspection on 2026-10-05, the Console error query returned no errors after attaching the map interaction. This is not a guarantee about later editor state.
