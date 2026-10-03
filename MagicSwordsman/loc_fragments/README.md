# Localization fragments

Content agents write their localization here instead of editing `MagicSwordsman/localization/**`:

    loc_fragments/<group>/<kor|eng>/<cards|powers|relics|events|potions|card_keywords>.json

One flat JSON object per file, same keys in `kor` and `eng`. The build's localization analyzer already sees these
keys; the game only sees them after the integrator runs `python3 tools/merge_loc_fragments.py --write`.
Full rules: `FRAMEWORK.md` section 7.
