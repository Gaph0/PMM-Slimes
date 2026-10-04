# Project Mamono Slime Faction

Wild slimes for Project Mamono, for RimWorld 1.6. It adds seven slime species
that arrive from the wild, or walk up to your colony in disguise.

- **Seven slime species.** Blue, red, dark, bubble, taisui, sea and nureonago,
  each with her own gel skin and her own place in the world.
- **They come to you.** Four kinds wander in from the wild and are tamed like
  wild men; a nureonago walks in with the rain; a traveller who is carrying a
  parasite walks in any day of the week.
- **Slime bodies.** Her hair always matches her skin, tattoos never take, she
  leaves slime filth as she walks, every wound she takes is a bruise, and
  butchering her gives slime jelly instead of skin or meat.
- **Slime jelly.** Eight kinds. Each keeps forever, each carries its own boon,
  and a woman who eats enough of one melts into that slime.

**Requires:** Harmony, Biotech, Odyssey, Project Mamono, Big and Small - Framework

**Optional:** Vanilla Psycasts Expanded (the dark slime's Slime Core path), ISEKAI RPG Leveling (mob ranks), Anomaly (the taisui's strange chats)

## Content

Seven slimes live in the wild, and two of them come to your colony in disguise.
All of them are women, all of them are factionless, and there are no slime
factions and no slime settlements: the only way in is her own event. Tame one, or
capture her and convince her to join, and she works and bonds like any other
mamono. Their bodies are as soft as a human's to the weather - any temperature
that would kill a naked human kills a slime.

### Xenotypes

| Mamono | Lore | Purpose in game | In plain words |
|---|---|---|---|
| **Slime** | The common slime of warm grasslands: cheerful, soft, and slow of thought. | The base slime, and the source of slime jelly. | Slower and weaker in a fight than a human, and hard to hurt. |
| **Red Slime** | The same creature, fiercer and rarer than the blue. | A slime without the slowness and the weak blows. | As quick and as strong as a human, and hard to hurt. |
| **Dark Slime** | Rarest of the grassland slimes, gifted in the psychic arts. | With Vanilla Psycasts Expanded, she walks the Slime Core path: tease, drain, and corrupt. | Soft and slow like her sisters, and the only slime who can cast. |
| **Bubble Slime** | A slime of polluted caves and sewers, who eats toxic waste. | Your waste disposal: she devours a wastepack stack and burps the gas out. | Poison and toxic buildup cannot hurt her; otherwise she is a common slime. |
| **Taisui** | A cave slime of legendary intellect, and the source of the elixir of the same name. | Your researcher, and the best jelly of the eight. | Soft like a common slime, and the cleverest slime there is. |
| **Sea Slime** | A coastal drifter who paralyses what she catches. | A fisher of your colony, and one of your fastest pawns on water. | As strong as a human, soft, and quick in the water. |
| **Nureonago** | The devoted housewife slime. She walks in out of the rain, disguised as an ordinary woman. | A colonist who keeps the colony clean, and adds no filth of her own. | Soft and slow, and always kind. |

### Genes

- `slime gel` - the slime body: she leaves about five times a human's filth as
  she walks, oozes jelly, and takes every wound as a bruise, having no bones and
  no skin to break or cut. No stat change to her own abilities.
- `blue gel skin`, `red gel skin`, `purple gel skin`, `slate gel skin`,
  `pale-yellow gel skin` and `sea foam skin` - the translucent colour of her
  body, one to a species. No stat change.

Every slime also carries the core Mamono gene and a set of vanilla genes, which
is where the seven species differ: the taisui's mind, the bubble slime's toxin
resistance, the sea slime's swimming, and the nureonago's neatness.

### Slime jelly

Eight jellies, each one the flesh of the slime it came from. All of them keep
forever and none of them can give food poisoning, except the bubble slime's.
Butchering a slime gives about twenty of her own jelly, and a living slime sheds
two to four of them a day.

| Jelly | Effect on eating | Turns a woman into |
|---|---|---|
| slime jelly, blue slime jelly | - | a slime |
| red slime jelly | Moving +10% for 3 hours | a red slime |
| dark slime jelly | +5 neural heat limit, faster heat regen, more meditation focus for 3 hours | a dark slime |
| taisui jelly | Research speed and learning +0.5 for 6 hours | a taisui |
| sea slime jelly | Work speed +10%, consciousness +5% for 3 hours | a sea slime |
| mochi slime jelly | Mood +6 for 8 hours | a nureonago |
| bubble slime jelly | Food poisoning for anyone not bonded to a bubble slime, then all toxic buildup flushed and toxic resistance +30% for 3 hours | a bubble slime |

Eating jelly builds mamono corruption in a woman, and about a dozen in one
sitting is enough to finish the change. Corruption fades while she is on her
feet, so snacking does nothing and a binge does everything. Men and monsters are
not affected at all.

### Events

- **A slime wanders in** - four events, one per wild species, each blocked while
  toxic fallout or noxious haze is on the map:
  - the common slime, on Odyssey grasslands with a coldest month of 18 °C or more
    and 1500 mm of rain or more;
  - the bubble slime, on polluted land with caves;
  - the taisui, on any land with caves - she is the rarest of the four;
  - the sea slime, on a coast facing open ocean.
- **A strange woman in the rain** (nureonago) - fires only while it rains, and
  only while the colony has a man she could bond with. She waits at the map edge
  and leaves when the rain stops. Talk to her and accept, and the disguise melts
  away and she joins. Refuse or attack her, and she shows what she is.
- **A travelling woman** (slime carrier) - needs a man in the colony, and no
  other carrier on the map. She looks and dresses like any woman down on her
  luck, waits about eighteen hours, and leaves if she is turned away. Accept her
  and she joins - with a parasite already inside her, which turns her about two
  days later.
- **A fishing catch** (sea slime) - on a coastal Odyssey map, one catch in
  sixteen is a wild sea slime instead of a fish. She surfaces in the water and
  can be tamed like any other.
- **The parasite slime** - a rare item, sold for 800 silver by exotic goods
  traders and never guaranteed. A woman who eats it is infested, and turns into
  a slime carrier about two days later, keeping her name, her looks and every
  gene she had.

### Abilities

- `Consume wastepack` (bubble slime) - she devours a whole wastepack stack
  within 20 cells and burps out a puff of toxic gas, two gas per pack. Cooldown
  12 hours.
- `Paralytic tentacles` (sea slime) - she injects paralytic poison into any pawn
  within 6 cells: Moving x25% for one hour, and using it again on the same pawn
  tops the dose back up. Cooldown 12 hours.
- **The Slime Core path** (dark slime, needs Vanilla Psycasts Expanded) - five
  psycasts in three tiers, all of them feeding the core mod's mana and
  corruption:
  - `Sticky Slime Bomb` (tier 1) - a burst over 5 cells: tease, a little
    corruption, and everyone caught is left slime-coated and slower.
  - `Slime Grope` (tier 1) - one victim: tease, and sips their essence into her
    mana.
  - `Draining Slime Splash` (tier 2) - a patch of living slime for 15 seconds
    that teases, drains and corrupts anyone standing in it.
  - `Amoeba Hold` (tier 2) - she engulfs a victim: both are rooted for five
    seconds while she teases and drains.
  - `Pleasure Ascension` (tier 3) - only against a slime-coated victim: 20
    seconds of binding and heavy teasing, which leaves a woman one infusion
    short of becoming a slime.

  Costs run from 0.10 to 0.30 psyfocus and 10 to 30 entropy, with cooldowns of
  10 to 60 seconds. Each tier needs the tier before it.

## Found a bug?

Report it on the issue tracker: https://github.com/Gaph0/PMM-Slimes/issues

Please do not leave bug reports in the comments section. Post them on the
tracker so they can be tracked and fixed.

## License and attributions

Licensed under the Unlicense. See the
[licence](https://github.com/Gaph0/PMM-Slimes/blob/master/LICENSE.txt).

Thanks to:

- Tynan Sylvester and the Ludeon Studios team - RimWorld, and the Biotech, Odyssey and Anomaly expansions.
- Big and Small - Framework - the slime races and the race pattern.
- Harmony - the patches under everything.
- Vanilla Psycasts Expanded - the psycast path the dark slime walks.
- ISEKAI RPG Leveling - the mob ranks the wild slimes spawn at.
- Kenkou Cross - the Monster Girl Encyclopedia, where these creatures come from.
