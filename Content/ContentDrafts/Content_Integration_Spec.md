# Content Integration Specification

This document describes the hand-off between the content author and the Unity integration owner. It does not add or modify Unity scenes.

## Scene mapping

| Scene | Narration | Subtitle cues | On-screen text | Interaction relation |
| --- | --- | --- | --- | --- |
| Museum | `Museum_Narration.md` | `Museum_Subtitles.csv` | `Museum_OnscreenText.md` | Ray points to burner, controller touches burner, trigger loads the memory scene |
| Mainland Home | `MainlandHome_Narration.md` | `MainlandHome_Subtitles.csv` | `MainlandHome_OnscreenText.md` | Arrival scene; player looks around and observes the home |

## Timing

- Museum subtitle cues cover approximately 0-31 seconds.
- Mainland Home subtitle cues cover approximately 0-31 seconds.
- The final Museum cue includes the touch-then-trigger instruction.
- On-screen prompts may remain visible longer than the spoken cue so that seated players can read them comfortably.

## Separation of responsibilities

- Narration files are spoken content references.
- Subtitle CSV files are timed caption references.
- On-screen text files are persistent panel and interaction text; they should not be copied line-for-line from every subtitle cue.
- The interaction owner controls the actual event hook and scene transition.
- The content author supplies text, timing intent, and asset provenance; the content author does not change the interaction script in this hand-off.

## Unity hand-off rule

These drafts remain outside `Assets/`. The integration owner should copy only team-approved final text or audio into the Unity project after review, preserving the cue IDs and the provenance record.
