# Body customization

Cosmetic adaptation of MOLOT-BlueMoon-Station's `/obj/item/organ/genital` appearance.
The character editor's **Основное → Части тела** section edits the existing draft.
Presence, shape, size stage, skin/custom color and visibility are
stored in `HumanoidCharacterAppearance.BodyFeatures`, persisted by both database
providers and loaded into networked `HumanoidAppearanceComponent.BodyFeatures`.
Existing profiles default to no additional parts. Choices are independent of sex.
Breasts include the separate color toggle and nipple overlay from
https://github.com/BlueMoon-Labs/BlueMoon-Station/pull/4108, pinned to commit
`e0681c819a195c390ded3109b2109ea09b2486c1`. The source's supported states are the
ordinary pair, sizes A–O and HUGE. Both colors are saved in the same cosmetic JSON.
Hands/gloves stay above exposed anatomy; under-clothing mode restores native order.
SS13-specific underwear DMI changes are not native SS14 clothing resources.

Seven external parts have directional overlays. `butt` is the source's combined
buttocks/hips overlay, not a separately modelled hip organ. `womb` has no on-mob
sprite in the source: its presence is an additional toggle inside the vulva
settings and does not affect the sprite. The anus uses the default shape without
a shape selector. No physiology, interactions, fluids, surgery or growth mechanics
are provided. Discrete sizes come from available sprite states; individual scaling
is disabled and legacy scale values normalize to 1.

The BODY feature catalog validates shape/size against compiled resource IDs.
Client-provided paths are never loaded. Legacy scale, duplicate/unknown parts,
colors and visibility are normalized when importing/loading a profile.
Overlay layers follow the body rotation and entity scale. The front layers sit
below the jumpsuit or above outer clothing, according to the selected visibility.
Back layers remain behind the chest; a removed/hidden chest hides its anatomy.
Ordinary and luminous tattoo layers draw above anatomy, including anatomy set to
draw above clothing. Tattoo cache order is rechecked after anatomy changes.
Source overlays use the standard 32px biped torso; unusual species may require
additional art. Clothing retains its original shape and is not deformed.

## Imported assets

Run from repository root with Python and Pillow:

```
python Tools/body-customization/import_bluemoon.py C:/old_disk/repos/MOLOT-BlueMoon-Station
```

The importer preserves DMI frame order (South, North, East, West), timing and
FRONT/BEHIND separation. It imports neutral tintable states and excludes duplicate
skin-tone variants. Unnamed DMI states still occupy tiles and are counted when
locating subsequent frames. The generated shared catalog contains only exported states.
Source paths and CC-BY-SA-3.0 attribution are recorded in each RSI's `meta.json`;
the source README declares that license for icons unless otherwise specified.
Artwork attribution: tgstation, Citadel and SPLURT/BlueMoon contributors; these
are conversions of the named source files, not newly authored artwork.

Database migration: `20261012120000_DeepLagoonBodyCustomization`, additive JSON
column for SQLite and PostgreSQL. Migration rollback removes this cosmetic data.
