# Changelog

## Player-facing

- 2026-09-27: Changed every internal name to Mamono, so saves from earlier versions no longer load.
- 2026-09-27: Changed the mod name to Project Mamono Slime Faction.
- 2026-09-27: Changed the word momo to mamono in the mod's labels and descriptions.
- 2026-09-20: Changed the notes about a refused wander-in to appear only in development mode.
- 2026-09-20: Added a "momo corpses" line in the butcher menu, holding all six slime momo corpses.
- 2026-09-20: Fixed a woman corrupted into a slime keeping a human body. She now becomes the slime's own race, so her colour and body match the slime that made her.
- 2026-09-19: Added Big and Small - Framework as a required mod.
- 2026-09-19: Added new slime jelly textures.
- 2026-08-30: Rebalanced wild slime spawns: they start at rank E or F in Isekai Leveling. They still rank up through play.
- 2026-08-30: Fixed slime carrier backstories staying "unknown" after you accept her.
- 2026-08-30: Fixed "all wounds are bruises" giving cuts instead of bruises.
- 2026-08-30: Fixed the cave gate opening on maps that have no caves.
- 2026-08-29: Rebalanced exotic traders: a parasite slime is no longer guaranteed.
- 2026-08-29: Added the parasite slime item and the slime carrier. A rare trader roll, never guaranteed. An infested woman keeps her race, backstories, hair and genes, gains the momo and slime-gel genes, goes sterile, and yields human skin and meat.
- 2026-08-29: Added the slime carrier visit event. A travelling woman begs to join, then secretly takes her over after two days.
- 2026-08-29: Added carrier disguise details: season-appropriate beggar clothes, and backstories masked as "unknown" until you accept her.
- 2026-08-29: Fixed the slime carrier never finishing her takeover.

## Internal

- 2026-09-27: Changed the mod folder and project names to Mamono.
- 2026-09-27: Changed the defNames, class names, scribe labels and file names to Mamono.
- 2026-09-27: Changed the README to say mamono.
- 2026-09-24: Changed the em dashes in this mod's text to plain hyphens.

- 2026-09-20: Changed the wander-in messages to go through core's `PMMLog`, so they only appear in development mode.
- 2026-09-20: Added the six slime races to the shared momo corpses line in the core mod.
- 2026-09-20: Changed the slime pawnkind and xenotype names to sort together in the dev spawner.
- 2026-09-20: Fixed stale comments that described the slime xenotype roll and what a dev-spawned nureonago looks like.
- 2026-09-20: Changed the six slime race clones to Human-based races with a Big & Small race tracker.
- 2026-09-20: Removed the post-commit changelog hook, which wrote entries in the old format.
- 2026-09-20: Changed the README to match the code.
- 2026-09-19: Changed the build to use MSBuild.
- 2026-09-02: Changed the About text and README to cover all seven xenotypes and the new mechanics.
- 2026-09-02: Removed the parasite plan doc.
- 2026-08-29: Added git hooks for the changelog and push checks.
- 2026-08-29: Changed the event strings to live in SlimeEvents.xml instead of SlimePsycasts.xml.
- 2026-08-29: Changed the About preview thumbnail to a png.
- 2026-08-29: Removed the Workshop upload step from the release script.
- 2026-08-29: Added a SteamCMD Workshop upload script.
- 2026-08-29: Changed release.sh to skip a fresh build and tag the release.
- 2026-08-29: Added release.sh for GitHub releases.
- 2026-08-29: Added build.sh.
- 2026-08-29: Added the initial Slime Faction mod.
