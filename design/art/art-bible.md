# Art Bible: 回响 (Echoes)

*Created: 2026-05-11*
*Status: Complete — All 9 sections drafted*

---

## 1. Visual Identity Statement

### 画卷中的呼吸 (Breathing Within the Scroll)

### One-Line Visual Rule

> Every frame must be a painting that could hang on a wall. 留白 decides what to paint. 意境 decides how to paint it. The beauty of the unfinished decides when to stop.

**When any visual decision is ambiguous, ask:** *"Does this bring the frame closer to a hand-painted scroll, or closer to a standard game interface?"* Choose the scroll.

### Principle 1: 静中有动 (Motion Within Stillness)
*Serves Pillar 4: 画卷中有呼吸 (Breathing Scrolls)*

The frame reads as a static hand-painted illustration at first glance — and should hold on that glance for a full beat. But always, at least one element is subtly, cyclically alive: a breeze through foliage, light shifting across a surface, ink bleeding at the edges, dust suspended in a sunbeam. Motion is the breath. Stillness is the body that holds it. Target ratio: one part motion to nine parts stillness.

**Design test:** If the player can stare at a memory fragment for 10 seconds and nothing moves, the fragment is visually incomplete. Add exactly one subtle loop, then stop.

### Principle 2: 颜色即是情绪 (Color Is Emotion)
*Serves Pillar 1: 选择即重写 (Choices Rewrite) AND Pillar 2: 不完美才是力量 (Imperfection is Power)*

Color is not decoration — it is the emotional signature of memory itself. Each life stage has a distinct color temperature: childhood glows with warm amber tones; youth runs saturated but cool; adulthood settles into desaturated earth; twilight fades to gray with isolated warm pinpoints. When a player rewrites a memory through choice, the palette shifts visibly — warmth enters where something was preserved, coolness spreads where something was lost. Imperfect, broken, or regretful endings use lower saturation not as punishment, but as visual honesty — regret is quieter than fulfillment, and the palette must tell that truth before any word does.

**Design test:** When the player transitions into a new memory fragment, the palette must communicate the emotional register BEFORE any text appears or any character speaks. If a "regret" memory and a "warmth" memory share the same saturation level, the palette is lying.

### Principle 3: 留白即是空间 (Emptiness Is Space)
*Serves Pillar 3: 关联的网络，不是线性的书 (Association Web, Not Linear Book)*

Not everything in a memory deserves a brushstroke. Associative anchors — the objects, faces, gestures, and text fragments that link to other memories — are rendered with full detail and precision. Everything else is suggested through negative space, ink-wash edges, and the minimum strokes needed to imply form. The emptiness is not absence; it is the navigational language of the association web. It clears the path between memories so the player's eye finds the next anchor because there is nothing in the way.

**Design test:** A new player, seeing any memory fragment for the first time, must identify every interactive memory anchor within 3 seconds. If they cannot, the composition has failed — there is either too much detail competing for attention, or not enough negative space guiding the eye to the next association.

### Pillar Coverage Summary

| Pillar | Primary Visual Principle | How It Manifests |
|---|---|---|
| 1: 选择即重写 | 颜色即是情绪 | Choice outcomes shift the memory's palette — warmth preserved, coolness lost |
| 2: 不完美才是力量 | 颜色即是情绪 | Broken/regretful endings rendered in honest desaturation — different beauty, not lesser |
| 3: 关联的网络 | 留白即是空间 | Negative space clears the path between associative anchors; detail only on what connects |
| 4: 画卷中有呼吸 | 静中有动 | One subtle micro-animation per frame; ratio of stillness to motion is 9:1 |

---

## 2. Mood & Atmosphere by Game State

### State 1: Exploring Memories (Primary Gameplay)

| Attribute | Definition |
|---|---|
| **Primary emotional target** | Quiet wonder with an undertone of melancholy — the feeling of looking at old photographs of people you can no longer reach. Beautiful, but the beauty carries the ache of distance. |
| **Lighting character** | Soft diffuse ambient light, low-to-medium contrast, gentle edge vignette. Light appears to come from "within" the memory rather than from a single directional source. Contrast is deliberately low — memories are hazy at the edges, not crisp. |
| **Atmospheric descriptors** | Contemplative, hushed, immersive, gently beckoning |
| **Energy level** | Low-Medium — a slow drift, never urgent. |
| **Signature visual element** | Depth parallax within the scroll plane — foreground/midground/background layers shift at different rates as the player drifts, creating the sensation of floating through a living scroll. |

**Design test:** If the player can stare at a memory fragment for 10 seconds and nothing moves, the fragment is visually incomplete.

### State 2: Choice Moment

| Attribute | Definition |
|---|---|
| **Primary emotional target** | Weighted significance — not anxiety, but gravity. The feeling of holding a fragile object in both hands, knowing that what you do next will change it. |
| **Lighting character** | High contrast — a warm, luminous spotlight on the anchor object; everything else desaturates and dims. Deep vignette pulls the periphery into near-black or ink-wash shadow. |
| **Atmospheric descriptors** | Suspended, intimate, weighted, reverent |
| **Energy level** | Near-zero — a held breath. Micro-animations continue, but scroll drift stops. |
| **Signature visual element** | Anchor isolation — everything except the chosen anchor fades toward ink-wash. The anchor sharpens to its highest detail level. Choice options emerge as branching ink strokes growing outward. |

**Design test:** If the player cannot immediately tell which objects are choice options vs. the anchor itself vs. the faded background, the composition has failed.

### State 3: Memory Transition

| Attribute | Definition |
|---|---|
| **Primary emotional target** | Liminal uncertainty — the feeling of being between places, half-remembering. Like trying to recall a dream as it slips away on waking. |
| **Lighting character** | Very low contrast, near-monochrome with one accent thread. Cool temperature overall — the space between memories is cool; memories are warm; the void between them is the temperature of absence. |
| **Atmospheric descriptors** | Liminal, drifting, vaporous, expectant |
| **Energy level** | Low — a gentle, continuous flow. The player is carried by the current of association, not steering. |
| **Signature visual element** | Ink-thread constellation — thin, organic ink lines connect bright points representing memory fragments. The departing memory's dominant color bleeds briefly into the transition space, then fades. The arriving memory's color begins as a faint stain and gradually saturates. Duration: 2-5 seconds. |

**Design test:** If the memory transition could be replaced by a fade-to-black without losing anything, it has failed.

### State 4: Chapter Ending / Ending Reveal

| Attribute | Definition |
|---|---|
| **Primary emotional target** | Catharsis — the exhale after holding breath. Not triumph, not tragedy, but completion. The player should feel the accumulated weight of every choice settle into a final tableau. |
| **Lighting character** | Full-frame composition at the highest contrast level in the game. The ending fills the frame completely — every corner holds paint. Color temperature follows the emotional register: warm amber-gold for preserved/fulfilled paths, cool desaturated blue-gray for loss/regret paths, layered warm-over-cool for bittersweet. |
| **Atmospheric descriptors** | Culminating, resonant, still, complete |
| **Energy level** | Near-zero, gradually descending to absolute stillness. |
| **Signature visual element** | The closing scroll border — an ink-wash edge slowly draws inward from all four sides, like a scroll being carefully rolled shut. It pauses at the most emotionally significant element, letting it linger in full illumination for an extra beat. For warm endings, the closing border is a soft sepia wash; for broken endings, it is a deeper, cooler ink. Both are equally "finished." |
| **Palette divergence** | **Preserved path**: warm amber/gold, higher saturation, light closing border. **Loss path**: cool blue-gray, lower saturation, deep closing border — rendered with more detail and care, not less. **Bittersweet path**: warm light source but cool shadows, alternating closing border. |

**Design test:** If a regret ending and a warm ending would look the same in thumbnail, the palette has failed. The loss-path ending must feel equally beautiful and intentional.

### State 5: Main Menu

| Attribute | Definition |
|---|---|
| **Primary emotional target** | Invitation to stillness — "come sit with me for a while." Not excitement, not mystery-thriller tension. A gentle, quiet pull. |
| **Lighting character** | Warm low light, very low contrast, gentle gradient. Time of day: late afternoon / golden hour — the universal "memory" light. Amber and sepia undertones with deep ink-black negative space. Light is soft and diffuse, like sunlight through rice paper. |
| **Atmospheric descriptors** | Beckoning, serene, timeless, intimate |
| **Energy level** | Very low — meditative stillness. The menu exists in a state of perpetual almost-completion. |
| **Signature visual element** | The perpetually-unfurling title scroll — a single hand-painted scroll in the background, slowly and continuously receiving new brushstrokes in a never-completing cycle (60-90 second loop). The title characters 回响 appear to be forming from accumulated ink, always partially legible but never fully "dry." Menu text rendered in restrained calligraphic style, as if brushed directly onto the scroll. Selection highlight is a soft ink-brush wash behind the text, not a digital rectangle. |
| **Secondary element** | Faint, nearly transparent afterimages of all four life stages drift at the scroll edges — a child's toy, a young person's shadow, an adult's hand, a cane — barely visible, appearing and dissolving. |

**Design test:** A new player, seeing the main menu for the first time, must understand within 5 seconds that this is (a) not an action game, (b) visually distinctive, and (c) emotionally serious.

### Summary Table

| State | Emotional Target | Temperature | Contrast | Energy | Signature Element |
|---|---|---|---|---|---|
| **Exploring memories** | Quiet wonder + melancholy | Chapter-driven | Low-Medium | Low-Medium | Depth parallax within scroll plane |
| **Choice moment** | Weighted significance | Warm spotlight, cool periphery | High | Near-zero | Anchor isolation; branching ink strokes |
| **Memory transition** | Liminal uncertainty | Cool base + one warm thread | Very low | Low | Ink-thread constellation; color bleed |
| **Chapter ending** | Catharsis (completion) | Warm / Cool / Layered | Highest | Near-zero → still | Closing scroll border |
| **Main menu** | Invitation to stillness | Universal warm (amber/sepia) | Very low | Very low | Perpetually-unfurling title scroll |

### Emotional Arc Across States

```
Main Menu          → Stillness, invitation
    ↓
Exploring memories → Quiet wonder, melancholy drift
    ↓
Choice moment      → Suspension, gravity, held breath
    ↓
Memory transition  → Liminal drift, expectancy
    ↓
Exploring memories → Return to wonder (changed by choice)
    ↓ ... (cycle repeats)
    ↓
Chapter ending     → Catharsis, completion, exhale
    ↓
Main Menu          → Return to stillness
```

---

## 3. Shape Language

### Shape Philosophy

> **One-line rule**: Brushstrokes are born incomplete. Every shape arrives at the eye mid-breath — never fully formed, never fully dissolved. The eye completes what the brush only suggests.

*Serves Pillar 4 (画卷中有呼吸), rooted in Visual Principle 3 (留白即是空间)*

The shape language draws from the 写意 tradition: suggestion over description, spirit over likeness. Shapes are not containers for color — they are traces of intention, left by a brush that moved through the space and moved on. The player collaborates with the brushstroke. The world is not solid; it is memory — made of partial traces, not complete reconstructions.

### Character Silhouette: The Wandering Spirit

*Serves Pillar 3 (关联的网络), rooted in Visual Principle 3 (留白即是空间)*

The player is 游魂 — a spirit adrift among memories. Shape language communicates: **weightless, present-through-absence, always-in-motion-even-when-still.**

| Quality | Rule | Emotional Effect |
|---|---|---|
| **Asymmetrical flow** | The spirit leans slightly in the direction of drift — always mid-journey | Implies ongoing movement; never at rest, never arrived |
| **No hard angles** | Every silhouette transition is curved, softened, or feathered | Communicates weightlessness; the spirit cannot be "cornered" |
| **Scale humility** | The spirit occupies ~5-8% of frame height | The spirit is a guest, not a protagonist imposing itself |
| **Emotional bleed** | Interior wash shifts temperature based on proximity to emotional anchors | Subconscious emotional tracking; the player's "body" reflects memory's register |
| **Subtle features** | Two faint brush-dot eyes — minimal but directional. They suggest gaze without defining expression. | The spirit has presence and direction, not a blank void. Players project emotion onto hinted features. |

**Silhouette philosophy — "the unclosed contour with hinted presence"**: The spirit's form is a thin, variable-weight brush line defining a human outline that *never closes* — hands dissolve into suggestion, feet fade before reaching the ground. The interior holds a faint wash of the current memory's dominant emotional color. Two barely-visible ink dots suggest eyes — just enough for the player to sense direction and presence, but not enough to define a specific face or expression.

### Environment Geometry & Edge Treatment

*Serves Pillar 3 (关联的网络) AND Pillar 4 (画卷中有呼吸), rooted in Visual Principle 3 (留白即是空间)*

**Dominance: Organic over geometric. 70/30 ratio.**

| Shape Type | Share | Brush Quality | What It Communicates |
|---|---|---|---|
| **Organic (dominant)** | ~70% | Loose, variable-weight, "wet" marks. Lines follow hand-with-brush natural rhythm. | Fluidity, impermanence, the natural flow of memory |
| **Geometric (accent)** | ~30% | Slightly more controlled strokes — "dry brush" or deliberate, slower lines. | Structure, significance, "this was built to last" |
| **Dissolving (transition)** | At edges only | Feather-to-nothing, ink-bleed at borders, scattered dots. | Impermanence, liminality, "this memory is ending" |

The 70/30 ratio accommodates indoor scenes (rooms, furniture, letters) and character-driven fragments where man-made objects naturally carry more geometric weight, while ensuring organic brushwork still dominates the overall visual field.

**Edge treatment — the unfinished frame**: Memory fragments do not end at hard rectangular boundaries. Edges dissolve in three modes:
1. **Ink-wash fade** (default, exploration): Color and line density decrease toward the edge, ending in blank scroll space.
2. **Brushstroke terminus** (chapter boundaries): The scene ends where a single decisive horizontal brushstroke cuts across.
3. **Feathered constellation** (memory transitions): Detail dissolves into individual ink-dots that spread apart like stars, then vanish.

### UI Shape Grammar: Calligraphic Diegesis

*Serves Pillar 1 (选择即重写) AND Pillar 4 (画卷中有呼吸), rooted in Visual Principle 3 (留白即是空间)*

**Core rule: UI is painted into the scroll, not overlaid on top of it.**

| UI Element | Shape Language | Rationale |
|---|---|---|
| **Selection highlight** | Soft, organic ink wash behind the target — like a loaded brush pressed and lifted. Never a rectangle. | The player chooses *within* the memory, not from an external menu |
| **Choice options** | Branching ink strokes that grow outward from the anchor — calligraphic, thin-to-thick, variable speed. | Choices grow FROM the memory |
| **Menu text** | Restrained calligraphic style. Characters appear brushed, not typeset. | The text belongs to the scroll |
| **Progress indicators** | Small ink dots near the scroll edge — filled for explored, empty for unexplored. No HUD chrome. | Diegetic minimalism |
| **Interaction prompts** | A single subtle brush dot with faint pulse animation near interactive objects. | Invitation, not instruction |
| **Small-scale UI elements** | Maintain calligraphic authenticity at all sizes — no simplified, higher-contrast fallback. At small sizes, use fewer strokes rather than different strokes. | Consistent visual language; legibility achieved through composition, not compromise |

**Anti-patterns**:
- Rectangular buttons with drop shadows
- Purely geometric icons with uniform stroke weight
- Opaque overlays or modal dialogs
- Progress bars, health bars, or any numerical HUD element

### Visual Hierarchy Through Shape

| Hierarchy Level | Stroke Count | Detail | Purpose | Example |
|---|---|---|---|---|
| **Hero (eye anchor)** | 30-50+ strokes | Fully realized, precise brushwork | Memory anchors, the spirit, choice options | A letter on a desk — every fold, character, ink bleed |
| **Supporting (context)** | 8-20 strokes | Suggested but not completed | Environmental context, secondary figures | A window, a distant figure, furniture |
| **Background (atmosphere)** | 3-8 strokes | Bare minimum; heavy 留白 | Atmospheric depth, parallax layers | Mountains in mist, distant trees, cloud-forms |
| **Negative space (rest)** | 0 strokes | Intentional emptiness | Space between anchors; navigational clarity | The empty scroll surface |

### Ink-Brush Connection

| Brush Principle | Shape Expression | Emotional Effect | When Used |
|---|---|---|---|
| **Variable line weight** | No two strokes have identical thickness | Human presence, not machine precision | Every frame |
| **Dry brush (枯笔)** | Streaky, porous strokes where brush splits | Age, fragility, things half-remembered | Geometric anchors; aging memories; regret-path endings |
| **Wet wash (湿染)** | Soft, bleeding edges; ink blooms beyond boundary | Fluidity, emotional saturation | Spirit's interior; memory transitions; warm-path endings |
| **Flying white (飞白)** | Streaks of negative space within a fast stroke | Urgency, incompleteness | Choice options emerging; ink-thread transitions |
| **Broken ink (破墨)** | Darker ink on wet wash — uncontrolled bleeding | Emotional intensity, memory overwriting | Choice resolution; anchors during rewrite |
| **Accumulated ink (积墨)** | Multiple layers built up over time | Weight of accumulated memory | Chapter endings; closing scroll border; menu title |

### Pillar Alignment & Design Tests

| Pillar | Shape Mechanism | Design Test |
|---|---|---|
| **1: 选择即重写** | Choice outcomes visibly alter shape completeness, edge treatment, or stroke density | If the frame is visually identical before/after a choice, the shape language has failed |
| **2: 不完美才是力量** | Broken/regret endings receive *more* expressive brushwork, not less | A loss ending must contain at least one shape more expressively rendered than its preserved-path counterpart |
| **3: 关联的网络** | Memory anchors share stroke characteristics so the player groups them pre-consciously | 4 of 5 playtesters must fixate on the anchor (not non-anchor) within 3 seconds |
| **4: 画卷中有呼吸** | 90% of shapes are static; 10% have subtle transformation loops | In any 10-second observation, at least one shape must exhibit micro-transformation |

---

## 4. Color System

**One-line rule**: Color is not atmosphere — it is emotion made visible. Every hex code below is a feeling before it is a pigment.

### The Four Seasons Framework

回响 is structured around four life stages, each a season, each a color world.

**Ordering principle**: The seasonal palettes define the chromatic range of the game. The base palette (Section 4.2) is the scroll they all sit on.

---

### 4.1 Season I: Spring — Childhood (童年)

**Time of day metaphor**: Early morning, just after sunrise. Light is diffuse, gentle, slightly hazy — filtered through thin rice paper. Shadows are soft and short.

| Color | Name | Approximate Hex | Emotional Meaning | Usage |
|---|---|---|---|---|
| 樱色 | Sakura Pink | `#F4C2C2` | The blush of early memory — warmth that does not yet know it is warmth | Childhood memory anchors; spirit's interior wash in spring |
| 新绿 | Fresh Green | `#C5D8B5` | New growth, first impressions, the world before categories | Foliage; edges of nurturing memories |
| 桃色 | Peach Blossom | `#F0CFB0` | Gentle affection, safety without comprehension | Skin tones of childhood figures; warm interior light |
| 朝露 | Morning Dew | `#D8E8D0` | Clarity, innocence, transparency of early feeling | Atmospheric wash; background haze |
| 淡墨青 | Pale Ink Blue | `#B0C4D0` | The first cool touch in a warm world — distance, the unknown beyond home | Distant mountains; sky wash; things beyond reach |

**Dominant hue range**: Warm pinks to soft yellow-greens (10° to 90°, centered 30°-50°)
**Saturation range**: 15-40% — deliberately muted; childhood memories are soft, not garish
**Contrast range**: Low. Light-on-light compositions.
**Light quality**: Diffuse morning light. No single directional source — ambient glow from within.

**Compositional rule**: At least 60% of the frame in warm domain. Cooling accents ≤25% of frame. Remaining 15% = negative space.

---

### 4.2 Season II: Summer — Youth (青年)

**Time of day metaphor**: Midday to early afternoon. Direct sun. High contrast shadows. The light is confident. Nothing hides.

| Color | Name | Approximate Hex | Emotional Meaning | Usage |
|---|---|---|---|---|
| 朱砂 | Vermilion | `#D4352C` | Passion, urgency, love that has not yet learned loss | Romantic memory anchors; conflict hotspots; choice options |
| 碧蓝 | Azure | `#3B7CB8` | Vast ambition, the infinite sky of youth's horizon | Sky and water; the color of possibility |
| 翠绿 | Verdant Green | `#4A8C3F` | Life at full strength, growth without restraint | Dense summer foliage; vitality markers |
| 金盏 | Marigold | `#E8A840` | Peak energy, the moment before the arc turns downward | Sunlight; important memory objects; spirit's interior near pivotal choices |
| 深靛 | Deep Indigo | `#3A3068` | The first taste of depth — passion's shadow side | Night scenes; emotional low points within high-saturation world |

**Dominant hue range**: Rich blues (190°-220°) with warm spikes (10°-40°)
**Saturation range**: 50-80% — the highest saturation in the game. Youth feels everything at full volume.
**Contrast range**: High. Deep shadows. Strong separation between subject and background.
**Light quality**: Direct midday sun. Strong directional light with sharp shadow edges.

**Compositional rule**: Every summer frame must contain at least one saturated "hot" accent. If a summer frame could be mistaken for autumn, the saturation is too low.

---

### 4.3 Season III: Autumn — Adulthood (中年)

**Time of day metaphor**: Late afternoon / golden hour. Light is horizontal, warm, and transitory. Everything is tinted by amber sun that has traveled far across the sky.

| Color | Name | Approximate Hex | Emotional Meaning | Usage |
|---|---|---|---|---|
| 赭石 | Ochre | `#B0895C` | Earned wisdom, the weight of decisions already made | Earth, wood, interiors; dominant grounding color |
| 枯茶 | Withered Tea | `#8C6E4A` | Settled warmth, comfort that has cooled to room temperature | Shadow areas; secondary figures; things held for a long time |
| 枫红 | Maple Red | `#B5412C` | Lingering passion — the echo of vermilion, deeper and more complex | Important memory anchors; spirit's interior near regret |
| 暮光 | Dusk Gold | `#D4A96A` | The golden hour of life — beauty of knowing it will not last | Key light; hair-light on figures; memory objects of great significance |
| 灰蓝 | Slate Blue | `#6B7B8A` | Reflective calm, the cooling that comes with perspective | Distant elements; things accepted but not celebrated |

**Dominant hue range**: Warm earth tones (20°-45°, shifted toward brown)
**Saturation range**: 20-45% — desaturated from summer. Experience mutes feeling. This is not less — it is different.
**Contrast range**: Medium. Shadows present but soft-edged.
**Light quality**: Golden hour. Warm directional light from low angle. Long shadows.

**Compositional rule**: No autumn frame may use a fully saturated primary color. Every hue must carry ≥15% perceptual gray.

---

### 4.4 Season IV: Winter — Twilight (暮年)

**Time of day metaphor**: Dusk fading into night. The last light at the horizon — thin band of warm color surrounded by vast cool darkness. Stars appear one by one, each one a memory.

| Color | Name | Approximate Hex | Emotional Meaning | Usage |
|---|---|---|---|---|
| 霜白 | Frost White | `#D8DEE3` | The cold clarity of distance — memory seen from far away | Dominant negative-space color; atmosphere; scroll surface |
| 墨灰 | Ink Gray | `#6B6B6B` | Fading recollection — things almost no longer remembered | Midground forms; dissolving edges; secondary elements |
| 寒蓝 | Cold Blue | `#5A7A8A` | Detachment, the remove of time, vastness of what has passed | Sky and distance; dominant cool domain |
| 残光 | Lingering Light | `#E8C080` | The last warmth — a single candle, a hand held, a final word | Isolated warm pinpoints; spirit's interior; most important anchors |
| 暗紫 | Dusk Violet | `#5C5060` | The color of ending itself — neither warm nor cold, the threshold | Winter scene borders; chapter-ending transitions; closing scroll border for loss-path endings |
| 星白 | Star White | `#F0EDE8` | Small, sharp points of light — memories that survived fading | Individual bright pinpoints in winter darkness |

**Dominant hue range**: Cool grays and blues (200°-260°, heavily desaturated)
**Saturation range**: 5-20% overall. Warm pinpoints may reach 40-60% but are small and sparse.
**Contrast range**: Low overall, with isolated extreme contrast. The eye finds warm pinpoints like stars in dusk.
**Light quality**: Dusk/darkness. Point-source light only. The scroll itself is the darkness; memory is the light within it.

**Compositional rule**: ≥80% of frame in cool gray domain (<10% saturation). Warm pinpoints ≤5% of total frame area.

---

### 4.5 Base Unifying Palette — The Scroll Itself

The material substrate of the entire game. The scroll, the ink, the paper — the physical reality that all four seasonal palettes sit on. Memory transition spaces and the main menu live here.

| Color | Name | Approximate Hex | Emotional Meaning | Usage |
|---|---|---|---|---|
| 宣纸 | Rice Paper | `#F5F0E8` | The scroll surface — receptive, warm-neutral, the color of waiting | Dominant negative-space; menu background; "empty" scroll in transitions |
| 墨 | Sumi Ink | `#2C2416` | The deepest mark — near-black with brown warmth; permanence | Outlines, text, darkest shadows; brushstroke core weight; transition ink-thread lines |
| 淡墨 | Light Ink Wash | `#8A8078` | Diluted ink — things half-present, half-remembered | Distant forms; atmospheric depth; faded state of inactive UI |
| 古纸 | Aged Paper | `#E8DEC8` | Aged scroll border — carrying the weight of time | Scroll borders; menu framing; closing border during chapter endings |
| 朱墨 | Vermilion Ink | `#A03828` | The accent ink — the seal, the signature, the mark of significance | Choice confirmation; spirit's eye dots; completed chapter progress; rarest accent |
| 金泥 | Gold Dust | `#C4A456` | Highest-significance accent — supreme narrative weight | Chapter completion acknowledgment; title characters at most "dry" state; used ≤5 times in entire game |

**Usage rule**: When in doubt about any neutral element — text, scroll edge, transition thread — default to the base palette. Seasonal palettes are chromatic; the base palette is structural.

---

### 4.6 Semantic Color Vocabulary

#### Temperature

| Temperature | Emotional Meaning | Across Seasons |
|---|---|---|
| **Warm** (amber through vermilion) | Presence, connection, something preserved or worth preserving | Spring: default warmth. Summer: passion. Autumn: earned warmth. Winter: isolated pinpoint — most precious warmth. |
| **Cool** (blue through blue-gray) | Distance, removal, passage of time, things lost or fading | Spring: unknown beyond home. Summer: sky of possibility. Autumn: reflective calm. Winter: dominant register. |
| **Neutral** (ink grays, rice paper) | The observing self — spirit's baseline state | Constant across all seasons — the material truth that does not change |

**Rule**: A warm accent in a cool scene = "this matters — pay attention." A cool accent in a warm scene = "something here is distant, lost, or unreachable." Temperature contrast, not absolute temperature, carries the information.

#### Saturation

| Saturation | Emotional Meaning | When It Appears |
|---|---|---|
| **High** (60-80%) | Immediacy, urgency, emotional peak — happening NOW | Summer chapter; choice moments at critical memory stakes |
| **Medium** (30-55%) | Lived experience — real but processed | Autumn; spring; normal exploration |
| **Low** (5-25%) | Distance, age, fading — weathered by time | Winter; memory transitions; elements the player chose to let go; periphery during choice moments |
| **Near-zero** (<5%) | The void — the unformed, the forgotten | Deep memory transition; edge of winter scenes; irrevocably lost |

**Rule**: Saturation spike in low-saturation scene = "this is what survived." Desaturation in saturated scene = "this is what was lost." Saturation change IS the choice outcome.

#### Color Temperature Shift as Choice Feedback

- **Temperature shift toward warm** = memory was preserved, deepened, or reconnected
- **Temperature shift toward cool** = memory was lost, distanced, or let go
- **No temperature shift but desaturation** = memory was transformed — changed into something different
- **Temperature split** (warm light source, cool shadows) = bittersweet — something gained and lost simultaneously

---

### 4.7 Choice Feedback Through Color

*Serves Pillar 1: 选择即重写. Direct expression of Principle 2: 颜色即是情绪.*

| Outcome | Palette Shift | Visual Signature | Duration |
|---|---|---|---|
| **Preserved Memory** | Anchor warm color intensifies (saturation +15-25%). Surrounding cool elements warm ~5°. Spirit interior brightens. | Anchor "ignites" — warm color spreads from anchor in soft wash, like ink bleeding into wet paper. | 2-4 seconds |
| **Lost Memory** | Anchor desaturates (saturation -30-50%). Color temperature shifts cool ~10°-20° toward blue. Spirit interior dims and cools. | Anchor "recedes" — color drains toward base palette's light ink wash. Becomes a ghost of its former self. | 3-5 seconds |
| **Transformed Memory** | Anchor shifts hue 30°-90° while maintaining saturation. Spirit interior shifts to match new hue. | Anchor undergoes "color bloom" — original color drains while new color fills in from edges inward. Form stays; emotional color is rewritten. | 3-5 seconds |
| **Bittersweet** | Anchor maintains warm core but gains cool-edge bleeding. Composition develops warm-light/cool-shadow split. Spirit interior becomes two-toned gradient. | Warm center + cool edges. Frame develops split-temperature character. | 4-6 seconds |

**Implementation note**: Palette shifts are gradual transitions rendered as "ink bleed" animations — new color spreads from anchor outward like ink dropped into water, following the brush-texture of the scene.

**Design test**: Player must identify choice outcome type from color alone within 2 seconds of shift completing.

---

### 4.8 UI Palette

*UI is painted into the scroll, not surfaced on top. UI palette drawn from base palette with restrained seasonal accent borrowing.*

| UI Element | Color | Source |
|---|---|---|
| **Primary text** | 墨 Sumi Ink `#2C2416` | Base palette |
| **Secondary / inactive text** | 淡墨 Light Ink Wash `#8A8078` | Base palette |
| **Selection highlight** | 朱墨 Vermilion Ink `#A03828` at 30-40% opacity, as ink wash behind text | Base palette |
| **Progress dots (filled)** | 朱墨 Vermilion Ink `#A03828` | Base palette |
| **Progress dots (empty)** | 淡墨 Light Ink Wash `#8A8078` at 50% opacity | Base palette |
| **Progress dots (current)** | Current chapter's seasonal warm accent (e.g., 樱色 for spring, 金盏 for summer, etc.) | Seasonal palette |
| **Choice option ink stroke** | Current season's dominant warm color | Seasonal palette |
| **Interaction prompt** | 朱墨 Vermilion Ink `#A03828` + subtle pulse opacity (30% → 60% cycle) | Base palette |
| **Scroll border** | 古纸 Aged Paper `#E8DEC8` | Base palette |

**Readability rule**: All text must maintain ≥4.5:1 contrast ratio against background (WCAG AA). Sumi Ink on Rice Paper ≈12:1. Light Ink Wash on Rice Paper ≈3.5:1 — acceptable for secondary/non-critical text only.

---

### 4.9 Memory Transition Palette

*Serves Pillar 3: 关联的网络. The liminal void where one memory dissolves and the next forms.*

| Element | Color | Source |
|---|---|---|
| **Transition background** | 宣纸 Rice Paper `#F5F0E8` fading to near-white at midpoint | Base palette |
| **Ink-thread lines** | 墨 Sumi Ink `#2C2416` at 40-60% opacity, variable brush weight | Base palette |
| **Memory node points** | Brightened seasonal accent from associated memory | Departing/arriving seasonal palette |
| **Color bleed (departing)** | Departing memory's dominant color fades from 40% → 0% over first 1.5s | Departing seasonal |
| **Color bleed (arriving)** | Arriving memory's dominant color fades in from 0% → 30% over last 1.5s, then snaps to full palette | Arriving seasonal |
| **Void midpoint** | Near-white `#F8F6F2` — pure scroll, unmarked by any season | Base palette |

**Transition structure (2-5 seconds)**:
1. **Departure (0-1.5s)**: Dominant warm color bleeds into scroll surface. Scene detail dissolves into ink-wash. Color stain fades.
2. **Void midpoint (~0.5s)**: Pure scroll. Ink-thread lines visible. Player drifts without steering.
3. **Arrival (1-2s)**: New memory's color stains first as faint wash (30%), then detail precipitates out — forms condense from colored wash.

---

### 4.10 Colorblind Safety

#### Problematic Color Pairs

| Risk Pair | Deficiency | Backup Cue |
|---|---|---|
| 樱色 `#F4C2C2` vs. 新绿 `#C5D8B5` (spring warmth vs. growth) | Protanopia, Deuteranopia | **Shape**: pink elements = petal silhouette; green = leaf silhouette. **Texture**: pink = soft wash edges; green = directional brush strokes. |
| 朱砂 `#D4352C` (passion) vs. 枫红 `#B5412C` (earned warmth) | Protanopia | **Saturation context**: vermilion in high-saturation summer; maple red in desaturated autumn. **Sound**: summer choice = higher-pitched bell; autumn choice = deeper, woodier tone. |
| 墨灰 `#6B6B6B` vs. 寒蓝 `#5A7A8A` (winter depth) | Tritanopia | **Texture**: cold blue = dry-brush texture; ink gray = wet-wash. |
| 碧蓝 `#3B7CB8` vs. 深靛 `#3A3068` (summer sky vs. depth) | Deuteranopia | **Value**: azure significantly lighter. **Position**: azure = sky/background; indigo = foreground/ground. |
| 残光 `#E8C080` (winter pinpoint) vs. 金盏 `#E8A840` (summer energy) | Protanopia, Deuteranopia | **Size**: winter pinpoint ≤5% frame; summer accent 15-30% frame. **Isolation**: winter = surrounded by near-monochrome; summer = among saturated colors. **Sound**: winter pinnacle = single sustained string; summer = fuller orchestration. |

#### General Rules

1. Never communicate choice outcome through hue alone — pair with saturation, value, and texture change
2. Warm vs. cool must also be light vs. dark — enforce ≥20% value difference
3. Memory node points in transitions use brightness rhythm variance (spring 1.5Hz, winter 0.5Hz) alongside color
4. All interactive elements use a distinctive brush-texture edge treatment visible in grayscale
5. Every significant color event has a corresponding audio cue

---

### 4.11 Palette Cross-Reference by Game State

| Game State | Active Palette | Notes |
|---|---|---|
| **Exploring memories** | Current chapter's seasonal palette (full range) | Spirit's interior wash reflects proximity to warm/cool anchors |
| **Choice moment** | Seasonal palette + high contrast + anchor isolation | Periphery desaturates toward base; anchor saturates toward seasonal peak |
| **Memory transition** | Base palette + departing/arriving color bleed | Only state where two seasonal palettes briefly touch |
| **Chapter ending** | Seasonal at maximum range, then resolving to base | Warm path: warm at peak saturation. Loss path: desaturated + base dominant. Bittersweet: split-temperature |
| **Main menu** | Base palette exclusively + 朱墨 for title accents | No seasonal colors. Menu lives in scroll material. |

---

### 4.12 Design Tests

**Test 1 — Grayscale Season Identification**: Present one frame from each season, transition, and menu — all grayscale. 5 participants. Pass: 4/5 correctly identify transition; 4/5 group warm seasons (spring+autumn) separate from winter.

**Test 2 — Choice Outcome Readability (No Text)**: Four 4-second outcome animations (preserved/lost/transformed/bittersweet), no text. 5 participants. Pass: 4/5 identify preserved and lost correctly; 3/5 identify transformed and bittersweet.

**Test 3 — Seasonal Palette Uniqueness**: Four seasons side by side, full color. Pass: Zero participants group spring with autumn, or summer with winter.

**Test 4 — Base Palette Constancy**: Rice Paper `#F5F0E8`, Sumi Ink `#2C2416`, Light Ink Wash `#8A8078` must match hex spec exactly in every frame. No substitutions.

**Test 5 — Winter Pinpoint Constraint**: Analyze 3 winter frames. Pass: Warm colors occupy <8% of frame area in all three.

---

### 4.13 Quick Reference Card

```
SPRING (Childhood): Warm pinks/peaches/soft greens. Saturation 15-40%. Low contrast. Morning light.
  Key: 樱色 #F4C2C2, 新绿 #C5D8B5, 桃色 #F0CFB0
  Rule: 60%+ warm domain. Cooling accents ≤25% frame.

SUMMER (Youth): Vivid blues + saturated warm spikes. Saturation 50-80%. High contrast. Midday light.
  Key: 朱砂 #D4352C, 碧蓝 #3B7CB8, 金盏 #E8A840
  Rule: At least one saturated "hot" accent per frame.

AUTUMN (Adulthood): Earthy ochres/browns/muted reds. Saturation 20-45%. Medium contrast. Golden hour.
  Key: 赭石 #B0895C, 枫红 #B5412C, 暮光 #D4A96A
  Rule: No fully saturated primary. Every hue carries ≥15% perceptual gray.

WINTER (Twilight): Cool grays/blues + isolated warm pinpricks. Saturation 5-20%. Dusk.
  Key: 墨灰 #6B6B6B, 寒蓝 #5A7A8A, 残光 #E8C080
  Rule: ≥80% cool gray domain. Warm pinpoints ≤5% frame.

BASE (Scroll): The material substrate all seasons sit on.
  Key: 宣纸 #F5F0E8, 墨 #2C2416, 朱墨 #A03828
  Rule: Any neutral element defaults here.

CHOICE OUTCOMES:
  Preserved  → +Saturation, +Warm shift. Anchor "ignites."
  Lost       → -Saturation, +Cool shift. Anchor "recedes."
  Transformed→ Hue shift 30-90°. Anchor "blooms" new color.
  Bittersweet→ Warm core + cool edges. Split-temperature.

VIOLATIONS:
  DO NOT use pure black (#000000) — use Sumi Ink #2C2416
  DO NOT use pure white (#FFFFFF) — use Rice Paper #F5F0E8
  DO NOT use fully saturated primary in autumn or winter
  DO NOT place warm/cool at equal value — enforce ≥20% difference
  DO NOT communicate choice outcome through hue alone
```

---
## 5. Character Design Direction

**One-line rule**: Every figure is a brushstroke-memory of a person, not the person themself. The spirit drifts through; the remembered flicker and fade; the brush decides what survives.

*Serves all four pillars — character design is where choices, imperfection, association, and breathing scrolls converge in human form.*

---

### 5.1 The Spirit (游魂) — The Player's Form

#### Core Archetype

The spirit is the player's physical presence in the memory world — a drifting, weightless form rendered in brush and wash. It is the only character the player inhabits, and the only character present in every frame.

| Quality | Specification | Pillar |
|---|---|---|
| **Form** | Human outline, unclosed contour. Head and torso are suggested with the most complete strokes; hands dissolve into suggestion; feet fade before reaching any ground plane. The form never stands — it always leans 2-5 degrees in the drift direction. | P4 (always mid-journey) |
| **Line** | Variable-weight brush line in Sumi Ink `#2C2416`. Weight varies 1:8 from thinnest to thickest within a single contour. No uniform stroke. | P2 (hand-made imperfection) |
| **Interior** | Faint wash fill that shifts color and opacity based on emotional proximity to memory anchors (see 5.1.5). Never fully opaque — the scroll surface always shows through. | P1 (choices rewrite color) |
| **Eyes** | Two brush-dot marks, 2-3px diameter at scroll distance. Placed asymmetrically — one dot is always slightly higher than the other. Directional: the higher dot is on the drift side, suggesting gaze-forward without defining an expression. | P3 (hinted presence, not defined face) |
| **Scale** | 5-8% of frame height at scroll distance (54-86px at 1080p). | P3 (spirit is guest, not conqueror) |
| **Silhouette** | Asymmetrical. No hard angles. The overall shape is a soft, leaning teardrop-with-head — wider at the shoulders (suggested), tapering to nothing at the feet. | P4 (weightless, always flowing) |

#### Seasonal Variation

The spirit's form is constant in structure, but its *rendering qualities* shift with each season's memory world. These shifts are subtle — the player should feel the change before they consciously notice it.

| Quality | Spring (Childhood) | Summer (Youth) | Autumn (Adulthood) | Winter (Twilight) |
|---|---|---|---|---|
| **Line weight** | Thinnest, most tentative. 1:4 weight ratio. Occasional slight tremble in the stroke. | Boldest, most confident. 1:8 weight ratio. Clean, decisive strokes. | Variable — some sure, some hesitant. 1:6 ratio. Dry brush enters at extremities. | Thinnest, most broken. 1:3 ratio. Dry brush dominant. Lines fracture into segments. |
| **Contour openness** | Most open — large gaps at hands, feet, and one shoulder. The form is barely holding together. | Least open — the contour is at its most complete. Hands and feet are suggested, not dissolved. | Re-opening — hands dissolve again; feet fade more completely. One shoulder gap returns. | Most open — gaps at both shoulders, hands entirely dissolved, feet absent. The form is nearly un-made. |
| **Interior wash** (baseline, no anchor proximity) | Sakura Pink `#F4C2C2` at 15-25% opacity. Gentle warmth without definition. | Azure `#3B7CB8` at 20-30% opacity, shifting to Vermilion `#D4352C` accent near passion anchors. | Ochre `#B0895C` at 15-25% opacity. Earthy, grounded warmth. | Near-transparent. Lingering Light `#E8C080` at 5-10% opacity. Barely there. |
| **Eye dots** | Slightly larger (3-4px). More ink spread — the dots have soft halos. Childlike openness. | Sharpest definition (2-3px, crisp edges). The spirit knows itself. | Standard (2-3px), with slight dry-brush halo on one side. | Faintest (1-2px), near-transparent. The spirit is almost gone. |
| **Presence quality** | Curious, unformed, receptive. | Confident, directed, the most "solid" the spirit ever feels. | Weighted, complex, carrying multiple temperatures at once. | Fading, distant, the observer more than the participant. |
| **Dominant brush mode** | Wet wash (湿染) — soft, bleeding edges. | Firm brush — clean line, minimal bleed. | Mixed — wet base with dry brush (枯笔) accent. | Dry brush (枯笔) + flying white (飞白) — the brush is running out. |

**Invariant across all seasons**: The spirit is always an unclosed contour. The eyes are always two dots. The form always leans. The interior wash always shifts with emotional proximity. These four constants ensure the player always recognizes themself.

#### Choice Moment Rendering (Push-In Distance)

During choice moments, the camera pushes in. The spirit grows from 54-86px (scroll distance) to 150-250px (push-in distance). At this scale, the rendering changes:

| Quality | Scroll Distance (54-86px) | Push-In Distance (150-250px) |
|---|---|---|
| **Contour** | Reads as a single variable-weight gesture line. | Reads as a compound stroke — visible as 2-4 overlapping brush marks. The eye can trace where individual brush hairs separated. |
| **Interior wash** | Flat or simple two-tone gradient. Reads as a single color field. | Visible brush texture — wash direction, water-content variation, paper grain showing through. Subtle ink-bleed at wash edges. |
| **Eye dots** | 2-3px marks. Direction barely legible. | 5-8px marks. Visible as deliberate brush dabs — slight irregularity in shape. Directional gaze is readable. |
| **Line texture** | Smooth at this scale. | Dry-brush texture legible where present. Flying-white streaks visible in faster contour segments. |
| **Unclosed quality** | Reads as "the line stops." | Reads as "the brush lifts" — the feathered end of a real brush stroke is visible. |
| **Emotional readability** | Posture and interior wash color carry all emotional information. | Posture is precise — head tilt, shoulder angle, drift lean are all individually readable. Interior wash shows emotional gradient (warm side / cool side). |

**Push-in trigger**: The camera begins its push when the player enters a choice anchor's proximity zone. The push takes 1.5-2 seconds. The spirit resolves to push-in detail by the 1-second mark. The remaining 0.5-1 second is the held-breath moment before choice options appear.

#### Memory Transition Appearance

During memory transitions (see Section 2, State 3), the spirit's rendering shifts:

- **Opacity**: Interior wash drops to 30-50% of its normal opacity. The spirit becomes more transparent — it is between memories, less present in any one.
- **Line**: Contour line thins to its lightest weight. Variable-weight range compresses (1:2 ratio maximum).
- **Color**: Interior wash shifts to the departing memory's dominant color for the first half of the transition, then to the arriving memory's color for the second half. There is a brief "void moment" at the midpoint where the interior is nearly clear.
- **Eye dots**: Fade to ~30% opacity. The spirit is not looking at anything — it is being carried.
- **Drift**: The spirit's lean angle increases from 2-5 degrees to 5-8 degrees — it is being pulled by the current of association, not steering.

#### Emotional Proximity System

The spirit's interior wash is the player's subconscious emotional compass. It shifts in real time as the spirit drifts closer to or farther from memory anchors.

| Proximity Zone | Distance from Anchor | Interior Wash Response |
|---|---|---|
| **Distant** (>30% frame width) | Anchor is visible but far. | Baseline seasonal wash. Cool-neutral temperature. Low opacity (15-20%). The spirit observes without investment. |
| **Approaching** (15-30% frame width) | Anchor is becoming relevant. | Wash warms 5-10 degrees. Opacity increases to 20-30%. A faint warm bloom appears on the side of the spirit facing the anchor. |
| **Near** (5-15% frame width) | Anchor is important. Choice potential. | Wash matches anchor's dominant emotional color at 30-40% opacity. Warm/cool split develops — the side facing the anchor is warmer. |
| **Adjacent** (<5% frame width, choice trigger zone) | Choice moment imminent. | Wash fully matches anchor's emotional temperature at 40-50% opacity. The spirit and the anchor share a color field. Eye dots gain a faint highlight from anchor's warm color. |

**Cold proximity rule**: Some anchors carry regret, loss, or distance. For these, "proximity" means the wash *cools* rather than warms. The spirit's interior shifts toward Slate Blue `#6B7B8A` or Cold Blue `#5A7A8A`. The change is still readable — it's just cool instead of warm. The player learns: *warm = connection, cool = distance, neutral = observing.*

**Transition speed**: Wash shifts are smooth interpolations over 1-2 seconds of drift. They never snap — the spirit's emotional response lags slightly behind its physical position, like an afterimage of feeling.

---

### 5.2 Memory Figures (NPCs) — The Remembered

#### The Cast Constraint

回响 has 3-6 significant memory figures total — 1-2 per life-stage chapter. This constraint is a feature, not a limitation. With only a handful of figures, every one receives bespoke visual treatment. No figure is "NPC #4 with a different hat." Every figure is a named presence in the player's emotional world.

**Rationale**: At 3-6 figures, the character artist can hand-craft every silhouette, every brush-quality fingerprint, every gesture motif. At 12-15 figures (standard for narrative games), figures become variations on a template. The small cast means every figure is a specific memory of a specific person — and the art must make them feel irreplaceable.

**Distribution guideline**: 1-2 per chapter. If a figure spans multiple chapters (e.g., a childhood friend who reappears in youth, a parent who appears in childhood and adulthood), they count toward both chapters' allocation but are still one figure in the cast total.

#### The Visual Signature System

Each significant memory figure is defined by four interlocking qualities. Together, these form the figure's visual signature — a gestalt that persists across different memory fragments, different ages, and different emotional contexts.

##### Component 1: Silhouette Motif

A distinctive shape that identifies the figure even in grayscale, even at scroll distance. This is the figure's visual "name."

- Must be unique within the cast — no two figures may share the same silhouette motif
- Must read at scroll distance (162-324px figure height) — detail that only works at push-in does not count
- Must survive aging — if the figure appears in multiple chapters, the motif evolves but remains recognizable
- Examples of viable motifs: a figure who always stands with one hand behind the back; a figure whose garment falls in a distinctive asymmetrical drape; a figure with a unique hair ornament silhouette; a figure whose shoulders have a specific slope and angle

**Design test**: Silhouette the figure (fill entirely with Sumi Ink `#2C2416`, no interior detail). Show to a playtester alongside silhouettes of the other cast members. They must identify the figure by silhouette alone. If they cannot, the motif is not distinctive enough.

##### Component 2: Brush-Quality Fingerprint

Each figure is rendered with a characteristic brush treatment. This is as identifying as a voice.

| Fingerprint Type | Brush Quality | Emotional Association | Example Application |
|---|---|---|---|
| **Wet-presence** | Slightly wetter strokes, soft ink-bleed at edges, colors bloom beyond line boundaries. | Warmth, openness, emotional availability. | A nurturing figure — their lines are always a little soft, a little blurred at the edges. |
| **Dry-presence** | Dry brush (枯笔) dominant, streaky porous strokes, bristle marks visible. | Age, restraint, formality, hidden depth. | A stern or distant figure — their lines are crisp, controlled, revealing little. |
| **Mixed-presence** | Variable wet/dry within the same figure. Wet at the core (torso, head), dry at extremities (hands, garment edges). | Complexity, internal contradiction. | A figure the player has conflicting feelings about — their rendering itself is conflicted. |
| **Firm-presence** | Clean, confident strokes. Minimal bleed. Strong line weight variation. | Certainty, solidity, "this person knows who they are." | A figure who represents stability or authority. |

**Persistence rule**: A figure's brush-quality fingerprint persists across all appearances. If a figure is "wet-presence" in spring, they remain identifiably "wet-presence" in autumn — even if the wetness is now tinged with the season's desaturation. The fingerprint may evolve but must never be replaced.

##### Component 3: Color Thread

A specific accent color, drawn from the figure's first seasonal appearance, that follows them through every memory fragment in which they appear.

| First Appearance | Color Thread | How It Follows |
|---|---|---|
| **Spring** | Sakura Pink `#F4C2C2` | A ribbon, a flower motif, a blush in the figure's garment lining. In later chapters, the pink desaturates but remains identifiable. |
| **Summer** | Vermilion `#D4352C` or Azure `#3B7CB8` | A sash, a hair ornament, a distinctive accessory. In later chapters, the vermilion becomes Maple Red `#B5412C` — the same color, aged. |
| **Autumn** | Dusk Gold `#D4A96A` | A piece of jewelry, embroidery, a particular light-catching element. In winter, it becomes Lingering Light `#E8C080` — gold fading to memory. |
| **Winter** | Lingering Light `#E8C080` | A pinprick of warmth in the cold. By definition, this thread is small and precious. It never grows larger — it may only become more isolated. |

**Color thread rule**: The thread occupies 3-8% of the figure's total color area. It is an accent, not a costume. It should be visible but not loud — the player should notice it unconsciously the first time, and consciously on reflection. If the thread screams for attention, it is too large.

##### Component 4: Gesture Motif

A recurring physical gesture or posture unique to the figure. This is the most "animated" element — it may appear in different contexts but always reads as *them*.

- A specific way of holding the head (slight tilt, angle of gaze)
- A hand gesture (fingers together vs. apart, palm open vs. closed)
- A way of standing (weight on one foot, specific shoulder angle)
- An interaction gesture (how they reach for things, how they turn)

**The gesture motif is a shape, not an animation**: It is the *pose the figure returns to* across different memory fragments. The figure may be doing different things in different memories, but their "resting" body language is always recognizable.

#### Distinguishing Rules

##### Spirit vs. Memory Figures

| Quality | Spirit | Memory Figure |
|---|---|---|
| **Contour** | Unclosed — always incomplete. | Closed or near-closed — the figure is "whole" in this memory. |
| **Eyes** | Two brush dots. No face. | Suggested facial features — eye line, brow gesture, mouth suggestion. |
| **Interior** | Wash that shifts with emotional proximity. | Rendered with garment/body color and shading. Interior is the figure's own color, not the memory's. |
| **Grounding** | Never touches ground. Always drifts. | May stand, sit, or occupy space. Figures are *in* the memory; the spirit is *passing through*. |
| **Scale** | 5-8% frame height. Consistent across all memories. | 15-30% frame height at scroll distance. Varies by memory importance. |

##### Figure vs. Figure (Within the Cast)

Each figure's visual signature (silhouette + brush fingerprint + color thread + gesture) must differ from every other figure's on at least **three of four** components. Overlap on one component is acceptable (two figures may both have warm color threads, for instance) — but if two figures share a silhouette motif AND a brush fingerprint, they are not visually distinct enough.

##### Figure vs. Background

| Quality | Memory Figure | Background Element |
|---|---|---|
| **Stroke density** | 20-50+ strokes (Hero to Supporting range, per Section 3 hierarchy). | 3-8 strokes (Background range). |
| **Contour completeness** | Closed or near-closed silhouette. | Open, suggested, dissolving at edges. |
| **Color saturation** | Full seasonal saturation for the figure's emotional register. | Muted by at least 30% compared to foreground figures. |
| **Edge treatment** | Defined edges. Ink-bleed is controlled and intentional. | Dissolving edges. Ink-bleed is uncontrolled, fading into negative space. |
| **Motion** | May have subtle micro-animation (breathing suggestion, garment sway). | Static or part of environmental motion (wind through trees, not through people). |
| **Eye contact** | Figures may "look" toward anchors or the spirit (suggested gaze direction). | Background figures never make directional eye contact — they are scenery, not subjects. |

#### Figure Hierarchy and Stroke Density

| Hierarchy Level | Stroke Count | Detail Level | Who Occupies This Level | Example |
|---|---|---|---|---|
| **Significant Figure** (current memory's subject) | 30-60 strokes | Fully realized. Silhouette, garment, facial suggestion, gesture all defined. Color thread present. Brush fingerprint legible. | The 1-2 figures per chapter; the memory's emotional center. | A parent in a childhood memory — every fold of their garment, the specific way they hold their head. |
| **Supporting Figure** (present in memory, not the focus) | 12-25 strokes | Suggested but not completed. Clear silhouette, basic garment, facial suggestion minimal. Gesture readable but not distinctive. | Figures who populate a memory but are not its emotional subject. | A crowd at a festival — individual figures, but rendered with economy. |
| **Background Figure** (atmosphere, not individual) | 5-10 strokes | Bare minimum. Person-shape readable. No individual features. No color thread. | Distant people in a landscape memory; passersby; figures the spirit does not know. | Silhouettes in a misty street. |
| **Fading Figure** (being forgotten) | 3-6 strokes, decreasing | Dissolving. Contour breaking. Color draining to base palette. | A figure the player chose to let go; a memory being lost. | A figure dissolving into ink-wash at the edge of a winter frame. |

---

### 5.3 Expression and Pose Style

#### The Restrained Brush Philosophy

> 写神不写形 — Capture the spirit, not the shape.

In the 回响 visual language, emotion lives in the brushstroke, not the face. The game's emotional range is "restrained but with peaks" — mostly contemplative, with specific moments of strong emotion. The character design must deliver this range through posture, brush quality, and the physical gesture of ink on paper.

**Core principle**: Facial micro-expressions are a tool of photorealism and animation. 回响 is neither. The brush can suggest a downturned mouth, a raised brow — but it cannot (and should not) render 47 distinct facial muscle movements. Posture carries 80% of emotional information. Brush quality carries 15%. Facial suggestion carries 5%.

#### Posture and Gesture as Primary Emotional Carriers

The body speaks before the face. At scroll distance (where the player spends most of their time), facial features are not legible. Posture must carry the emotional register.

| Emotion | Posture Signature | Head/Neck | Hands/Arms | Spirit Expression |
|---|---|---|---|---|
| **Quiet wonder** | Slight forward lean. Open chest. Weight on forward foot. | Head tilted 3-5 degrees upward. | Hands slightly apart from body, palms open or neutral. | Spirit's drift lean increases toward the object of wonder. Interior wash warms. |
| **Melancholy** | Weight shifted back. Shoulders slightly rounded. Stationary or very slow drift. | Head tilted 3-5 degrees downward. | Arms close to body. Hands still or clasped. | Spirit's drift slows. Interior wash cools toward blue-gray. |
| **Gentle sadness** | Body curved slightly inward — a subtle C-curve of the spine. Stillness. | Head bowed 5-10 degrees. | Hands touching or near face/chest. | Spirit's contour opens more — more gaps. Interior wash desaturates. |
| **Tenderness** | Body open but leaning in. The figure inclines toward the object of tenderness. | Head tilted, gaze soft (eyes suggested as horizontal dashes rather than dots). | Hands extended or reaching, but not grasping. | Spirit's interior wash blooms warm — the softest, most even gradient. |
| **Gravity / weight** | Body grounded. Feet planted (for figures). Shoulders squared but not tense. | Head level. Direct gaze. | Arms at sides or one hand resting on something solid. | Spirit's drift slows to near-zero. Interior wash deepens in saturation. |
| **Distance / removal** | Body turned slightly away. 3/4 view rather than frontal. Weight on back foot. | Head turned aside. Gaze averted. | Arms crossed or hands behind back. | Spirit's interior wash cools. The spirit may drift slightly away — not fleeing, but creating space. |
| **Anticipation** | Weight forward. Body aligned toward the anticipated object. | Head lifted. Gaze directed. | Hands slightly raised or open, ready. | Spirit's lean angle increases. Interior wash brightens. |
| **Regret** | Body curved inward. Weight heavy. Stillness — the figure is frozen in the moment of understanding. | Head bowed or turned fully away. | Hands covering face or hanging limp. | Spirit's interior wash drops in saturation. Contour line thins. |

**Posture language rule**: No posture should require more than 3 brushstrokes to read. If a posture needs 8 strokes to communicate "sadness," it is the wrong posture. The body's emotional language must be legible from its silhouette alone.

#### Brush Quality and Emotional State

The brush itself carries emotion. The same figure rendered with a different brush quality is the same person in a different emotional register.

| Brush Quality | Technique | Emotional Register | When It Dominates |
|---|---|---|---|
| **Wet wash (湿染)** | Loaded brush, soft paper. Color blooms beyond the line, edges are soft, forms bleed gently into their surroundings. | Tenderness, warmth, openness, vulnerability, love. | Reunion scenes; childhood memories; moments of connection; the spirit near warm anchors. |
| **Dry brush (枯笔)** | Nearly-dry brush, textured paper. Strokes are streaky, porous, split. Bristle marks visible. Paper shows through. | Grief, age, distance, restraint, things half-remembered. | Loss scenes; winter twilight; a figure who is fading; regret-path endings. |
| **Flying white (飞白)** | Fast brush, dry or damp. White streaks tear through the stroke where the brush moved too fast for ink to fill. | Urgency, incompleteness, emotional velocity, things in motion. | Choice options emerging; memory transitions; a figure in a moment of sudden realization. |
| **Broken ink (破墨)** | Dark ink dropped into still-wet wash. Uncontrolled bleeding, feathered edges, ink blooms. | Emotional overwhelm, memory overwriting, intensity that breaks containment. | A fight; a confession; the moment of loss; choice resolution on high-stakes anchors. |
| **Accumulated ink (积墨)** | Multiple layers of ink and wash built up over time. Depth through accumulation. | Weight, history, things carried for a long time. | Chapter endings; a figure who has appeared across multiple chapters; the closing scroll border. |
| **Firm brush (正锋)** | Controlled, centered brush. Clean line, minimal bleed, deliberate weight variation. | Certainty, presence, stability, "this is true." | A figure at their most defined; spirit in summer; choice confirmation. |

**Brush quality shift rule**: When a figure's emotional state changes, the brush quality rendering them shifts first — before their posture changes, before the color shifts, before any text appears. The brush is the fastest emotional signal. A dry-brush figure entering a reunion scene shifts toward wet wash at the edges first; the core follows.

#### Facial Suggestion, Not Description

Faces in 回响 are not drawn — they are *indicated*. The brush suggests enough for the player to project the rest.

| Feature | Specification | What It Communicates |
|---|---|---|
| **Eyes** | Two horizontal or slightly angled dashes (1-3 strokes each). Not dots — the spirit has dots; figures have dashes. Width, angle, and spacing carry expression. | Downward angle = sadness, weariness. Upward angle = openness, warmth. Close spacing = intensity. Wide spacing = openness, distance. |
| **Brows** | Single stroke above each eye, or one continuous stroke across the brow line. | Raised = openness, surprise. Lowered = gravity, intensity. Asymmetrical = complexity, mixed emotion. |
| **Mouth** | Single stroke — a short horizontal, a slight curve, or absent entirely. | Absent = the default. Present only in peak emotional moments. A mouth stroke is an exclamation mark in this visual language. |
| **Nose** | Never rendered. The bridge of the nose is defined by the eye/brow placement and a slight contour suggestion at the profile edge. | — |
| **Head shape** | Oval or subtly angular suggestion. 3-5 strokes for the overall head structure. | Rounder = youth, softness. More angular = age, definition. |

**Facial economy rule**: If a face requires more than 8 strokes total (eyes + brows + head contour + optional mouth), it is over-rendered for this style. The player's imagination completes the face. The brush must leave room for that completion.

#### Peak Emotion Moments

The restrained style breaks — deliberately, briefly — at the game's emotional peaks. These are the moments the player remembers. The brush must rise to meet them.

##### Peak: A Fight (Conflict / Confrontation)

**Emotional target**: The breaking of composure. The brush loses control.

| Element | Shift |
|---|---|
| **Brush mode** | Flying white (飞白) and broken ink (破墨) dominate. The brush moves fast — streaks of paper tear through every stroke. Dark ink drops into wet wash, bleeding uncontrollably. |
| **Line quality** | Weight extremes: hair-thin cuts (0.5-1px visual equivalent) beside thick slashing strokes (8-12px). Lost is the careful variable-weight control of the normal style. |
| **Color** | Vermilion `#D4352C` spikes to full saturation. Color bleeds beyond figure boundaries — it is no longer contained by line. The background may briefly stain red at the edges. |
| **Shape language** | The "no hard angles" rule is suspended. Angles enter — sharp turns in brushstrokes, triangular negative space, intersecting lines that cut across each other. The spirit's form briefly sharpens — it gains temporary edges. |
| **Composition** | Figures may break the frame — a hand or weapon extending past the normal scroll boundary. Negative space contracts. The frame feels crowded, urgent. |
| **Duration** | 2-4 seconds maximum. |
| **Recovery** | The brush slows. Flying white recedes. The spirit's sharpened edges soften. Color bleeds are absorbed back into forms. One element holds the echo of the fight — a single broken-ink bloom, a streak of vermilion that has not yet faded. |

##### Peak: A Loss (Grief / Letting Go)

**Emotional target**: The hollowing. The brush runs out of ink.

| Element | Shift |
|---|---|
| **Brush mode** | Dry brush (枯笔) takes over entirely. The brush has almost no ink. Strokes are porous, streaky, breaking apart. Bristle marks are fully visible — the brush itself is exposed. |
| **Color** | Saturation drops 40-60% over 3-5 seconds. The world drains toward the base palette — toward Rice Paper `#F5F0E8` and Light Ink Wash `#8A8078`. One element (the thing being lost) holds full saturation for one final beat, then fades. |
| **Contour** | Lines that were continuous fracture into segments. The spirit's contour opens wider — new gaps appear. The lost figure's contour begins dissolving at the edges: they are becoming memory in real time. |
| **Interior** | The spirit's interior wash goes nearly transparent. The lost figure's interior wash drains last — their color is the last thing to leave. |
| **Composition** | Heavy negative space. The lost thing occupies a shrinking center. The frame feels vast and empty — the opposite of the fight's crowding. |
| **Duration** | 4-6 seconds for the full drain. The "final beat" of full saturation on the lost element lasts 1-1.5 seconds. |
| **Recovery** | Color does not fully return. The memory world is permanently less saturated — the loss is real. But the brush finds its control again. The lines re-stabilize. The spirit's interior wash settles at a new, cooler baseline. |

##### Peak: A Reunion (Return / Reconnection)

**Emotional target**: The bloom. The brush is full of water and color.

| Element | Shift |
|---|---|
| **Brush mode** | Wet wash (湿染) blooms from the center outward. Soft edges. Color bleeds gently — not the uncontrolled bleed of the fight, but a slow, deliberate spreading, like ink dropped into clear water. |
| **Color** | Warm saturation spreads from the center: Sakura Pink, Dusk Gold, or Lingering Light — whichever warm accent belongs to this season. The saturation spreads in a radial wash, reaching ~30% of frame area. |
| **Contour** | The spirit's unclosed contour approaches near-completion — not fully closed (that would be wrong), but the gaps narrow. Both figures share the same interior wash color for one held moment: they are emotionally congruent. |
| **Light** | Light appears to come from within the figures themselves — an inner glow that is not physically motivated but emotionally true. The background darkens slightly (5-10% value drop) to make the figures' inner light read. |
| **Composition** | Figures centered. Less negative space than normal — briefly breaking the 留白 rule for emotional impact. The moment fills the frame. |
| **Duration** | 3-5 seconds for the bloom. The "shared wash" moment lasts 1-2 seconds. |
| **Recovery** | The wash recedes. Negative space returns. The spirit's contour re-opens to its seasonal norm. But the color temperature of the memory is permanently warmer by 5-10 degrees — the reunion has changed it. |

---

### 5.4 Detail Philosophy

#### The Two Distances

回响 operates at two visual distances, defined by the camera's position. Detail budgets are set by distance.

| Parameter | Scroll Distance | Push-In Distance |
|---|---|---|
| **When active** | Memory exploration (95% of gameplay). | Choice moments (5% of gameplay, highest emotional stakes). |
| **Spirit height** | 54-86px (5-8% frame). | 150-250px (14-23% frame). |
| **Memory figure height** (significant) | 162-324px (15-30% frame). | 432-648px (40-60% frame). |
| **Memory figure height** (supporting) | 108-162px (10-15% frame). | Not applicable — supporting figures are not the subject of choice moments. |
| **Background figure height** | 54-108px (5-10% frame). | Not applicable. |
| **Rendering philosophy** | Gesture over detail. The brushstroke is the unit of information, not the pixel. | Detail emerges from the brush — individual marks become legible. |
| **Surface detail visible?** | No. Paper texture, brush hair marks, ink-bleed grain — none of these read at scroll distance. | Yes. Paper texture, brush hair separation, ink-bleed patterns, and wash gradients are all legible. |
| **Facial features visible?** | No — only head angle, eye-dash placement, and overall face shape. | Yes — eye shape, brow position, mouth gesture, head tilt, gaze direction. But still rendered as brush gestures, not anatomical drawing. |

#### Spirit Detail at Each Distance

| Element | Scroll Distance (54-86px) | Push-In Distance (150-250px) |
|---|---|---|
| **Contour** | Single variable-weight line, 1-3px visual weight. Reads as one continuous gesture. | 2-4 overlapping strokes visible. Individual brush marks legible. Weight varies 1:8 across the visible contour. |
| **Interior wash** | Flat color field with simple gradient (2-3 color stops). Reads as "warm" or "cool." | Gradient with 5-8 color stops. Brush direction visible. Water-content variation legible (wetter = smoother, drier = more textured). |
| **Eye dots** | 2-3px diameter. Two marks. | 5-8px diameter. Shape irregularity visible (not perfect circles — real brush dabs). Slight directional asymmetry legible. |
| **Dry brush texture** | Not visible. Reads as slightly thinner/darker line. | Fully visible. Individual bristle marks, paper showing through, flying-white streaks. |
| **Unclosed gaps** | Read as "the line ends." | Read as "the brush lifts" — feathered stroke terminus visible. Exact lift point legible. |
| **Seasonal line quality** | Barely perceptible. Spring vs. winter line weight difference is 1-2px — at 54-86px, this is near the threshold of visibility. | Fully perceptible. Spring's tentative tremble, summer's confidence, autumn's dry-brush, winter's fracture — all legible as distinct brush treatments. |

#### Memory Figure Detail at Each Distance

| Element | Scroll Distance (162-324px) | Push-In Distance (432-648px) |
|---|---|---|
| **Silhouette** | Clear, distinctive. Silhouette motif must be identifiable. | Silhouette gains interior detail — garment folds, hair texture, accessory shapes all contribute to the silhouette at this scale. |
| **Facial suggestion** | 3-5 strokes: eye dashes, brow line, head contour. Head angle readable. Gaze direction suggested. | 6-8 strokes: eye dashes with subtle curve, brow with weight variation, mouth gesture (if present), head contour with more precise shape. |
| **Garment** | Overall shape and drape. 5-10 strokes for major folds. Color areas defined. | Individual folds, edge treatment, accessory detail. 15-25 strokes. Fabric texture suggested through brush quality. |
| **Gesture** | Overall body posture readable. Gesture motif identifiable. | Hand position, finger suggestion, precise head angle, weight distribution between feet — all individually readable. |
| **Color thread** | Visible as accent color. 3-8% of figure area. | Visible as a specific rendered object (ribbon, jewelry, embroidery) with its own 3-5 brushstrokes. |
| **Brush fingerprint** | Identifiable — wet vs. dry vs. mixed vs. firm reads at this scale. | Fully legible — individual bristle marks, ink-bleed patterns, water-content variation. The fingerprint is experienced, not just identified. |
| **Eye contact** | Direction readable. | Intensity readable — the specific quality of the gaze (soft, searching, direct, averted). |

#### Minimum-Viable Person

The least that still reads as a human figure at 1080p. For background figures, distant crowds, and fading memories.

| Element | Specification |
|---|---|
| **Total height** | 40-60px (4-6% frame height). |
| **Total strokes** | 5-8. |
| **Stroke breakdown** | 1-2 for head (oval or rounded shape). 1 for shoulder line. 2-3 for body column / garment suggestion. 1-2 for a single distinctive feature (arm, hand, garment edge, hair element). |
| **Facial features** | None. The head is a shape, not a face. |
| **Color** | Single wash tone drawn from the seasonal palette at 20-30% saturation. No interior detail. No color thread. |
| **Edge treatment** | Soft — edges dissolve into the atmosphere. The figure is suggested, not stated. |
| **Posture** | One readable posture element: standing, walking, sitting, turned. If the posture cannot be read in 2 seconds, it has failed. |
| **Differentiation** | Minimum-viable figures are differentiated by height, posture, and placement — never by individual features. They are "people in the memory," not "a specific person." |

**Minimum-viable person rule**: If you can tell who it is, it is not a minimum-viable person. Minimum-viable figures are generic by definition. The moment a figure gains individuality, they require Supporting-level stroke density.

---

### 5.5 Per-Chapter Character Design Notes

#### Spring — Childhood (童年)

**The world**: Warm, soft, hazy. The child's eye — things are large, close, and not yet fully categorized. Memory is impressionistic.

**Figure archetypes (1-2)**:
- **The Nurturer**: A parent, grandparent, or guardian. The source of warmth in the child's world. Large in the frame (20-30% height at scroll distance). Rendered with wet wash — soft, bleeding edges. Silhouette motif: encompassing, curved toward the viewer. Color thread: Sakura Pink `#F4C2C2`. Gesture motif: open arms or hands extended downward (toward the child's eye level).
- **The Companion**: A childhood friend, sibling, or animal companion. Equal scale to the nurturing figure when both present, but rendered with firmer brush (child friendships feel solid and eternal). Silhouette motif: small but distinct — a specific hair shape, a specific way of running. Color thread: Fresh Green `#C5D8B5`. Gesture motif: mid-motion — childhood is kinetic.

**Spirit in spring**: Thinnest line. Most open contour. Interior = Sakura Pink at 15-25%. Eye dots slightly larger with soft halos. The spirit is a child-spirit — not remembering childhood, but *being* childhood.

**Spring detail note**: Childhood memories are the most visually simplified — not because they are less important, but because the child's eye does not parse detail. A room is "the warm place" or "the cold hallway," not a specific arrangement of furniture. Background elements should be at their minimum viable — 3-8 strokes. The figures carry all the detail.

#### Summer — Youth (青年)

**The world**: High contrast, high saturation, strong light. The young adult's eye — everything matters intensely. Memory is dramatic.

**Figure archetypes (1-2)**:
- **The Bond**: A close friend, first love, or rival. The figure who defined this season. Rendered with firm brush — clean, confident lines. Silhouette motif: strong, distinctive — this figure cuts a clear shape against the world. Color thread: Vermilion `#D4352C` (passion) or Azure `#3B7CB8` (vast possibility). Gesture motif: energy — a specific way of moving, turning, reaching. This figure is in motion even when still.
- **The Mirror**: A figure who reflects the self back — a mentor, an antagonist, someone who forces the self to be seen. Rendered with mixed brush — wet core, dry edges (internal conflict made visible). Silhouette motif: confrontational or complementary — facing the viewer or standing in counterpoint to the Bond. Color thread: Deep Indigo `#3A3068` (passion's shadow). Gesture motif: stillness within motion — they are the fixed point the self orbits.

**Spirit in summer**: Boldest line. Most complete contour. Interior = Azure at 20-30%, shifting to Vermilion accent near passion anchors. Eye dots sharpest. The spirit is a young-adult-spirit — present, directed, alive.

**Summer detail note**: This is where the brush shows off. Summer figures receive the highest stroke count (40-60 at scroll distance, 60-80 at push-in). Every fold, every hair element, every gesture is fully realized. Summer is the visual peak of the game's rendering intensity — autumn and winter will deliberately recede from here.

#### Autumn — Adulthood (中年)

**The world**: Golden hour. Warm but transitory. The adult's eye — everything is connected to everything else. Memory is woven.

**Figure archetypes (1-2)**:
- **The Anchor**: A partner, a child, a life-defining relationship. The figure who gives adulthood its center of gravity. Rendered with accumulated ink (积墨) — layers built up over time. Silhouette motif: grounded, settled — this figure occupies space with weight and permanence. Color thread: Dusk Gold `#D4A96A` (the golden hour of life). Gesture motif: stillness — they are the harbor, not the ship.
- **The Echo**: A figure from an earlier season, returned changed. The childhood friend grown up. The parent now old. Rendered with dry brush entering their previous fingerprint — if they were wet-presence in spring, they are now mixed. Silhouette motif: their original motif, aged — the same posture with more weight, the same gesture slowed. Color thread: their original thread, desaturated 20-30%. Gesture motif: their original gesture, carrying more weight — the same hand movement, slower.

**Spirit in autumn**: Variable line — some sure, some hesitant. Contour re-opening. Interior = Ochre `#B0895C` at 15-25%. Dry brush at extremities. The spirit is an adult-spirit — carrying multiple temperatures, aware of endings.

**Autumn detail note**: Stroke count reduces 20-30% from summer. Garment folds are suggested, not detailed. Background elements are more present than in spring, but rendered with economy. The detail is concentrated in the faces and hands of significant figures — these are the instruments of connection. Autumn is about what is earned, not what is displayed.

#### Winter — Twilight (暮年)

**The world**: Dusk fading to night. Cool, vast. The twilight eye — memory is pinpricks of light in a dark field. What was everything is now far away.

**Figure archetypes (1-2)**:
- **The Last Connection**: A final relationship, a caregiver, a fellow traveler at the end. Rendered with dry brush — the brush is running out of ink, and every stroke is precious because it might be the last. Silhouette motif: close, intimate — this figure occupies personal space. Color thread: Lingering Light `#E8C080` — the smallest, warmest accent in the game. Occupies ≤3% of the figure's area. Gesture motif: touch — a hand on a shoulder, fingers intertwined, heads close together.
- **The Self**: The spirit's own younger reflection — a mirror-figure of the spirit at an earlier season. Rendered as the spirit was in that season, but seen through winter's desaturation. This figure is optional — it may not appear, or it may appear only in specific choice paths. Silhouette motif: identical to the spirit's silhouette from the reflected season. Color thread: the spirit's interior wash color from that season, at 30-40% opacity. Gesture motif: the spirit's own drift lean, reversed — looking back at itself.

**Spirit in winter**: Thinnest, most broken line. Most open contour. Interior near-transparent. Eye dots faintest. The spirit is a twilight-spirit — almost gone, almost at peace.

**Winter detail note**: Stroke count is at the game's minimum for significant figures (15-25 at scroll distance). Everything is suggested, nothing is insisted upon. The warm pinpoints carry all the emotional weight. A winter figure is defined by what ISN'T rendered — the negative space around them IS their presence. Background elements are near-monochrome, 3-5 strokes maximum. Winter is about the beauty of letting go.

#### Cross-Chapter Figure Evolution

When a figure appears in multiple chapters, their visual signature must evolve while remaining recognizable. This is the hardest character design problem in the game — and the most important.

| Signature Component | How It Evolves | How It Persists |
|---|---|---|
| **Silhouette motif** | Gains weight, changes posture with age. A figure who stood straight in youth may stoop in age. A figure who was small and kinetic in childhood may be tall and still in adulthood. | The core shape DNA survives. A figure with a distinctive shoulder slope keeps that slope. A figure with an asymmetrical garment keeps that asymmetry. The silhouette ages; it does not transform. |
| **Brush-quality fingerprint** | Evolves with the season's dominant brush mode. A wet-presence figure in spring gains dry-brush edges in autumn. A firm-presence figure in summer softens at the edges in winter. | The fingerprint's *center* stays identifiable. The wet figure is always wetter at the core than the season demands. The dry figure is always drier. The fingerprint shifts 20-30% toward the seasonal norm, not 100%. |
| **Color thread** | Desaturates and shifts temperature with the season. Sakura Pink in spring becomes a muted rose in autumn. Vermilion in summer becomes Maple Red `#B5412C` in autumn. Azure becomes Slate Blue `#6B7B8A` in winter. | The hue family persists. The thread occupies the same 3-8% area. The placement on the figure (garment edge, accessory, hair element) stays consistent. |
| **Gesture motif** | Slows. The same gesture, aged. The quick turn of youth becomes a deliberate turn in adulthood. The open arms of the nurturer become trembling hands. | The gesture's *shape* is recognizable across decades. A figure who always holds one hand behind their back does so at age 8 and age 80. |

**Evolution design test**: Show a figure at age 20 (summer) and age 60 (winter) side by side. A playtester who has spent time with the summer version must identify the winter version as the same person within 5 seconds. If they cannot, the signature persistence has failed.

---

### 5.6 Connection to Pillars

| Pillar | Character Design Mechanism | Where Defined |
|---|---|---|
| **1: 选择即重写** (Choices Rewrite) | Spirit's interior wash shifts with choice outcomes — preserved = warm bloom, lost = cool fade, transformed = hue shift, bittersweet = split temperature. Peak emotion moments (loss, reunion) permanently alter the color temperature of all subsequent appearances of the affected figure. | 5.1.5 Emotional Proximity; 5.3.4 Peak Emotion; Section 4.7 Choice Feedback |
| **2: 不完美才是力量** (Imperfection is Power) | Every figure is rendered with visible brush imperfection — variable line weight, ink-bleed, dry-brush texture. The unclosed contour of the spirit is the game's central visual statement of incompleteness-as-beauty. Loss-path figures receive MORE expressive brushwork (broken ink, dry brush), not less — the broken path is visually richer. Minimum-viable people are beautiful because of what the brush leaves out. | 5.1.1 Core Archetype; 5.3.2 Brush Quality; 5.3.4 Peak: Loss; 5.4.3 Minimum-Viable Person |
| **3: 关联的网络** (Association Web) | The visual signature system makes figures recognizable across memory fragments — the silhouette motif, brush fingerprint, color thread, and gesture motif are the associative links. Emotional proximity system guides the player toward anchors through warm/cool interior wash shifts — the spirit IS the association web's navigational compass. During memory transitions, the spirit's interior wash bridges departing and arriving memories. | 5.1.5 Emotional Proximity; 5.1.4 Memory Transition; 5.2.2 Visual Signature System; 5.5.5 Cross-Chapter Evolution |
| **4: 画卷中有呼吸** (Breathing Scrolls) | The spirit is always in motion (drift lean), always incomplete (unclosed contour) — it is the embodiment of the breathing scroll. Memory figures have subtle micro-animation (garment sway, breathing suggestion) — the 10% motion in the 90/10 stillness-to-motion ratio. Peak emotion moments are the "exhale" — brief intensification before return to contemplative stillness. | 5.1.1 Core Archetype; 5.1.2 Seasonal Variation (presence quality); 5.3.4 Peak Emotion recovery phases |

---

### 5.7 Design Tests

**Test 1 — Silhouette Identification (Cast Distinctiveness)**:
Create full-Sumi-ink silhouettes of all significant memory figures + the spirit. Present to 5 playtesters. Task: "Name each figure by silhouette alone." (Provide reference images of each figure first, then test with silhouettes.) Pass: 4/5 participants correctly identify every figure. If any figure is confused with another, their silhouette motif is not distinctive enough.

**Test 2 — Seasonal Spirit Readability**:
Present one spirit rendering from each season (4 total) in isolation — no background, no memory context, no color (grayscale). 5 participants. Task: "Identify which season each spirit belongs to AND rank them from youngest to oldest." Pass: 4/5 correctly identify all four seasons; 4/5 correctly rank youngest to oldest. This validates that line quality and contour openness alone communicate the spirit's seasonal state.

**Test 3 — Cross-Chapter Figure Recognition**:
Select one figure who appears in at least two seasons (e.g., summer and autumn). Present the figure as they appear in each season, side by side, with different memory contexts. 5 participants who have NOT seen the figure before. Task: "Are these the same person?" Pass: 4/5 say yes, correctly identifying at least two persistent signature elements (silhouette, color thread, gesture, or brush quality).

**Test 4 — Emotional Proximity Readability (No Text, No UI)**:
Record a 5-second clip of the spirit drifting from Distant to Adjacent proximity toward a warm anchor. Remove all text and UI. 5 participants. Task: "What is happening to the spirit emotionally?" Pass: 4/5 describe a shift toward warmth, connection, or interest. (Exact wording not required — the emotional direction must be correct.)

**Test 5 — Peak Emotion Identification**:
Create three 4-second clips: a Fight, a Loss, and a Reunion. Grayscale only (to isolate brush quality from color). 5 participants. Task: "Identify which is which." Pass: 4/5 correctly identify all three. If the brushwork alone cannot distinguish fight anger from reunion tenderness, the brush-quality shifts are not extreme enough.

**Test 6 — Minimum-Viable Person Threshold**:
Create a memory fragment with minimum-viable background figures. 5 participants. Task: "Point to all the people in this scene." Pass: 5/5 identify all minimum-viable figures as people. Then ask: "Describe any of these people individually." Pass: 0/5 can describe an individual — the figures are generic. This validates that minimum-viable = person-shape without individuality.

---

### 5.8 Asset Naming Convention (Character Assets)

All character assets follow: `char_[figure]_[season]_[distance]_[variant].[ext]`

| Element | Format | Examples |
|---|---|---|
| **Figure** | `spirit`, `fig01` through `fig06` (assigned by cast list) | `spirit`, `fig01`, `fig03` |
| **Season** | `spring`, `summer`, `autumn`, `winter`, `base` (for menu/transition) | `spring`, `winter` |
| **Distance** | `scroll` (exploration distance), `pushin` (choice moment distance) | `scroll`, `pushin` |
| **Variant** | `idle`, `drift`, `choice`, `loss`, `reunion`, `transition` | `idle`, `loss` |
| **Extension** | `.png` (still frames), `.apng` or sprite sheet for micro-animations | `.png` |

**Examples**:
- `char_spirit_spring_scroll_drift.png` — Spirit at exploration distance, spring, drifting
- `char_spirit_summer_pushin_choice.png` — Spirit at push-in distance, summer, during choice moment
- `char_fig01_autumn_scroll_idle.png` — Figure 1 at exploration distance, autumn, idle pose
- `char_fig03_winter_scroll_loss.png` — Figure 3 at exploration distance, winter, loss state
- `char_spirit_base_transition.png` — Spirit during memory transition (base palette, not seasonal)
```

---

This covers all seven areas you specified, plus asset naming. The section is designed to be read by a character artist with no additional briefing — every rule has a reason, every spec has a number, and every emotional target has a brush technique to deliver it.

---

## Section 6: Environment Design Language — 场景设计语言

**One-line rule**: The environment is not a backdrop -- it is the memory's body. The spirit drifts through landscapes that feel the same loss, the same warmth, the same fading that the figures do. Every tree, roof-line, and mountain ridge is painted with the same brush that paints the characters -- the world is alive with the same ink.

*Serves all four pillars. Direct spatial expression of Visual Principle 3 (留白即是空间) and Principle 2 (颜色即是情绪).*

---

### 6.1 Architectural Style -- Memory Built in Ink

#### Core Principle: Architecture as Recollection, Not Blueprint

Buildings in 回响 are **not constructed -- they are remembered**. A childhood home is not a measured floor plan; it is the feeling of the doorway, the slope of a roof against an evening sky, the particular warmth of one window. Architecture follows the same brush philosophy as characters: suggestion over description, spirit over likeness.

| Quality | Rule | Rationale |
|---|---|---|
| **Structural suggestion** | Buildings are defined by 3-5 key strokes for overall form, not by counting beams and brackets. The roofline, one wall edge, and a doorway may be all that renders. | The memory supplies the rest. Over-rendering a building makes it a blueprint; under-rendering makes it a feeling. Aim for the feeling. |
| **No perspective grid** | Architecture uses atmospheric/isometric suggestion rather than strict one/two/three-point perspective. Lines may converge loosely but never mathematically. | Mathematical perspective belongs to photography, not to memory. A remembered room does not have vanishing points. |
| **Selected detail, not comprehensive detail** | One architectural element per building receives Hero-level stroke density (30+ strokes). Everything else is Supporting or Background. | The eye needs one anchor to believe the rest. A fully detailed bracket set under an eaves with everything else suggested = the player reads "traditional building." A half-detailed everything = the player reads nothing. |
| **Inhabited silence** | Interiors feel lived-in even when empty. A single cup on a table, a half-open screen, a cushion slightly askew. One or two occupancy markers per interior space -- never more. | The memory is of people who are no longer there. The objects they left behind carry the emotional weight. |

#### Architectural Vocabulary

回响 draws from the East Asian architectural tradition -- curved rooflines, wooden post-and-beam structure, raised platforms, screen walls, courtyard organization, garden integration. But every element is filtered through the memory-lens.

| Architectural Element | Memory Rendering | Purpose |
|---|---|---|
| **Rooflines** | Sweeping curves rendered in 3-5 confident brushstrokes. The curve is slightly exaggerated -- memory makes roofs more elegant than they were. Eaves may dissolve into ink-wash at the edges. | The roofline is the building's silhouette. It must read against sky or mountain in a single gestural line. |
| **Walls and screens** | Suggested as light ink wash planes with 2-3 vertical/horizontal structural lines. Rice paper screens are the lightest wash in the scene (near 宣纸 `#F5F0E8`); wooden walls carry more 墨 `#2C2416` weight. | Screens = permeability, the boundary between inside and outside. Walls = enclosure, protection. The brush weight difference communicates this without explanation. |
| **Doors and thresholds** | Rendered with the most complete strokes in any architectural scene (near-figure-level detail). The threshold between spaces is emotionally significant -- entering, leaving, crossing over. | Thresholds are the architectural equivalent of choice anchors. The player passes through them; the brush lingers on them. |
| **Courtyards and negative space** | Defined by what surrounds them (roof edges, wall lines, a single tree) rather than by floor planes. The courtyard itself is pure 留白 -- Rice Paper `#F5F0E8` with perhaps one fallen leaf or one stone. | Courtyards are the architectural expression of 留白 -- the empty center that holds meaning. |
| **Interior objects** | 1-3 objects per room, rendered at Supporting stroke density (8-20 strokes). A scroll on a desk, a tea set, a folded garment. Everything else is atmosphere -- suggested by the room's shape and the light within it. | Objects carry the ghost of occupancy. Too many objects = clutter. Too few = abandonment. One to three = someone was just here. |
| **Stairs and paths** | Suggested as directional brushstrokes -- a series of horizontal marks receding into atmospheric wash. Never fully rendered. Stairs are a gesture: "upward travel happened here." | Paths are invitation. The brush points the way; the player's drift follows. |

#### Per-Chapter Architectural Character

| Chapter / Season | Architectural Register | Tang Vocabulary & Elements | Brush Treatment |
|---|---|---|---|
| **Spring -- Childhood** | The intimate Tang home. Domestic interiors at child's-eye scale. Low sightlines; rooms feel enclosing but not confining. Garden always glimpsed through an opening -- the world beyond is close. | 榻 (platform bed), 案几 (low table), 窗棂 (lattice window), 屏风 (standing screen). Warm wood tones; rice-paper surfaces. Ceiling beams low enough to touch. | Wet wash (湿染). Soft bleeding edges at wood grain, at screen paper edges. Warm undertone: 樜色 carried in wood warmth. Architecture at 3-8 strokes per element -- the child does not parse structure, only feeling. |
| **Summer -- Youth** | The architecture of presence. Grander spaces: halls (堂), elevated pavilions, covered walkways (廊). Courtyards open to sky. Architectural ambition is visible -- the world is opening up, and structure is confident. | 斗拱 (bracket sets), 飛檐 (flying eaves with pronounced upward curve), 堂 (ceremonial hall), 廊 (roofed walkway). Timber frame visible and celebrated. | Firm brush (正锋). Confident strokes, clean line, minimal bleed. Post-and-beam legible at 12-18 strokes. Geometric accent (the 30%) given its most prominent expression in the game -- bracket sets are visible structural rhythm. |
| **Autumn -- Adulthood** | The architecture of reflection. Settled spaces: verandas with views (轩), upper chambers (阁), garden pavilions (亭), walled courtyards (院). Architecture shows habitation -- wood darkened by touch, stone worn by passage. | 轩 (veranda with a view -- the scholar's perch), 阁 (upper chamber for quiet), 亭 (pause-pavilion), 院 (courtyard as world). Golden hour light becomes architectural material -- it enters low and horizontal, defining space. | Mixed brush: wet core, dry edges (枯笔 at beam ends, at tile edges). Accumulated ink (积墨) for surfaces that hold history -- the table edge touched thousands of times, the threshold stepped over for decades. |
| **Winter -- Twilight** | The architecture of departure. Spaces dissolve. What remains: a single gate (門), a last wall (壁), a distant gate tower (阙) -- architecture reduced to threshold and boundary. The structure recedes; the memory of structure persists. | 門 (gate -- singular, final), 壁 (wall -- the last boundary between presence and absence), 阙 (gate tower -- seen from far away, no longer approached). | Dry brush (枯笔) dominant. Flying white (飛白) where forms break. Architecture at 3-5 strokes per element -- the game's minimum. Forms dissolve into ink wash at edges. The negative space IS the architecture now. |

**Architectural style rule**: A building in any chapter must read as "East Asian traditional architecture seen through memory" within 1 second. If the player has to study the roofline to understand what culture they are in, the architectural language is too generic.

---

### 6.2 Natural Elements -- The Scroll Canvas

#### Core Principle: Nature Is the World the Spirit Inhabits

Most memory fragments take place in or at the edge of natural landscapes. The built environment (Section 6.1) is an island within the natural world. Nature is rendered as a scroll painting -- layered, atmospheric, built from brush techniques rather than polygon counts.

#### Element-by-Element Rendering

##### Mountains and Distant Forms

| Quality | Specification |
|---|---|
| **Technique** | 3-5 horizontal bands of ink wash at decreasing opacity and saturation. Each band = one mountain ridge receding into atmospheric distance. The nearest band resolves into a few tree-dot accents and a ridge contour line. The farthest band is barely visible -- 5-10% opacity. |
| **Brush mode** | Wet wash (湿染) with accumulation at ridge lines. The peak/ridge carries the most ink; the slope washes down into nothing. |
| **Seasonal variation** | Spring: soft green-gray washes. Summer: deeper blue-green, higher contrast between bands. Autumn: warm brown-gray washes with gold-dot accents (distant trees turning). Winter: near-monochrome gray-blue, lowest contrast. |
| **Strokes per band** | 2-4. Mountains are Background in the visual hierarchy. |
| **Role in composition** | Mountains establish depth, season, and the vastness of the world beyond the memory. They are always in the background layer. |

**Design test**: A mountain ridge must read its season from color alone, in isolation, at 100x100px thumbnail.

##### Water

| Quality | Specification |
|---|---|
| **Technique** | Horizontal negative space (留白) bounded by subtle ink lines at shores/edges. Water is defined by what it reflects and what disturbs it, not by painting the water itself. Still water = pure negative space. Moving water = 2-4 horizontal broken-ink strokes suggesting current. |
| **Brush mode** | Still: No strokes -- water IS negative space. Rippled: 1-2 thin horizontal lines in Light Ink Wash `#8A8078`. Flowing: 3-5 directional strokes with flying white (飞白) for surface texture. |
| **Reflections** | Significant objects (the spirit, memory figures, a bridge, a moon) cast "ink-bleed reflections" -- their form repeated in the water below as a softer, more dissolved version, offset by 2-5% vertically. Reflections are 30-40% the opacity of the source object. Color is the source color desaturated 20%. |
| **Seasonal variation** | Spring: water carries a faint warm tint (Peach Blossom `#F0CFB0` at 5% wash). Summer: water may carry vivid reflection color (Vermilion, Azure). Autumn: water is still, reflective -- the golden hour light on water is Dusk Gold `#D4A96A` reflection. Winter: water is frozen or near-frozen -- flat, gray, motionless. May carry isolated warm reflections from distant lit windows. |
| **Emotional role** | Still water = contemplation, reflection, the passage of time suspended. Flowing water = time passing, things carried away. Frozen water = endings, stillness, the final state. |

**Water rule**: If the player can see the "paint" that makes the water (as opposed to the paint that makes the things on/in/around the water), the water is over-rendered.

##### Trees and Vegetation

| Quality | Specification |
|---|---|
| **Technique** | Trees are defined by trunk contour (3-5 strokes, variable weight, dry brush for bark texture) and canopy suggestion (5-15 strokes of directional leaf-cluster marks). Individual leaves are never rendered -- the canopy is a collection of brush-dab clusters. |
| **Tree hierarchy** | **Hero tree** (30-40 strokes): A significant tree that appears in multiple memories -- a childhood climbing tree, a tree under which something important happened. Fully realized trunk, branch structure, canopy texture. May carry a color thread (Section 5.2.2 Component 3) if emotionally significant. **Context trees** (10-15 strokes): Trunk + canopy clusters. Read as species (pine vs. broadleaf) but not as individuals. **Atmosphere trees** (3-6 strokes): Vertical brush mark + one canopy dab. Background layer. |
| **Seasonal variation** | Spring: Soft green washes (`#C5D8B5`), blossoms as scattered pink dots (`#F4C2C2`). Branches are thin, new. Summer: Full, dense canopies in Verdant Green `#4A8C3F`. Maximum stroke density. Autumn: Canopies in Maple Red `#B5412C` and Dusk Gold `#D4A96A`. Some trees partially bare -- branch structure exposed. Falling leaves: 3-8 individual leaf strokes drifting downward. Winter: Bare branches -- the tree is reduced to its structural linework. Trunk and branch contours only, in dry brush. 5-10 strokes total. |
| **Undergrowth** | Sparse. 1-3 grass-cluster strokes at the base of trees or along path edges. Undergrowth is never a solid mass -- it is accent marks. |

**Tree rule**: A tree in 回响 is a brush painting of a tree, not a simulation of a tree. Its canopy is a gesture, not a volume calculation. If the player can count the leaves, there are too many leaves.

##### Sky and Atmosphere

| Quality | Specification |
|---|---|
| **Technique** | The sky is a single wash gradient from top to bottom or horizon to horizon. No clouds rendered as discrete objects -- atmosphere is suggested through wash density variation. |
| **Day sky** | Very light wash -- Rice Paper `#F5F0E8` at top fading to near-white at horizon. The sky is mostly negative space. |
| **Dusk/dawn sky** | Warm gradient: Dusk Gold `#D4A96A` at horizon fading to Pale Ink Blue `#B0C4D0` overhead. |
| **Night sky** | Deep Indigo `#3A3068` wash with negative-space stars (unpainted dots of Rice Paper showing through). |
| **Atmospheric haze** | A uniform Light Ink Wash `#8A8078` overlay at 5-15% opacity, applied evenly across distant elements. Haze increases with depth -- the farthest mountain ridge is 40-50% haze. |
| **Seasonal variation** | Spring: Hazy, diffused. Summer: Clear, high-contrast. Autumn: Golden haze at horizon. Winter: Low, heavy sky -- the wash gradient is compressed (less vertical range). |
| **Emotional role** | The sky is the memory's emotional ceiling. A low, heavy sky = emotional weight, enclosure, winter. A high, open sky = possibility, summer. The sky is the one environmental element that communicates emotional register independent of any object in the scene. |

##### Weather

Weather in 回响 is rare and significant. It never appears as "background atmosphere" -- weather always means something.

| Weather | When It Appears | Rendering | Emotional Meaning |
|---|---|---|---|
| **Rain** | Moments of sadness, cleansing, or release. 1-2 times per chapter maximum. | Thin diagonal strokes in Light Ink Wash `#8A8078`. 20-40 strokes across the frame. Sparse -- the rain is a veil, not a downpour. | Tears, washing away, things that cannot be held. |
| **Snow** | Winter chapter. Appears in 2-3 memories. | Individual white/rice-paper dots, 5-15 per frame, drifting at 0.5-1 degree angle. Each dot has a soft halo -- snow in memory is gentle, not harsh. | Silence, burial, the world being covered, endings that are peaceful. |
| **Mist/fog** | Memory transitions; moments of uncertainty; the edge of a chapter. | A uniform wash overlay at 20-40% opacity in the dominant atmospheric color. Detail fades from background forward -- the mist eats the world from the edges inward. | Uncertainty, liminality, the space between knowing and not-knowing. |
| **Wind** | Moments of change, arrival, departure. Never constant -- wind is an event. | Not rendered directly. Communicated through: tree canopy lean (2-5 degrees), garment edge lift on figures, 1-3 drifting elements (leaf, petal, dust, snow). The wind is the movement of things, not a visual effect. | Change, motion, things in transit, the breath of the scroll (Pillar 4). |

**Weather rule**: If weather appears and the player does not register it as emotionally significant, the weather should not have appeared. Weather is never decorative.

---

### 6.3 Texture Philosophy -- Stylized Brush, Not PBR

#### Core Decision: This Is a 2D Hand-Drawn Game

回响 is a 2D ink-painting game. The question of "PBR vs. stylized vs. painted textures" resolves to a single answer: **every surface in the game is a brushstroke on paper**. There are no 3D materials, no normal maps, no roughness/metallic channels, no lighting calculations that simulate physical light behavior.

#### Texture = Brush Technique

| Traditional Texture Concept | 回响 Equivalent | Implementation |
|---|---|---|
| **Diffuse / Albedo** | The base ink-wash color of a surface. | A flat or gradient-filled region with seasonal palette color. |
| **Roughness** | Brush texture -- smooth wet wash vs. textured dry brush. | Controlled through brush mode selection, not a roughness map. Wet wash surfaces appear "smooth" (even color field with soft edges). Dry brush surfaces appear "rough" (streaky, porous, paper grain visible). |
| **Normal / bump** | Not applicable. Depth is communicated through stroke layering and wash density, not simulated surface angle. | A mountain ridge is "bumpy" because the brush loaded and released ink unevenly along the ridge-line, not because a normal map deflects light. |
| **Metallic** | Not applicable. Metallic surfaces (gold accents, bronze vessels) are rendered with high-value warm brush strokes and small bright-highlight marks -- but these are painted, not calculated. | Gold = Dusk Gold `#D4A96A` with a few 1-2px white (Rice Paper) highlight marks at edges. |
| **Ambient occlusion** | Ink accumulation (积墨) -- darker ink layered in crevices, at joints, under eaves. Depth through darker wash, not through ambient occlusion calculation. | A corner where two walls meet = one additional layer of ink wash at 20-30% opacity, feathered outward. |
| **Subsurface scattering** | Wet wash bloom -- the soft, bleeding edge where ink spreads into damp paper. Used for translucent materials (paper screens, flower petals, spirit's interior). | A paper screen with light behind it = the wash color bleeding 3-5px beyond the screen's contour line, at reducing opacity. |

#### Why This Choice

1. **Visual coherence**: The entire game lives in one material world -- ink on paper. Introducing PBR surfaces would break the scroll illusion instantly. A photorealistic wooden beam next to a brushstroke figure destroys the "every frame is a painting" rule (Section 1).

2. **Performance**: 2D hand-drawn assets with simple parallax compositing require negligible GPU resources. No shader compilation, no material instances, no draw-call budgeting for surface properties. The entire rendering budget goes to more brushstrokes, not more surface calculations.

3. **Player expectation**: The player who chooses a game that looks like an ink scroll painting wants to be inside an ink scroll painting. Realistic surface response would satisfy an expectation no one brought to this game.

4. **Art pipeline simplicity**: One artist, one brush library, one set of seasonal palettes, one material philosophy. No importing Substance materials, no tweaking roughness values across 47 surfaces, no lighting-rebuild cycles. The artist paints surfaces; the surfaces are done.

#### Grain and Paper Texture

The one "texture" that exists across every surface in the game: the rice paper grain.

| Quality | Specification |
|---|---|
| **Paper texture** | A subtle, tileable noise pattern at 5-10% opacity overlaid on all non-pure-negative-space areas. Visible at push-in distance; subliminal at scroll distance. |
| **Texture source** | A high-resolution scan or procedural generation of handmade rice paper (xuan paper, 宣纸). Irregular fiber distribution. No repeating grid pattern. |
| **Application** | Applied as a global overlay in the final compositing pass. Not baked into individual assets -- this ensures consistency across all art and allows the paper to "receive" all brushstrokes uniformly. |
| **Seasonal variation** | None. The paper is the constant substrate. The ink changes; the paper does not. |
| **Technical note** | At 1080p, the paper grain should be 1-2px features. At 4K, 2-4px. The grain must never compete with brush detail -- it is felt, not seen. |

#### Material Distinctiveness Through Brush, Not Surface

Different materials in the world are distinguished by HOW they are painted, not by simulation of material properties.

| Material | Brush Signature | Example |
|---|---|---|
| **Wood** | Dry brush (枯笔), directional strokes following grain. Darker ink accumulation at joints and edges. | A wooden beam: 3-5 directional strokes, streaky texture, darker at the end-grain. |
| **Stone** | Short, irregular brush dabs -- accumulated ink (积墨). Variable opacity within each dab. No directional grain. | A stone path: 8-15 irregular dark marks on a light wash base, uneven spacing. |
| **Fabric / cloth** | Soft wet wash with 2-3 fold lines in darker ink. Fold lines follow gravity and garment drape. | A hanging scroll or curtain: vertical wash with 2-3 thin vertical fold marks. |
| **Paper / screen** | Lightest wash, near Rice Paper `#F5F0E8`. Edge defined by 1-2 thin lines. Interior may show faint shadow-suggestion of what is behind the screen. | A shoji screen: near-white rectangle with thin wood-frame lines and a faint silhouette of a figure or object behind it. |
| **Metal / bronze** | Firm brush (正锋), clean edges, one bright accent mark (painted highlight, not calculated specular). | A bronze mirror or vessel: warm-brown wash, clean contour, single Rice Paper highlight dot at the highest point. |
| **Ceramic / porcelain** | Smooth wet wash, even color field, clean contour. May have 1-2 decorative brush marks (a painted pattern on a vase). | A tea cup: even wash, clean outline, small painted motif. |
| **Water** | Negative space + reflection + edge lines. (See 6.2 Water.) | — |
| **Flesh / skin** | Soft wet wash, warm tone, no internal linework except at significant contours (jaw, hand edge). | Memory figure's face: warm wash, single contour line at profile edge, eye/mouth suggestion marks. |

**Material rule**: A material is defined by the brush technique used to paint it. If two materials require the same brush technique, they are visually the same material -- and that is intentional. This game does not need 14 distinct surface types. Wood, stone, fabric, paper, and water cover 95% of all environmental surfaces.

---

### 6.4 Prop Density Rules -- Economy of the Brush

#### Core Principle: Density Follows Emotional Weight

Props (discrete environmental objects that are not architecture, landscape, or figures) carry disproportionate storytelling weight. A single object in a room speaks louder than ten objects. Density is governed by three factors: area type, narrative significance, and player choice state.

#### Density Tiers

| Tier | Props per Screen | Total Strokes (props only) | When Used |
|---|---|---|---|
| **Sparse** | 1-3 | 8-30 | Most memory fragments. The default. |
| **Standard** | 4-7 | 40-80 | Interior scenes with specific narrative focus; a room where something important happened. |
| **Rich** | 8-12 | 80-150 | One or two scenes per chapter -- the emotional climax room, the place the memory is "about." |
| **Overgrown** | 15+ | 150+ | Never. This tier does not exist in 回响. |

#### Area Type Density Mapping

| Area Type | Default Tier | Density Driver | What Props Do |
|---|---|---|---|
| **Natural landscape** | Sparse | 1-2 path markers (stone, fallen branch, bridge suggestion) + 0-1 significant object (a single lantern, a boat, a shrine marker). | Props in nature are waypoints -- they guide drift direction or mark memory anchors. |
| **Memory fragment -- exterior** | Sparse to Standard | 1-3 significant objects associated with the memory (a child's toy in a courtyard, a letter on a bench, a tool left mid-task). | Exterior props are evidence of interrupted life. Someone was here; they left this; now it is a memory. |
| **Memory fragment -- interior (general)** | Standard | 3-6 objects that define the room's function (a desk with writing implements, a tea set, a folded garment, a scroll, a lamp). | Interior props establish "what kind of room this is" and "what was happening here." |
| **Memory fragment -- interior (climax)** | Rich | 5-8 objects, of which 2-3 are Hero-level (30+ strokes). The rest are Supporting. | Every object in the climax room carries emotional significance. No decoration. |
| **Memory transition** | Sparse (dissolving) | 0-2 partial objects -- props from the departing memory that have not yet fully faded. | Transition props are echoes, not objects. They dissolve as the transition progresses. |
| **Main menu** | Sparse | 0 prop objects. The menu is the scroll, the title, and the afterimage figures. | Props would anchor the menu in a specific memory. The menu must belong to all memories and none. |

#### Prop Detail Level by Narrative Significance

| Prop Significance | Stroke Count | Detail Treatment | Example |
|---|---|---|---|
| **Anchor prop** (interactive, choice-relevant) | 25-40 strokes. Hero hierarchy. | Fully realized. Brush technique appropriate to material. Color thread present if emotionally significant. May have subtle micro-animation (a candle flame, ink drying on a letter). | The letter that holds a pivotal childhood memory -- every fold, every ink character, the seal, the paper edge. |
| **Story prop** (non-interactive, narrative support) | 10-20 strokes. Supporting hierarchy. | Clearly readable as an object. Material suggested but not fully detailed. No micro-animation. | A tea set on a table -- recognizable cups and pot, but not every decorative detail. |
| **Atmosphere prop** (environmental context) | 3-8 strokes. Background hierarchy. | The idea of the object. A shape that reads as "a cup," "a book," "a cushion." No material detail. No internal linework. | A distant shelf with objects -- you know they are objects, but you do not know which objects. |
| **Choice-modified prop** (changed by player choice) | Variable -- starts at story or anchor level, shifts with choice outcome. | Must be visually identical to its pre-choice state in all qualities EXCEPT the ones the choice changes. (See 6.5 Environmental Storytelling.) | A letter that was whole, now torn. The tear is rendered in 3-5 additional broken-ink strokes. |

#### Prop Placement Rules

1. **One hero per frame**: If a frame contains more than one Anchor-level prop, one must be visually dominant (larger, more central, warmer color). The eye cannot negotiate between two equal anchors.

2. **Props cluster near memory anchors**: Props do not distribute evenly across the frame. They cluster near significant figures, thresholds, and choice objects. The empty parts of the frame are truly empty.

3. **Props obey the 留白 budget**: In any frame, the combined area of all props must not exceed 25% of total frame area. (Architecture may occupy another 20-30%. Figures may occupy another 10-15%. The remaining 30-45% is negative space.)

4. **No two props share the same visual weight**: If two props have equal stroke density, equal size, and equal color saturation, one must be reduced or one must be elevated. Equal weight = visual indecision.

5. **A prop that appears in multiple memories is a recurring anchor**: If the same tea cup appears in spring (childhood) and autumn (adulthood), it is not just a tea cup -- it is a memory thread. Its rendering ages between appearances: in spring it is new, with clean edges; in autumn it carries a chip (one dry-brush mark), a stain (one darker ink bloom). The prop, like the figures, remembers.

---

### 6.5 Environmental Storytelling Guidelines

#### Core Principle: The World Remembers What Happened

Environmental storytelling in 回响 operates at four levels:

1. **The inherent story** -- what the environment communicates about the memory on first viewing (who lived here, what happened, what was felt).
2. **The choice story** -- how the environment changes AFTER the player makes a choice (what was preserved, what was lost, what was transformed).
3. **The accumulation story** -- how the environment carries forward changes from earlier chapters (the childhood home revisited in adulthood).
4. **The brush story** -- what the rendering itself communicates (age, emotional temperature, significance) independent of depicted content.

#### Level 1: Inherent Story -- What the Environment Says on Arrival

Before the player interacts with anything, before any figure speaks or any text appears, the environment must answer these questions through visual detail alone:

| Question | Visual Answer Mechanism | Example |
|---|---|---|
| **Who was here?** | Personal objects at 1-3 locations. The objects are specific to a person -- a toy (child), a tool (worker), a letter (lover), a medicine cup (elder). | A child's room: a small garment draped over a screen, a toy on the floor, a low table with a half-finished ink painting. |
| **What was happening?** | Objects in mid-action -- a cup tipped over, a scroll unrolled mid-sentence, a door left ajar, a fire burned down to embers. Interrupted action reads as "this memory is of a moment that mattered." | A study: ink stone still wet (one dark wash bloom), brush resting on the ink stone (not in its holder), chair pushed back (not tucked in). Someone was writing and left suddenly. |
| **What was felt?** | Color temperature and brush quality of the environment. Warm washes = comfort, safety, love. Cool washes = distance, loss, isolation. Dry brush = age, neglect, things fading. Wet brush = immediacy, presence, emotional fullness. | A warm room: dominant wash in Sakura Pink `#F4C2C2` or Dusk Gold `#D4A96A`, soft edges, wet brush on significant objects. A cold room: dominant wash in Slate Blue `#6B7B8A`, dry brush edges, objects rendered with fewer strokes. |
| **When did this happen?** | Seasonal palette (Section 4) applied to all environmental elements -- trees, light quality, atmospheric haze, sky color. | The same room in spring (pink-gold light, cherry blossom visible through window) vs. winter (gray-blue light, bare branches, frost-white edge on the window frame). |
| **Is this a happy memory or a sad memory?** | Warm-to-cool ratio. A happy memory: 60%+ warm domain. A sad memory: 60%+ cool domain. A complex memory: 40-60% warm, with warm concentrated on one significant object and cool everywhere else. | A complex memory: a warmly-lit desk (Dusk Gold `#D4A96A`) where a letter was written, in a room that is otherwise Slate Blue `#6B7B8A` and Deep Indigo -- the one warm spot is what the memory is about. |

**Inherent story design test**: Show a new memory fragment to a playtester for 3 seconds, then hide it. Ask: "Who was here, what were they doing, and how did they feel?" The playtester should get at least 2 of 3 correct without any text, dialogue, or character animation.

#### Level 2: Choice Story -- How the Environment Responds to Player Choices

When the player makes a choice (preserve, let go, transform, or split/bittersweet -- see Section 4.7), the environment changes to reflect the outcome. This is the most important environmental storytelling mechanism in the game.

| Choice Outcome | Environmental Change | Visual Signature | Duration |
|---|---|---|---|
| **Preserved** | The anchor object and its immediate surroundings warm and saturate. Adjacent props gain definition (stroke count increases 2-4 on nearby objects). The room's overall warm domain expands 5-10%. | Warmth radiates from the anchor in a soft ink-bleed wash. The anchor appears more "present" -- sharper edges, deeper color. Nearby objects "wake up" slightly. | 2-4 seconds. |
| **Lost / Let Go** | The anchor object desaturates and cools. Adjacent props lose definition (stroke count decreases 2-4 on nearby objects -- strokes are visually removed or fade to Light Ink Wash `#8A8078`). The room's overall cool domain expands 10-15%. | The anchor appears to recede -- edges soften, color drains toward the base palette. Nearby objects become less distinct, as if the memory is losing interest in them. Gaps open in the environment (a screen that was closed is now partially open to emptiness; a wall that was solid now shows a crack of negative space). | 3-5 seconds. |
| **Transformed** | The anchor object shifts hue while maintaining most of its stroke density. The environmental palette adjusts to accommodate the new hue -- if the anchor shifts from warm to cool, the room rebalances. | The anchor undergoes a "color bloom" -- original color drains from edges inward while new color fills from the center outward. Adjacent objects may shift 5-10 degrees in the same direction as the anchor, subtly re-contextualizing. | 3-5 seconds. |
| **Bittersweet** | The anchor retains warm color at its core but develops cool edges. The environment splits -- the side of the frame nearest the anchor stays warm; the far side of the frame shifts cool. A temperature gradient forms across the composition. | Warm-to-cool gradient across the entire frame. The anchor is the warm pole; the farthest edge is the cool pole. This is the only outcome where the environmental change is spatial rather than uniform. | 4-6 seconds. |

**Choice story persistence**: Environmental changes from choices persist if the player returns to the same memory fragment. A preserved room stays warm. A room where something was lost stays cool and sparse. The world accumulates the player's choices.

**Choice story propagation**: When the player moves to the NEXT memory fragment after a significant choice, the new fragment's initial environmental state carries a slight echo of the previous choice -- a 5-10% temperature or saturation shift that fades over the first 5-10 seconds in the new memory. This is the "emotional residue" of association. The player may not consciously notice it, but the emotional continuity between memories is maintained.

**Choice story design test**: After a lost/let-go choice, show the same memory fragment. A playtester who saw the "before" must identify, without prompting, that something is different. "It feels colder" or "it seems emptier" is a pass. "I don't see any difference" is a fail.

#### Level 3: Accumulation Story -- Cross-Chapter Environmental Continuity

When a location reappears in a later chapter (the childhood home in adulthood, the youthful meeting-place in twilight), the environment carries the marks of time and the player's accumulated choices.

| Environmental Element | How It Ages / Accumulates | Tracking Requirement |
|---|---|---|
| **Architecture** | Gains weathering marks: additional dry-brush lines at edges, slight asymmetry (a settled roof beam), one new crack or repair mark per chapter. Age is layered, not decrepit. | Track which architectural elements the player spent time near (proximity >30s cumulative). Those elements weather more visibly -- the player's attention leaves marks on the world. |
| **Props** | Recurring props age between appearances (see 6.4, Prop Placement Rule 5). Props the player preserved gain a subtle warm halo (1-2px soft edge bloom) in later appearances. Props the player let go are less defined or absent. | Track each recurring prop's choice state per appearance. If a prop was let go and later "should" appear, it appears as a dissolving version (3-6 strokes, fading edges). |
| **Vegetation** | Trees that were saplings in spring are mature in autumn. A tree the player spent time near may carry a single color accent (one blossom cluster, one gold leaf) that the others do not. | Track hero trees and player proximity. |
| **Light quality** | The room's base light temperature shifts 5-10 degrees cooler per chapter, independent of season. This is the natural cooling of memory over time -- the same room in autumn is intrinsically cooler than in spring, even accounting for seasonal palette differences. | Global per-location tracking. |
| **Negative space** | Negative space increases across chapters. A room that was 35% negative space in spring is 45% negative space in winter. The world literally empties as the spirit approaches the end. This is separate from and additional to the seasonal reduction in stroke density. | Per-location: negative space ratio must increase ≥3% per subsequent chapter appearance. |

**Accumulation story rule**: A returning player who encounters a revisited location must feel the weight of time without being told. They should think "this place is older now" or "something is missing here" -- the environment tells the story of what happened between visits.

#### Level 4: Brush Story -- What the Rendering Communicates

Independent of depicted content, the brush itself tells a story. This is the meta-layer of environmental storytelling -- the HOW of rendering communicates meaning before the WHAT.

| Brush Quality | Environmental Meaning |
|---|---|
| **Wet wash dominant** | The memory is emotionally present, close, warm. The spirit is near the emotional center of this moment. |
| **Dry brush dominant** | The memory is distant, aged, or painful. The spirit is holding this at arm's length. |
| **Flying white streaks** | The memory is in motion -- something is happening, changing, or about to change. |
| **Broken ink blooms** | Emotional intensity has broken through the composed surface of the memory. Something here still hurts (or still burns). |
| **Accumulated ink layers** | This is a memory the spirit has returned to many times. It is built up, layered, heavy with revisiting. |
| **Firm, clean strokes** | This memory is clear and certain. The spirit knows exactly what happened here. |
| **Hesitant, broken strokes** | This memory is uncertain, fragmented, or being actively forgotten. The spirit is unsure. |

**Brush story rule**: The brush quality of the environment must match the emotional register of the memory. A warm childhood memory rendered in dry, hesitant brush is a failure of brush-story. A regret memory rendered in lush wet wash is a failure of brush-story. The brush does not lie.

---

### 6.6 Per-Season Environmental Character

#### Core Principle: The Same Place, Four Different Truths

A recurring location (a home, a garden, a street, a room) transformed by seasonal palette, light quality, vegetation state, atmospheric character, and brush mode. The following defines how each environmental category behaves in each season.

#### Master Transformation Table

| Environmental Element | Spring (Childhood) | Summer (Youth) | Autumn (Adulthood) | Winter (Twilight) |
|---|---|---|---|---|
| **Light quality** | Diffuse morning. Soft ambient. No single source. | Direct midday. Strong directional. Sharp shadows. | Golden hour. Low angle. Long soft shadows. | Dusk into night. Point-source. Isolated warmth in darkness. |
| **Atmospheric haze** | High -- 20-30% wash overlay. The world is soft, in the process of forming. | Low -- 5-10%. The world is clear, defined, confident. | Medium -- 15-20%. Golden haze at horizon. The world is warm but distant. | Medium-high -- 25-35%. Cool gray haze. The world is receding. |
| **Vegetation state** | Budding, blossoming. New leaves (small stroke clusters). Cherry/plum blossom dots. | Full, dense, saturated. Maximum canopy stroke density. Deep shadows under trees. | Turning, falling. Warm colors. Partial bare branches. Scattered falling leaves (3-8 per frame). | Bare. Branch structure only. Frost-white edge accents. No leaves. |
| **Water state** | Still to gently rippling. Warm-tinted. Reflective. | Moving, vivid reflections. Deep blue-green color. | Still, mirror-like. Golden reflections. The most beautiful water in the game. | Still or frozen. Gray. May carry isolated warm reflections (a distant lit window). |
| **Sky character** | High and hazy. Pale blue to pink at horizon. | High and clear. Deep azure (`#3B7CB8`). Strong gradient. | Low golden horizon. Warm gradient from gold to slate. | Low and heavy. Compressed gradient. Near-monochrome cool gray to deep indigo. Stars possible (unpainted dots). |
| **Architecture feel** | Large, enclosing, warm. Interiors dominant. | Open, expansive. Verandas, views outward. | Layered, accumulated. Repairs visible. History in the walls. | Distant, receding. One warm room. Everything else is far away and cold. |
| **Prop character** | Few, large, emotionally simple. A toy. A blanket. A bowl. | Many, detailed, expressive. Tools of passion and ambition. Letters, instruments, maps. | Fewer but heavier. Objects that have been kept. A worn tool. A photograph-like object. A ring. A key. | Fewest -- 1-3 objects, each one precious. A single cup. A single letter. A single light. |
| **Negative space ratio** | 25-35% (lowest -- childhood feels full). | 30-40%. | 35-45%. | 45-60% (highest -- twilight is mostly empty). |

#### The Same Location Across Seasons: An Example

A garden that appears in three chapters -- childhood, youth, and twilight.

| Element | Spring (Childhood) | Summer (Youth) | Winter (Twilight) |
|---|---|---|---|
| **Overall impression** | A safe, enclosed world. The garden is the whole universe. | A beautiful place that opens to a larger world beyond. | A memory of a garden, seen from far away. |
| **Trees** | Cherry tree in full blossom. Soft pink dot clusters (`#F4C2C2`). Leaves are small, new, tentative. | The same cherry tree, now mature green canopy. Dense. Deep shade beneath. | The same cherry tree, bare branches. Frost-white on bark. No blossoms. One or two desaturated pink dots remain -- the memory of blossoms. |
| **Path** | A few flat stones, widely spaced. Suggests a path without defining one. | A defined stone path with moss accents at the edges. The path leads to a gate -- the world beyond. | The path is mostly gone. 1-2 stones visible. The rest is snow (negative space with frost-white edge marks). |
| **Water feature** | A small pond, still water. One or two koi suggested as orange-red dabs. | The same pond, now with a small waterfall or stream. Water is moving, vibrant. Reflections sharp. | Frozen over. Flat gray surface. The fish are gone. |
| **Props** | A child's toy boat at the pond's edge (5-8 strokes). A single lantern, not yet lit. | A bench (Supporting detail). A letter left on the bench (Anchor). The lantern is lit. | Nothing. The bench is a snow-covered shape (3 strokes). The lantern is a dark silhouette. The space where objects were is as important as the objects that remain. |
| **Sky** | High, hazy, pale blue-pink. | Clear, deep azure. | Low, gray, heavy. One star (unpainted dot of Rice Paper) above the bare cherry tree. |
| **Emotional register** | Wonder, safety, the world is small and perfect. | Possibility, connection, the world is opening. | Distance, peace, the world is letting go. |

**Multi-appearance location rule**: A location that appears in multiple chapters must change enough to feel like "the same place, aged" but NOT so much that it feels like a different place. The cherry tree is the anchor of continuity. If the player cannot identify that the winter garden IS the spring garden (recognizing the tree, the pond shape, the stone arrangement), the continuity has failed.

#### Example 2: Tang Dynasty Interior -- 唐朝书房 (The Study)

| Season | Furniture & Objects | Screens & Openings | Decorative Elements | Atmosphere/Light | Color Thread | Emotional Register |
|---|---|---|---|---|---|---|
| **Spring** | Low table (案几) with rounded edges, 3-5 wet-wash strokes. Platform bed (榻) with soft cushions -- wood carries 樜色 undertone. Objects sparse: one brush, one inkstone. | Standing screen (屏风) half-unfolded -- garden light bleeds through rice paper. Lattice windows (窗棂) open; branches with new buds visible beyond. | Single calligraphy scroll, ink still wet-feeling. Small celadon vessel with one blossom. Incense burner (香炉) with thin, barely-visible smoke thread -- the 10% micro-animation. | Diffuse morning light through paper windows. Dust motes suspended in sunbeams (subtle drift loop). Low contrast; ambient glow from within. | 樜色 #F4C2C2 -- in the blossom, the cushion fabric, the morning light's blush on wood. | Safe enclosure. The room as nest -- the child's world is small, warm, known. |
| **Summer** | Table holds more: books, brushes, a bronze mirror catching light. Platform bed with bolder fabric. Furniture lines firmer -- confident 8-12 strokes per object. No soft bleed. | Screen fully unfolded, depicting a mountain landscape in firm brush. Lattice windows wide open; garden at full green saturation. Strong direct light enters. | Multiple scrolls on walls, characters bold. Sancai-glazed (三彩) vessel: amber, green, cream. Incense smoke robust, curling vigorously -- faster animation cycle. Bronze mirror reflects warm light. | Direct midday light through lattice -- sharp shadow edges on floor. High contrast. Dust motes active in strong beams. | 朱砂 #D4352C -- a red seal on a scroll, a sash draped over the screen edge, the bronze mirror's warm reflection. | The room as studio. Youth creates; the room is alive with purpose and making. |
| **Autumn** | Table shows wear -- ink stains on wood grain (dry brush enters at edges). Cushion fabric faded. Platform bed with heavier, darker textiles. 6-10 strokes per object -- some sure, some hesitant. | Screen partially folded; one panel angled inward for privacy. Paper shows age. Garden visible through open panels -- leaves thinning, warm amber through branches. | Scrolls yellowed at edges. Bronze mirror's reflection softer, warmer. Ceramic vessel holds dried branches, not blossoms. Incense smoke horizontal -- drifting, not rising. | Golden hour light entering low through west-facing lattice. Long soft shadows. Warm amber tint over everything. Micro-animation: light visibly moves -- slow, 60-90s drift across the floor. | 暮光 #D4A96A -- the golden light itself, the bronze mirror's patina, the dried branch that once held blossoms. | The room as repository. Everything has been touched many times. The room remembers. |
| **Winter** | Table sparse -- most objects gone. Platform bed with thin, pale covering. Dry brush (枯笔) dominant: streaky, porous strokes. Furniture edges dissolve into ink wash at extremities. 3-5 strokes per object. | Screen nearly folded -- one panel extended, its painted mountain now a faint wash-ghost. Lattice mostly closed. Garden beyond: bare branches and mist, near-monochrome. Indoor/outdoor boundary blurs. | One scroll remains, characters half-faded into 淡墨. Bronze mirror reflects only gray. Incense burner cold. Ceramic vessel with single dried stem -- the last thing left. | Dusk light: thin, cool, horizontal. Interior near-monochrome blue-gray (寒蓝 domain). One warm pinpoint: the last scroll's vermilion seal, glowing faintly -- ≤3% of frame area. | 残光 #E8C080 -- that single seal. The faint warm ghost in the bronze mirror. Nothing else. | The room as memory itself. Almost empty. What remains is precious because it is the last. Not sad -- complete. |

#### Example 3: Tang Dynasty Street -- 长安街景 (Chang'an Street)

| Season | Architecture & Roofs | Street & Surface | Shopfronts & Signs | Atmosphere/Light | Color Thread | Emotional Register |
|---|---|---|---|---|---|---|
| **Spring** | Eave curves (飛檐) rendered in tentative, thinner strokes -- 3-5 per roofline; gentle upward sweep suggested, not defined. Post-and-beam (斗拱) hinted at street level, 5-8 strokes total. Distant pagoda: wash-suggestion, 3 strokes. | Wide stone-paved road -- irregular stone outlines, soft moss at edges, puddles reflecting pink sky. Sparse prop density: 3-5 visible objects (a basket, a barrel, a cart wheel). | Hanging signs (招牌) with fresh ink characters. 1-2 shop fronts open; warm interior glow bleeding out. No visible figures, or one minimum-viable (5-8 strokes) at distance. | Morning mist lifting. Soft diffuse light. Gentle edge vignette. The street feels half-awake -- the child's world is small in a big, gentle space. | 新绿 #C5D8B5 -- moss at stone edges, new leaves on a courtyard tree extending over a wall, the edge of a shop awning. | Wide-eyed. The street is big but not frightening -- a place of discovery at small-person scale. |
| **Summer** | Eave curves confident and bold -- pronounced upward sweep, 8-12 strokes per roofline. Post-and-beam legible in full: 12-18 strokes, geometric accent within organic brushwork. Distant pagoda defined: 8-12 strokes, clear silhouette, rhythmic roof tiles. | Stone road at full definition -- individual stones visible. Canal to one side with modest arched bridge, water bright and reflecting 碧蓝. Shadow patterns from eaves fall sharp across stone. | Signs fully legible, calligraphy bold. Multiple shop fronts open -- interiors visible. Standard prop density: 8-12 objects (lanterns, displayed goods, stacked crates, a vendor's stall). One supporting figure (12-25 strokes) at a shop front. | Direct midday sun. Strong directional light. Sharp shadow edges from eaves and bridge. High contrast. Micro-animation: a shop sign swaying gently (4-6s cycle), a distant figure moving. | 碧蓝 #3B7CB8 -- canal water, sky above pagoda. 朱砂 #D4352C spike -- a red lantern, a vendor's cloth. | Alive. The street is the stage of youth -- full of possibility, full of energy, full of being young in a city that is the center of the world. |
| **Autumn** | Eave curves still present but strokes slower -- the brush lingers, 6-10 strokes. Post-and-beam 8-12 strokes; dry brush (枯笔) enters at beam ends. Distant pagoda softer -- recedes into golden haze, 5-8 strokes. Roof tiles suggested with economy. | Stone road with fewer defined stones -- more wash, less line. Wear visible: ruts from cart wheels. Canal lower, reflecting gold. Bridge stones warm-toned. Leaves scattered: individual ink dots, 3-5 strokes total. Sparse-to-Standard props: 5-8 objects. | Signs legible but characters have settled -- less bold, more worn. Shop interiors warm-lit but quieter. One supporting figure walking away, back turned -- posture carries weight. | Golden hour. Long horizontal light. Shadows stretch across the street. Warm amber wash. Micro-animation: a single leaf falling -- slow, deliberate, 8-10s cycle. | 枫红 #B5412C -- a fading red lantern, an autumn leaf on stone, a sign character written in red. | Knowing. The street is the same street, but the person seeing it has changed. Beautiful because it is familiar, not because it is exciting. |
| **Winter** | Eave curves barely suggested -- 3-5 strokes, dry brush dominant. Post-and-beam dissolves: beams fade into mist at ends, 3-5 strokes. Distant pagoda: silhouette, near-monochrome, 3 strokes maximum. Roof tiles no longer individual -- a wash suggests "roofness." | Stone road almost entirely wash -- dark gray with scattered ink dots for remaining visible stones. Canal surface still, gray, reflecting nothing. Bridge: a dark curve, near-silhouette. Empty. Props at minimum: 1-3 objects -- one lantern, unlit; one abandoned crate. | Signs faded -- characters nearly illegible, like old memory. Shops closed or dark. Any figures are minimum-viable (5-8 strokes), distant, facing away -- they are scenery, not subjects. | Dusk fading to night. Near-monochrome: cool grays/blues, 5-20% saturation. One warm pinpoint: a single lit lantern at a distant gate, ≤5% frame area. Micro-animation: faint breath of wind moving the lantern -- the last motion, 10-15s cycle. | 残光 #E8C080 -- that single lantern. Nothing else warm in the entire frame. | Distant. The street is almost a memory of a street. The person who was young here is now old. The street holds its silence like a held breath. The lantern is enough. |


---

### 6.7 Depth and Parallax -- The Scroll in Layers

#### Core Principle: Depth Is Painted, Not Calculated

回响 is a 2D game. Depth is created through composition, layering, atmospheric perspective, and parallax motion -- not through a z-buffer or 3D camera. The "camera" is a viewport panning across a multi-layer scroll painting.

#### Layer Architecture

The game world is composed in four discrete depth layers. Each layer is a separate 2D plane that moves at a different parallax rate relative to camera motion.

| Layer | Depth Position | Parallax Rate | Content | Stroke Density | Opacity / Atmosphere |
|---|---|---|---|---|---|
| **L1: Foreground** | Closest to viewer. In front of the spirit. | 1.2-1.5x camera speed (moves fastest). | Framing elements that partially occlude the scene: an overhanging branch, a screen edge, an architectural element at the near edge. | 5-15 strokes. Sparse -- foreground is accent, not obstruction. | Full opacity but soft-edged. Foreground elements have ink-wash fade on the inner edge (the edge that faces the scene) to prevent hard occlusion. |
| **L2: Midground (Action Plane)** | The spirit's plane. This is where gameplay happens. | 1.0x camera speed (moves with camera -- this IS the reference plane). | The spirit, memory figures, significant props, architectural elements, ground plane. | Hero and Supporting density (see Section 3 hierarchy). | Full opacity and detail. This is the in-focus plane of the memory. |
| **L3: Background** | Behind the midground. Architecture and landscape elements at medium distance. | 0.5-0.7x camera speed (moves slower). | Context trees, secondary buildings, hills, walls, street elements beyond the immediate scene. | Background density (3-8 strokes per element). | 15-25% atmospheric haze overlay. Colors shift 5-10% toward the atmospheric haze color for the current season. |
| **L4: Far Background** | Deep distance. Mountains, sky, horizon. | 0.15-0.3x camera speed (moves slowest). | Mountains, distant water, sky gradient, clouds, stars. | Bare minimum (1-5 strokes per element). | 30-50% atmospheric haze overlay. Colors significantly desaturated and shifted toward atmosphere. |

#### Parallax Behavior by Game State

| Game State | Parallax Active? | Behavior |
|---|---|---|
| **Exploring memories** | Yes. | Full parallax -- all four layers shift at their defined rates as the player drifts horizontally. Vertical parallax is minimal (0.1-0.2x on background layers). The sensation is of floating through a scroll painting -- slow, smooth, gently dimensional. |
| **Choice moment** | No. Camera locks. | All layers freeze. The camera pushes in toward the midground (spirit plane) on a straight z-axis -- no horizontal shift. The foreground and background layers scale slightly to simulate depth push (foreground scales up 5-10%, background scales down 5-10%), but do not shift laterally. |
| **Memory transition** | Partial. | Only L3 and L4 continue subtle parallax at 0.5x their normal rate. L1 and L2 are static or dissolving. This creates the sensation that the world is falling away -- the deep layers are still "alive" while the near world dissolves. |
| **Chapter ending** | No. | All layers static. The closing scroll border (see Section 2, State 4) is composited above all layers. |
| **Main menu** | Partial. | L4 (far background) has a very slow continuous pan at 0.1x to create the "breathing" effect. L1-L3 are static. |

#### Depth Cues Beyond Parallax

Parallax is the motion-based depth cue. Static depth cues supplement it for screenshots, still frames, and moments when the camera is not moving.

| Depth Cue | Mechanism | Example |
|---|---|---|
| **Atmospheric perspective** | Distant objects are lighter, lower contrast, and shifted toward the atmospheric haze color. Farther = more haze. | A mountain in L4 is near the haze color. The same mountain in L3 (if it were closer) would be darker and more saturated. |
| **Stroke density falloff** | Stroke count decreases with depth. L2 objects may have 30 strokes; L3 objects have 3-8; L4 objects have 1-5. | A tree in L2: trunk + branches + leaf clusters = 25 strokes. A tree in L3: trunk + one canopy dab = 6 strokes. A tree in L4: vertical mark + dot = 2 strokes. |
| **Scale overlap** | Closer objects are larger and may partially overlap farther objects. This is the primary monocular depth cue in 2D art. | A foreground branch (L1) crosses the top 15% of the frame, partially occluding the midground architecture. The player reads "this branch is close to me." |
| **Edge treatment** | Closer objects have more defined edges. Distant objects have softer, more dissolved edges. | L2 element: clean contour line with controlled ink-bleed. L3 element: feathered edges, ink-wash fade. L4 element: no contour line -- defined entirely by wash shape. |
| **Value contrast** | Closer = higher contrast (darker darks against lighter lights). Distant = lower contrast (compressed value range). | L2: Sumi Ink `#2C2416` line on Rice Paper `#F5F0E8` -- 12:1 contrast. L4: Light Ink Wash `#8A8078` wash on atmospheric haze -- 3:1 contrast. |

#### Camera Motion and Drift

The camera follows the spirit's drift with a gentle, elastic behavior.

| Parameter | Specification |
|---|---|
| **Follow mode** | Soft follow with horizontal offset. The spirit is positioned at 35-40% from the left edge (in the drift direction) and 45-55% from the top. This gives the player more screen space to see what they are drifting TOWARD. |
| **Follow speed** | Spring: 0.8x spirit speed (slow, dreamy). Summer: 1.0x (responsive). Autumn: 0.7x (weighted, deliberate). Winter: 0.5x (slowest, most distant). |
| **Max pan range** | Each memory fragment has a defined scroll width. The camera pans within this range and stops at scroll edges (the memory's boundary). The scroll edge is not a hard cut -- it is an ink-wash fade (Section 3, Edge Treatment). |
| **Vertical drift** | Minimal. The spirit drifts primarily horizontally. Vertical drift of 2-5% of frame height may occur on specific narrative beats (a moment of rising, a moment of sinking). |
| **Push-in (choice)** | Linear push over 1.5-2 seconds. Spirit resolves to push-in detail by 1-second mark. Remaining time is held-breath stillness before choice options appear. |
| **Drift lean correlation** | The spirit's body lean angle (Section 5.1.1) and the camera's follow offset are correlated -- the more the spirit leans, the more the camera leads. The player unconsciously reads "I am moving faster" from the combined visual of body lean + camera offset. |

#### Technical Implementation Notes

- All layers are 2D sprite/quad planes in orthographic projection
- Parallax is achieved through position offset multipliers on camera movement delta, not through 3D z-position
- Layer order (back to front): L4, L3, L2, L1. The camera's push-in (for choice moments) scales L1-L3 around the focal point (the spirit's position)
- Memory fragments are composed as a single wide canvas (e.g., 3840x1080 for a fragment with 2x horizontal scroll) with layer content positioned within that canvas
- The "scroll edge" at the boundary of each memory fragment is a soft ink-wash fade applied as a gradient mask on all layers simultaneously

---

### 6.8 Design Tests

**Test 1 -- Season Identification from Environment Alone (No Figures, No Text)**:
Create four environmental scenes (a garden, a room, a mountain view, a street) rendered in all four seasons. Remove all figures and text. Present 16 images (4 scenes x 4 seasons) in randomized order to 5 playtesters. Task: "Identify the season of each image." Pass: 4/5 participants correctly identify 14 of 16 (87.5%). The two allowable misses should be spring/autumn confusions (both are warm-medium saturation). Zero summer/winter confusions permitted.

**Test 2 -- Location Continuity Across Chapters**:
Present two environments that are the same location in different chapters (e.g., a childhood home in spring AND the same home in winter). 5 playtesters. Task: "Are these the same place?" Pass: 4/5 say yes AND can point to at least one specific element that identified the location (the tree, the roofline, the pond shape, the stone arrangement). This validates that environmental continuity markers survive seasonal transformation.

**Test 3 -- Choice Outcome Visibility in Environment (No UI, No Text)**:
Record a 5-second clip of an environment before a choice, then the 5-second environmental transition after a "lost" choice. Remove all UI and text. 5 playtesters. Task: "Describe what changed in the room." Pass: 4/5 identify that the room became emptier, colder, or less defined. They do not need to use the word "lost" -- any description of loss/diminishment/cooling counts.

**Test 4 -- Inherent Story Readability (3-Second Glance)**:
Show a memory fragment for exactly 3 seconds, then hide it. 5 playtesters. Ask: "Who was here, what were they doing, and how did they feel?" Pass: 4/5 answer at least 2 of 3 correctly. The emotional question ("how did they feel") is the most important; the "who" and "what" questions can be generic ("a family," "eating together") as long as the emotional register is accurate.

**Test 5 -- Prop Density Ceiling**:
Analyze 10 randomly selected memory fragments. Calculate total prop area / total frame area for each. Pass: 0 of 10 exceed 25% prop area. This validates that the 留白 budget (Section 6.4, Prop Placement Rule 3) is respected across the full game.

**Test 6 -- Depth Readability in Static Frame (No Parallax)**:
Present a static screenshot of an environment with all four layers (L1-L4) populated. 5 playtesters. Task: "Point to the closest object and the farthest object in this scene." Pass: 5/5 identify a foreground element as closest and a far-background element (mountain, sky feature, distant tree) as farthest. This validates that static depth cues (atmospheric perspective, scale, edge treatment) work without parallax motion.

---

## Section 7: UI/HUD Visual Direction

### 7.1 Screen Typography and Sizing

**One-line rule**: The text on screen is a brush that happens to form characters. It belongs to the scroll, not to the machine.

#### Font Personality

回响's typography must feel like calligraphy — organic, variable, hand-held. The font is not a typesetting system; it is a brush making words.

**Chinese font direction**: A running-script (行书) inspired typeface with visible stroke variation, organic terminals, and ink-like texture. The characters should read as "brushed onto the scroll, not printed onto a screen." Not a rigid Song/Ming typeface (too mechanical, too "published"). Not a fully cursive grass-script (too illegible for sustained reading). The ideal sits at the intersection of legibility and brush energy — a 楷体 (regular script) foundation with expressively varied stroke weight.

**English font direction**: A humanist serif with organic terminals, moderate stroke contrast, and a warm, slightly irregular rhythm. The English font is the guest language — it must pair harmoniously with Chinese calligraphic characters without mimicking them (an English font that tries to look "Chinese" becomes caricature). Look for qualities that naturally echo ink on paper: visible stroke modulation, slightly asymmetrical counters, terminals that resolve softly rather than sharply. Think of a warm, hand-set letterpress quality rather than cold digital precision.

**Font pairing rule**: When Chinese and English text appear together, the English must read as a translation whispered beside the original, not as equal-weight text. The Chinese carries the emotional weight; the English carries the meaning.

#### Weight Hierarchy

| Weight Tier | Usage | Rationale |
|---|---|---|
| **Display / Title** | Chapter titles, Main menu title (回响), Ending reveal text | Heaviest presence. Full brush expressiveness. Characters may be partially "wet" or "in-progress." 回响 title characters use accumulated ink (积墨) — never fully dry. |
| **Heading** | Section headers within menus, Chapter select labels, Choice prompt headers | Confident but restrained. Clean brush strokes. Fully "dry" and legible. |
| **Body / Menu** | Menu options, Dialogue text, Memory fragment descriptions, Choice option text | Standard reading weight. Prioritize legibility over expressiveness. The brush is controlled — the reader should not notice it. |
| **Label / Secondary** | Settings labels, Chapter progress indicators, Input prompts | Lighter weight. Less ink on the page. The brush is barely touching the paper. |
| **Micro-text** | Version numbers, Copyright, Tutorial hints, Accessibility labels | Thinnest weight. Functional only. The brush is at its driest. |



**Running script limitation (UX constraint)**: Running script (行书) is reserved for Display and Heading tiers only. At Body, Label, and Micro sizes, the connected strokes of semi-cursive characters become illegible -- structurally similar characters (e.g., 已/己/巳) collapse into near-identical brush gestures below 24px. Body, Label, and Micro tiers use a regularized semi-cursive (楷乓-adjacent) typeface: still brush-flavored with visible stroke modulation, but with discrete, separated strokes for reliable character recognition. The brush aesthetic carries through stroke weight variation and organic terminals, not through stroke connection.

#### Size Tiers (at 1080p)

All sizes below are for Chinese text. For English, reduce by approximately 15-20% at equivalent tiers (English characters are less dense and more legible at smaller sizes). For mixed-language text, use the Chinese size tier and let the English sit smaller within the same line height.

| Tier | Chinese Size | English Size | Usage | Minimum Contrast |
|---|---|---|---|---|
| **Display** | 56-72px | 44-56px | Chapter titles, Main menu title 回响, Ending reveals | ≥7:1 (AAA) |
| **Heading** | 36-48px | 28-38px | Menu section headers, Choice prompt framing text | ≥4.5:1 (AA) |
| **Body** | 24-30px | 18-24px | Menu options, Dialogue, Choice option text, Memory descriptions | ≥4.5:1 (AA) |
| **Label** | 18-22px | 14-18px | Settings labels, Chapter indicators, Input prompts | ≥4.5:1 (AA) |
| **Micro** | 18-20px | 14-16px | Version number, Copyright, Tutorial hints | ≥3:1 (acceptable for non-critical only) |

**Size flexibility rule**: Body text may scale ±4px based on available space and visual balance. Display and micro-text sizes are fixed — titles must command the frame; micro-text must not attempt to.

**Line spacing**: 1.5x to 1.75x line height for body text. Chinese characters benefit from generous vertical spacing — crowded lines undermine the 留白 philosophy.

**Chinese character minimum legibility**: 20px at 1080p (18px at 720p). Below this, brush-stroke terminals blur together and stroke count information is lost. Never render Chinese UI text below 20px.

#### Text Rendering Notes

- Text must render with subpixel anti-aliasing enabled. The brush-stroke quality of the font depends on smooth glyph edges.
- At display sizes (56px+), consider a subtle paper-texture overlay behind or within the text — the rice paper of the scroll should be faintly visible "through" the largest characters.
- Text color: Sumi Ink `#2C2416` for all active/interactive text. Light Ink Wash `#8A8078` for secondary/inactive/read-only text. Never pure black, never pure white (per Section 4.13 violations).
- Text that appears as ink-spread reveal (see 7.3) should briefly show the vermilion or seasonal accent as its "wet" state before resolving to Sumi Ink.

---

### 7.2 Iconography Style

**One-line rule**: An icon is a single-brushstroke idea. If it needs more than five strokes, it is an illustration, not an icon.

#### Icon Rendering Mode

Icons in 回响 are rendered as **calligraphic brush-line marks** — outlined shapes made from 1-5 deliberate brush strokes with visible variable weight and organic terminals. No filled geometric shapes. No uniform-stroke vector icons. No pixel icons.

| Icon Category | Stroke Budget | Style | Example |
|---|---|---|---|
| **Navigation icons** (back, forward, menu, close) | 1-3 strokes | Single continuous brush mark. Like a calligrapher's shorthand symbol. | Back arrow = one curved stroke with a hook terminal. Close X = two crossing dry-brush strokes. |
| **Action icons** (confirm, cancel, interact, inspect) | 2-4 strokes | Slightly more formed — the action is legible as a gesture. | Confirm = a descending dot (the brush pressed). Cancel = a horizontal strike-through. |
| **Status icons** (completed, locked, current, new) | 1-2 strokes | Minimal — a dot, a circle, a seal shape. Rendered with seasonal accent colors. | Completed = filled vermilion seal dot. Locked = empty ink circle. Current = seasonal warm dot with pulse. |
| **System icons** (audio, display, language, controls) | 3-5 strokes | Representational but simplified — the brush suggests the object, never describes it. | Audio = one curved stroke for an ear shape + two suggestion strokes for sound waves. |
| **Achievement / seal icons** | 1 stroke + color fill | Square seal-script format. Rendered in Vermilion Ink `#A03828` or Gold Dust `#C4A456`. Like a seal stamped onto the scroll. | Chapter complete seal. Hidden ending seal. |

**Icon color rules** (reinforcing Section 4.8):
- Active/interactive icons: Sumi Ink `#2C2416`
- Inactive/disabled icons: Light Ink Wash `#8A8078` at 50-70% opacity
- Selection highlight: Vermilion Ink `#A03828` ink wash behind the icon at 30-40% opacity
- Confirmation/completion: Vermilion Ink `#A03828` at full opacity (seal-stamp look)
- Highest significance (chapter complete, hidden ending found): Gold Dust `#C4A456` — used at most 5 icons in the entire game

#### Minimum Legible Icon Size

| Context | Minimum Size (at 1080p) | Rationale |
|---|---|---|
| **Standalone icon** (menu icon, action button) | 28x28px | Below this, variable brush weight becomes illegible and the icon reads as a smudge. |
| **Icon + label pair** | 22x22px (icon) + label text | Context from adjacent label supplements the icon's legibility. |
| **Inline icon** (icon within text flow) | 18x18px | At this size, icons must use at most 2 strokes. Single-stroke marks preferred. |
| **Progress dot** | 8x8px (filled), 6x6px inner for unfilled | Already defined in Section 3. These are dots, not icons. |
| **Below minimum** | Do not render | If an icon cannot fit at 18x18px, use text or remove the icon. Never compromise the brush-stroke quality by shrinking below minimum. |

**Icon simplification rule**: As icon size decreases, reduce stroke count — never reduce stroke quality. A 2-stroke mark at 22px with full brush expressiveness is better than a 5-stroke mark at 22px with strokes so thin they lose their variable weight.

#### Icon Anti-Patterns

- Solid-filled geometric shapes (circles, squares, triangles) — these are not brush marks
- Uniform line weight — betrays the brush
- Pixel-aligned hard edges — the brush does not snap to a grid
- Icons that require color to be legible — every icon must read in grayscale through shape alone
- Icons rendered at different stroke weights within the same screen — inconsistent brush pressure

---

### 7.3 Animation Feel for UI

**One-line rule**: UI does not appear — it arrives on the brush. UI does not disappear — it lifts off the page.

#### Animation Philosophy

Every UI transition is a brush event. The player should feel the presence of the brush even in purely functional moments: opening a menu, confirming a choice, reading a prompt. The animation language has three core verbs: **arrive** (ink spread / brush draw), **transform** (ink bleed from one state to another), and **depart** (fade-to-ink / brush lift).

#### Arrive: How UI Elements Appear

| Technique | Description | Duration | Easing | Best Used For |
|---|---|---|---|---|
| **Ink-spread reveal** | Element blooms from its center outward. Color spreads like ink dropped into wet paper — fast initial spread, slower edge settling. Edges remain soft and slightly feathered until fully resolved. | 0.5-0.8s | Exponential ease-out (fast start, slow finish) | Menu screens appearing; dialogue text appearing; choice options emerging from anchor |
| **Brush-stroke draw** | Element is "painted in" by an invisible brush moving along the element's dominant axis. Left-to-right for horizontal elements (menu text, dividers). Top-to-bottom for vertical elements. The stroke begins dry (thin, tentative) and resolves to full weight. | 0.4-1.0s (proportional to element width/height) | Ease-out with slight overshoot at stroke terminus (like brush pressing at end of stroke) | Menu text appearing line by line; scroll border drawing in; ink-thread lines in transitions |
| **Seal-stamp press** | Element appears as if a seal was pressed onto the scroll — brief compression (scale 0.9x), then release to 1.0x with a soft settle. Used sparingly for high-significance moments. | 0.4-0.6s | Bounce ease-out (one gentle bounce, never more) | Chapter completion seal; hidden ending reveal; choice confirmation |
| **Ink-dot percipitation** | Small elements (dots, micro-icons) appear by "precipitating" from the scroll surface — fading in from 0% to 100% opacity with a slight downward drift (2-4px), like ink particles settling. | 0.3-0.5s | Ease-out | Progress dots updating; interaction prompts appearing; status icons resolving |

#### Transform: How UI Elements Change State

| Technique | Description | Duration | Easing | Best Used For |
|---|---|---|---|---|
| **Wash bloom (hover)** | A soft ink wash (Vermilion Ink at 10-15% opacity) blooms behind the hovered element. The wash starts from the element's center and spreads outward, staying within a soft organic boundary. | 0.2-0.3s | Ease-out | Hover state on menu items, interactive text, choice options |
| **Wash deepen (selection)** | The existing wash deepens to 30-40% opacity (per Section 4.8). The boundary tightens slightly — the wash becomes more "focused." | 0.3-0.5s | Ease-out | Selecting a menu item; clicking an interactive element |
| **Color bleed (outcome)** | Anchors shift color through ink-bleed — new color enters at the edges and spreads inward, like ink dropped at the perimeter bleeding toward center. Old color drains simultaneously from the center outward. | 1.0-2.0s (proportional to anchor size) | Exponential ease-out | Choice outcome visualization (preserved/lost/transformed/bittersweet) |
| **Pulse (idle attention)** | Gentle rhythmic opacity oscillation on interactive elements waiting for input. Opacity cycles: 100% → 80% → 100%. | 2.0-3.0s per cycle | Sine-wave ease-in-out | Interaction prompts near un-activated anchors; "new" chapter indicators; unpaused-but-idle menu |
| **Dissolve (deactivation)** | Element loses opacity and structural coherence simultaneously. Lines thin and break apart as they fade. Element resolves toward the base palette — Sumi Ink toward Light Ink Wash, then toward Rice Paper. | 0.5-1.0s | Linear | Disabling an option; progressing past an anchor; closing a sub-menu |

#### Depart: How UI Elements Disappear

| Technique | Description | Duration | Easing | Best Used For |
|---|---|---|---|---|
| **Fade-to-ink** | Element fades from full ink toward Light Ink Wash `#8A8078`, then toward Rice Paper `#F5F0E8`. The two-stage fade (ink → wash → paper) feels like the brush lifting and the ink residue drying to nothing. | 0.6-1.0s | Ease-out (stage 1), Linear (stage 2) | Closing menus; dismissing prompts; departing choice options that were not selected |
| **Brush lift** | Element's brush stroke "lifts off" the page — the stroke thins from its trailing edge to its leading edge, then vanishes. The visual equivalent of a brush stroke that gradually loses pressure until it leaves the paper. | 0.3-0.6s | Ease-out (accelerating thinness) | Individual UI text lines departing; scroll border retracting |
| **Ink dispersion** | Element breaks apart into ink dots that scatter outward and fade. Like ink particles dispersing in water. | 0.8-1.2s | Ease-out for scatter distance, simultaneous fade | Memory transition UI dissolving; notification dismissal; chapter complete screen transitioning to next |
| **Scroll roll (screen transition)** | Full-screen transition where the current view appears to be rolled up like a scroll — an Aged Paper border sweeps across from one edge, "rolling up" the image. The next screen unrolls from the opposite edge. | 1.0-1.5s | Ease-in-out (center of the roll movement) | Chapter transitions; major menu changes (Main Menu → Chapter Select) |

#### Duration and Easing Philosophy

| Rule | Specification | Rationale |
|---|---|---|
| **Maximum single animation** | 1.5 seconds | Longer animations feel sluggish and break the player's sense of agency. The scroll breathes slowly; the UI does not. |
| **Minimum single animation** | 0.2 seconds | Faster feels like a glitch, not a brush event. The brush always has weight; weight means time. |
| **Default easing** | Ease-out | The brush decelerates as it completes a stroke. Arrival is softer than departure. |
| **Simultaneous animations** | Max 3 elements animating simultaneously on screen | More than 3 concurrent animations creates visual noise. Stagger if needed — 0.1-0.2s delay between each. |
| **Interruptibility** | All UI animations are interruptible | If the player inputs before an animation completes, the animation resolves to its end state immediately. Never block input for animation. |
| **Animation-free mode** | Supported (accessibility) | All UI transitions can be replaced with instant 0.1s crossfades. See 7.7. |

**Duration hierarchy**: Arrive > Transform > Depart. Elements take longer to arrive (the brush must commit) than to depart (the brush simply lifts). Transformations sit between — the brush is already on the page; it adjusts.

---

### 7.4 Layout Philosophy

**One-line rule**: The center of the frame belongs to memory. UI lives at the margins, like red seals and colophons on a scroll — present only when needed, never competing with the painting.

#### Screen Zones

At 1080p, the screen is divided into five zones based on their relationship to the scroll painting:

| Zone | Screen Area | UI Content | Visual Treatment | Rationale |
|---|---|---|---|---|
| **Memory Canvas** | Center 70% (15%-85% width, 10%-90% height) | None. This is the painting. UI only enters here during choice moments (choice options grow from the anchor inside this zone). | The memory painting. Negative space, brushwork, atmosphere. Protected from persistent UI. | The painting is what the player came to see. UI must not obstruct it. |
| **Scroll Margin — Top** | Top 10% of screen, full width | Chapter title (faint, top-left). Progress dot row (top-right). Both rendered at 40-60% default opacity, fading to full on hover/focus. | Restrained ink marks on the scroll border. Like a chapter heading brushed at the top of a scroll. | Context without intrusion. The player glances up when they need orientation. |
| **Scroll Margin — Bottom** | Bottom 10% of screen, full width | Dialogue/subtitle text (centered or left-aligned, depending on speaker). Interaction prompts (near the relevant anchor). | Text area with subtle Aged Paper `#E8DEC8` border strip — not a solid box, but a suggestion of the scroll's lower margin. | The scroll's natural text area. The bottom margin is where colophons and annotations live. |
| **Scroll Margin — Sides** | Left/Right 15% each, full height | Nothing by default. Scroll border rendered as a faint Aged Paper edge. Interactive when containing chapter navigation arrows. | The rolled edges of the scroll. The majority of the time, these are purely atmospheric — part of the "wall-hangable painting." | Framing device. The scroll has edges; the player should feel them but not interact with them unless navigating. |
| **Overlay Layer** | Full screen (when active) | Pause menu, Settings, Chapter Select, Ending screens. Rendered as semi-transparent ink wash over the memory canvas. | Deep ink wash (Sumi Ink at 40-60% opacity) or seasonal desaturated wash over the current scene. The memory breathes underneath. | Diegetic overlay — the player is still "in" the scroll; they have simply paused to read its margins. |

#### Layout Principles

1. **Center is sacred.** No persistent UI element may occupy the central 50% of the screen (25%-75% width, 20%-80% height). This zone belongs exclusively to the memory painting and to choice options that grow FROM the painting.

2. **UI weight is proportional to frequency of use.** Elements the player needs constantly (interaction prompts) are small, subtle, and near the anchor. Elements the player needs rarely (settings, chapter select) are behind deliberate navigation. The most-used element is the memory painting itself — which has zero UI weight.

3. **Symmetry is memory-dependent.** The scroll layout is fundamentally asymmetrical — text aligns to the brush's natural rhythm, not to a grid. But within each memory fragment, the anchor's position dictates UI placement. If the anchor is on the left, textual UI may align right for counterbalance. If the anchor is centered, UI retreats to margins. The frame should feel composed, not engineered.

4. **UI appears and disappears with purpose.** When exploring memories, UI is at its minimum — progress dots and a faint chapter label. UI elements emerge only when the player's attention shifts to them (hovering near the top reveals the progress dots fully; approaching an anchor reveals the interaction prompt). This is "progressive revelation" through the brush language — UI doesn't hide; it rests.

**Interaction prompt rule (UX constraint)**: Interactive memory anchors need a persistent (not hover-dependent) subtle visual signature. The art bible's own 留白 principle resolves this: detail density itself signals interactivity -- Hero-level stroke density (30-50+ strokes) inherently draws the eye. The interaction prompt dot appears when the spirit is within the anchor's proximity zone (15-30% of frame width from the anchor center), not on pixel-hover. This provides diegetic, contextual guidance without requiring the player to already know where to look. On first encounter in Chapter 1, all UI elements briefly auto-reveal as a tutorial beat before settling into their resting state.




5. **The scroll border is the UI container.** All persistent UI elements (progress dots, chapter labels) live within or just inside the conceptual scroll border. They are part of the scroll's framing apparatus — not separate objects floating on glass.

#### Layout by Game State

| Game State | Active Zones | UI Elements Present | Layout Notes |
|---|---|---|---|
| **Exploring Memories** | Memory Canvas (full), Scroll Margin — Top (subtle), Scroll Margin — Bottom (contextual) | Faint chapter label (top-left), progress dots (top-right), interaction prompts (near anchors, contextual). Dialogue text in bottom margin when triggered. | Maximum painting, minimum UI. The frame is a painting with marginalia. |
| **Choice Moment** | Memory Canvas (with anchor isolation), Scroll Margin — Top (faded further) | Choice options growing from anchor (within Memory Canvas). Chapter label fades to 20% opacity. Progress dots remain but dim. | The choice IS the painting. Nothing else competes. |
| **Memory Transition** | Memory Canvas (ink-thread constellation), Scroll Margin — Top (faded) | Ink-thread lines and memory node points. No text UI. Chapter label may dissolve. Progress dots fade. | Pure visual language. No UI at all — the transition speaks through ink and color alone. |
| **Pause Menu** | Overlay Layer (full), Memory Canvas (dimmed beneath) | Menu options rendered as brushed text on the overlay. Continue, Chapter Select, Settings, Quit. | Semi-transparent wash overlay. The memory breathes underneath — the player hasn't left. |
| **Settings** | Overlay Layer (full) | Settings panels as annotation cards. Sliders and toggles in brush language. Back navigation icon. | Most screen-space layout. Diegetic framing (annotation cards tucked into scroll) with necessary screen-space controls. |
| **Chapter Select** | Overlay Layer (full) | Chapter scrolls (partially unrolled, one per chapter). Locked = sealed scroll. Completed = red seal stamp. Current = partially open. | Visual metaphor of a shelf of scrolls. Each chapter is a scroll object the player can touch. |
| **Chapter Ending** | Memory Canvas (full frame composition), Overlay Layer (closing scroll border) | Closing scroll border edges inward from all four sides. Ending text bleeds in from center. | The closing scroll border IS the UI. Text arrives as brushwork within the closing frame. |
| **Main Menu** | Memory Canvas (perpetual scroll), Scroll Margin — Bottom (menu text) | Title characters 回响 forming in center. Menu text brushed along the bottom margin. Faint life-stage afterimages at scroll edges. | The scroll IS the menu. No separate UI layer — everything is painted into the title scroll. |

---

### 7.5 Menu Screen Design

**One-line rule**: Every menu screen is a specific kind of scroll moment — a pause in the painting, not an exit from it.

#### Main Menu

*Expanding on Section 2, State 5.*

**Visual composition**: A single hand-painted scroll fills the frame. The title characters 回响 hover at center, rendered in accumulated ink (积墨) — always partially formed, never fully dry. Brushstrokes cycle in a 60-90 second loop: a new stroke arrives, an old one fades, but the characters never reach completion. The scroll background carries faint afterimages of all four life stages drifting at the edges — a child's toy, a young figure's shadow, an adult's hand, a cane — each barely visible, dissolving and reappearing.

**Menu text**: Brushed directly onto the scroll surface in the bottom third of the frame. Text reads vertically (top-to-bottom, right-to-left — traditional scroll reading order) or horizontally. Each menu item is separated by a small ink dot. Current selection marked by a Vermilion Ink wash behind the text.

**Menu items**: Continue (if save exists), New Journey (new game), Chapter Select, Settings, Credits, Quit. No more than 6 items — the scroll is not a list.

**Background animation**: The perpetual brushstroke loop (Section 2, State 5). Faint life-stage afterimages at edge. Subtle paper texture shifts — the scroll is breathing.

**Transition in**: Scroll unrolls from center outward. Title characters begin forming immediately. Menu text fades in via ink-spread 0.5s after scroll is fully open.

**Transition out**: Scroll rolls from edges inward toward center. The last element visible is the 回 character, which holds for an extra 0.3s before the scroll closes completely.

#### Pause Menu

**Visual composition**: A semi-transparent ink wash (Sumi Ink at 40-50% opacity) settles over the current memory scene. The wash has soft, irregular edges — it is a brush application, not a rectangle. The memory continues to breathe underneath at reduced visibility. One micro-animation element remains faintly visible through the wash — the player hasn't left; the world hasn't stopped.

**Menu items**: Continue, Chapter Select, Settings, Return to Main Menu. Text is brushed in Sumi Ink directly on the wash surface. Current selection marked by Vermilion Ink wash behind text. Items appear sequentially with 0.15s stagger.

**Arrival animation**: Wash blooms from center outward (0.5-0.6s). Menu items brush-draw from top to bottom with 0.15s stagger (0.4s each item).

**Departure animation**: Menu items fade-to-ink in reverse order. Wash lifts from edges inward (0.4-0.5s). Memory scene restores to full visibility with a gentle brightness bloom.

**Design constraint**: The pause menu must not use a solid opaque overlay. The player must always feel the memory is still "there" — they are pausing within the scroll, not tabbing out to a system screen.

#### Chapter Select Screen

**Visual metaphor**: A reading-room table or shelf, viewed from above or at a slight angle. Individual scroll objects represent each chapter. The base surface is Rice Paper `#F5F0E8` with a subtle aged texture. Lighting is warm and diffuse — late afternoon sun through a paper screen.

**Chapter scroll states**:

| Chapter State | Visual Presentation | Interaction |
|---|---|---|
| **Locked** (not yet reached) | Scroll is fully rolled and tied with a thin ink cord. The scroll body shows a faint wash of its season's color at the tied edge. No title visible. | Hover: soft glow at the tied edge. Click: no response (optional subtle shake — the cord holds). |
| **Unlocked** (reached but not started) | Scroll is tied but the cord is looser. A single brushstroke of the chapter title peeks from the unrolled edge. | Hover: cord loosens further, title peeks more. Click: scroll unrolls, chapter begins. |
| **In Progress** (started, not completed) | Scroll is partially unrolled — 30-50%. The exposed painting shows a miniature of the player's current position in the chapter's memory web. Faint ink-wash preview of the chapter's season. | Hover: scroll unrolls slightly more, showing more of the miniature. Click: chapter resumes from last position. |
| **Completed** (finished, main ending reached) | Scroll is fully unrolled. The chapter's ending tableau is visible as a miniature painting. A red vermilion seal stamp marks the lower corner. | Hover: seal glows softly. Click: chapter replay begins (from start, with new path options). |
| **Hidden Ending Found** | As Completed, but the seal is Gold Dust `#C4A456` instead of vermilion. A second, smaller seal appears beside it. | Hover: gold seal catches light. The miniature painting briefly shows a hint of the hidden ending's scene. |

**Chapter arrangement**: Scrolls are arranged left-to-right in chronological order (Spring → Summer → Autumn → Winter). Unreached chapters sit to the right. Spacing is generous — at least 15% of screen width between scroll objects. The 留白 philosophy applies to menu navigation too.

**Navigation**: Player drifts left/right to move between chapter scrolls. Current scroll is centered with adjacent scrolls partially visible. Selection is a soft Vermilion Ink wash bloom behind the chosen scroll. Confirm starts the chapter with an unroll animation (0.8-1.0s).

**Transition in**: From Main Menu: the title scroll fades to ink-wash, then the reading-room scene resolves from the wash. From Pause: the memory scene desaturates and the reading-room scene bleeds in from the edges.

#### Settings Screen

**Philosophy**: Settings is the most functional screen — it has sliders, toggles, dropdowns. It cannot be purely diegetic. But it can be *framed* diegetically. Settings panels are annotation cards tucked into the scroll — like a reader's notes slipped between the pages.

**Visual composition**: The current memory scene (or a generic scroll surface) sits behind a deep ink wash overlay (60-70% opacity — darker than pause, to focus attention on settings). On this surface, 3-4 annotation cards appear: Audio, Display, Language/Accessibility, Controls. Each card is an Aged Paper `#E8DEC8` rectangle with soft, irregular edges and a subtle ink border. Cards are arranged in a loose, asymmetrical grid — not a uniform table.

**Controls within cards**:

| Control Type | Brush-Language Rendering |
|---|---|
| **Toggle (on/off)** | A circular brush mark. Off = empty ink circle (淡墨 outline). On = filled Vermilion Ink dot within the circle. Toggle transition = dot blooms or recedes (0.3s). |
| **Slider (continuous value)** | A horizontal brush stroke (淡墨, variable width — the stroke is thickest at the current value). The "handle" is a vermilion brush dot at the current position. |
| **Dropdown / Selector** | Current selection displayed as brushed text. Tapping blooms the options list as a vertical ink wash column below. Options are additional brushed text items. |
| **Button (action)** | Brushed text with ink wash behind. Press = ink wash deepens (0.2s) then releases with seal-stamp feel (0.3s). |

**Navigation**: Back icon (single curved brush stroke) at top-left. Section tabs rendered as horizontal brushed labels. Current tab = Vermilion Ink wash behind.

**Key constraint (revised per UX review)**: Settings controls must use recognizable interaction patterns with brush aesthetic applied as a skin, not a replacement. A toggle is a square seal stamp -- on = vermilion ink impression (filled square with visible brush texture), off = faint ink outline only (single stroke, unfilled). A slider is a horizontal brush stroke with a vermilion dot at the current position. A dropdown is brushed text that blooms open with additional options. **Focus indicators are mandatory**: a soft Sumi Ink wash glow (15-25% opacity) behind the currently focused element ensures keyboard/gamepad navigability. The rule: brush-aesthetic controls with standard affordances, not controls that discard standard affordances for brush language.

---

### 7.6 Interaction Feedback

**One-line rule**: Every interaction is a brush event. The player touches the scroll; the scroll responds with ink.

#### Feedback States

Building on the UI Shape Grammar from Section 3 and the color assignments from Section 4.8.

| State | Visual Response | Audio Note | Duration | Color Reference |
|---|---|---|---|---|
| **Idle (interactive, untouched)** | Element rendered in Sumi Ink `#2C2416` at 100% opacity. If the element is a "discovery" type (newly available), it carries a subtle idle pulse (opacity 100% → 80% → 100% over 2.5s). | None. | Continuous | Section 4.8: Primary text |
| **Hover / Focus** | Soft Vermilion Ink wash blooms behind element at 10-15% opacity. Wash is organic in shape — follows the text/icon contour, not a box. Element's brush strokes may warm slightly (shift toward vermilion by ~5 degrees). Cursor changes to a brush-dot or brush-tip shape. | Subtle paper rustle or single soft brush-on-paper sound. | 0.2-0.3s wash bloom | Section 4.8: Selection highlight (reduced opacity for hover) |
| **Press / Select (click down)** | Wash deepens to 25-30% opacity. Element compresses slightly (scale 0.97x) — the brush is pressing into the paper. Duration: held state while input is down. | Slightly deeper, more present brush sound — ink loading onto the brush. | 0.1s press animation | Section 4.8: Selection highlight |
| **Confirm (click release)** | Wash blooms to full 30-40% opacity (per Section 4.8). Element releases compression (scale 0.97x → 1.0x) with a gentle 1.02x overshoot, settling back to 1.0x — the brush pressed and lifted. If action proceeds (new screen, chapter start), element fades as next screen arrives. | Seal-stamp sound — soft but definitive. The ink has touched the paper. | 0.3-0.5s | Section 4.8: Selection highlight |
| **Disabled / Inactive** | Element rendered in Light Ink Wash `#8A8078` at 50-70% opacity. No hover response. Cursor remains neutral. | None. | N/A | Section 4.8: Secondary / inactive text |
| **Error / Invalid** | Element pulses Vermilion Ink at 40% opacity for one quick cycle (0.15s at 40%, fade to 0% over 0.3s). Single pulse only — never repeated flashing. | Soft negative brush sound — like a brush being set down, not used. | 0.45s total | Section 4.8: Vermilion Ink accent |
| **Loading / Processing** | If a load exceeds 0.5s: a single ink dot appears and slowly spreads into a small organic ring, then recedes. Cycles gently. Not a spinner — an ink drop in water, expanding and contracting. | None unless load exceeds 2s (ambient transition sound then plays). | Until load complete | Base palette: Sumi Ink at 30-50% opacity |

#### Cursor Style

The system cursor is replaced with a custom brush-themed cursor:
- **Default (exploring)**: A small ink dot (6-8px), slightly irregular (not a perfect circle). Rendered in Sumi Ink with a faint soft halo.
- **Hover (interactive)**: The dot warms to Vermilion Ink, and the halo expands slightly. A second, fainter dot may appear to the upper-right — suggesting the brush is "ready."
- **Press (clicking)**: The dot compresses (scale 0.8x vertically) as if the brush is pressing into the paper.
- **Dragging**: The dot elongates into a short brush stroke in the direction of the drag — the brush is moving across the paper.
- **Text input**: Standard text cursor, but rendered as a thin vertical brush stroke (fading from Sumi Ink at top to Light Ink Wash at bottom — the brush was pressed and is lifting).

**Cursor rules**: The cursor dot must always be legible against the current background. On very dark areas, the dot inverts to Rice Paper `#F5F0E8` with a Sumi Ink halo. The cursor speaks the brush language even in its smallest form.

#### Choice Moment Feedback (Extended)

Choice moments are the highest-stakes interaction in 回响. The feedback sequence expands from what's defined in Section 2, State 2:

1. **Anchor isolation** (arrival): Camera pushes in. Anchor sharpens. Periphery fades toward ink wash. Spirit resolves to push-in detail. Duration: 1.5-2.0s.

2. **Choice options emerge** (arrival): Branching ink strokes grow outward from the anchor. Each option is a calligraphic text phrase rendered in the current season's dominant warm color. Strokes draw from anchor toward option text — the ink is flowing FROM the memory. Duration: 0.6-1.0s.

3. **Option hover**: The hovered option's ink stroke warms to Vermilion Ink. A soft wash blooms behind the text. Other options dim slightly (opacity drops to 60-70%). Duration: 0.2-0.3s.

4. **Option selected** (confirm): The chosen option's stroke thickens and fully saturates. The anchor blooms with the outcome color (per Section 4.7). Unchosen options dissolve — their ink strokes thin, break, and fade to Light Ink Wash. The choice is irreversible. Duration: 0.6-1.2s for the full resolution.

5. **Memory rewrites** (transform): The memory painting shifts according to the choice outcome (per Section 4.7: preserved/lost/transformed/bittersweet). This is the longest UI event — the painting itself is the feedback. Duration: 2-6 seconds depending on outcome type.

6. **Return to exploration** (depart): Camera pulls back. Spirit returns to scroll distance. Periphery re-saturates to exploration levels. UI returns to minimum — chapter label and progress dots fade back in. Duration: 1.0-1.5s.

**Choice sequence pacing rule (UX constraint)**: The full 6-step sequence (up to 6s) plays on first encounter per choice per playthrough. On repeat encounters, the sequence collapses to a 3-step reduced version: anchor isolation (0.8s) → option select (0.5s) → memory rewrites (0.8s). Total repeat time: ≤2.1s. Pressing any navigation key during the sequence skips immediately to the final resolved state. The Reduced Motion accessibility setting (Section 7.7) overrides all choice animation durations globally, including the first-encounter full sequence.




---

### 7.7 Accessibility Notes

**One-line rule**: The brush welcomes every reader. The scroll must be legible to all who drift through it.

Building on Section 4.10 Colorblind Safety. The rules below are UI-specific extensions of the color safety system already defined.

#### Text Scaling

| Scale | Chinese Body Size | English Body Size | Behavior |
|---|---|---|---|
| **1.0x (default)** | 24-30px | 18-24px | As specified in 7.1. |
| **1.25x** | 30-38px | 22-30px | Menu layouts reflow. Line spacing increases proportionally. Multi-line text areas expand vertically. No text may overflow or clip. |
| **1.5x** | 36-45px | 27-36px | Maximum scale. Menu layouts may stack vertically rather than horizontally if needed. Simplified menu layouts acceptable — "New Journey" may become "New." Icon sizes do not scale with text (icons remain at fixed pixel sizes for brush-stroke integrity). |

**Text scaling rules**:
- All UI containers must support 1.5x text without overflow, clipping, or overlap.
- At 1.5x, menu item count per screen may need to reduce. Prioritize showing fewer items at full legibility over showing all items cramped.
- Micro-text (version, copyright) does not scale — it stays at 14-16px Chinese / 12-14px English. It is not gameplay-critical.
- Text scaling does not affect the memory painting (dialogue text within the painting scales; the painting's visual elements do not).

#### Colorblind Modes

All rules from Section 4.10 apply to UI. UI-specific extensions:

| Mode | UI Adjustment | Rationale |
|---|---|---|
| **Protanopia / Deuteranopia** (red-green) | Vermilion Ink accents shift toward a warm gold/orange that remains distinct from green-seasonal elements. Red-green problematic pairs (Section 4.10) add shape differentiation: vermilion interactive elements gain a distinctive double-stroke edge; green elements gain a single-stroke edge with a dot. | Vermilion `#A03828` and Fresh Green `#C5D8B5` must never be the only differentiator between interactive and non-interactive. |
| **Tritanopia** (blue-yellow) | Cool-blue UI elements (Slate Blue, Cold Blue) gain a dry-brush texture that warm elements do not have. Warm/cool differentiation through texture instead of hue. | Winter UI elements (cool) vs. autumn UI elements (warm) must be distinguishable without hue. |
| **Grayscale mode** | All UI elements must remain distinguishable through value contrast alone. Interactive elements must have ≥30% value difference from their background. Vermilion Ink wash behind selected text must read as a distinct value darkening. | The worst-case scenario — if all color is removed, can the player still use the UI? |

**General colorblind UI rules** (beyond Section 4.10):

1. **Interactive elements must always carry a shape/texture cue in addition to their color cue.** Hover = wash bloom (shape change) + vermilion shift (color change). Selection = wash deepen (shape) + saturation (color). Disabled = reduced opacity (value) + light ink wash (color). If the color is removed, the shape change alone must communicate the state.

2. **Choice outcome feedback** (Section 4.7) must include shape/animation differentiation alongside color shift. Preserved = anchor sharpens (detail increase) + warm saturation increase. Lost = anchor softens (detail decrease, edge dissolve) + cool desaturation. Transformed = anchor shifts hue through bloom animation + shape remains but color changes. Bittersweet = split rendering (warm core, cool edges visible as two distinct zones).

3. **Confirmation/completion must never rely on red/green color coding.** There is no "green = good, red = bad" in 回响. Confirmation = vermilion seal stamp (shape + color). Completion = filled dot (shape) in vermilion or gold (color). The shape communicates the state; the color communicates the emotional register. Removing color still leaves the shape.

#### Input Prompts

| Input Type | Visual Presentation | Behavior |
|---|---|---|
| **Keyboard** | Key glyph rendered as a small brushed character in a faint ink circle. Example: "ESC" appears as three small brush strokes forming the letters within a тонкий ink ring. | Glyph matches current binding. Updates if keys are rebound. Minimum legible size: 18px for the glyph. |
| **Mouse** | Left/Right click shown as brush-dot glyphs (filled = click, empty = release). Scroll wheel shown as a curved brush stroke with directional arrowhead. | Mouse glyphs are the smallest prompt type — 14-16px. |
| **Gamepad** | Face buttons rendered as ink circles with brushed labels (A/B/X/Y become stylized brush marks within circles). D-pad rendered as four directional brush strokes. Shoulder buttons as horizontal strokes at top of glyph. | Gamepad glyphs must be visually distinct from each other in grayscale. Use shape, not color. |
| **Touch** | Tap shown as a single ink dot with expanding ring. Swipe shown as a directional brush stroke. Pinch shown as two dots with converging/diverging strokes. | For potential future platforms. |

**Prompt placement**: Prompts appear near the relevant UI element or interactive anchor. They are contextual — they appear on first encounter and fade after 3-5 seconds. They reappear if the player idles for 10+ seconds without input. Prompts are suggestions, not instructions — their absence should not block gameplay.

#### Reduced Motion Mode

| Setting | Effect |
|---|---|
| **Full animation** (default) | All UI animations play as specified in 7.3. |
| **Reduced** | Animations are shortened to 30-50% of their default duration. Ink-spread becomes fast bloom. Brush-stroke draw becomes instant (0.1s). Micro-animations (pulses, idle effects) are disabled. Scroll-roll transitions become 0.3s crossfades. |
| **None** | All UI transitions replaced with 0.1s crossfades. No animation. The brush language is communicated through static rendering — ink-wash edges, variable line weight, seal-stamp shapes — rather than through motion. |

**Reduced motion rule**: Reduced motion must not make the UI feel "broken" or "cheap." The brush aesthetic survives through static qualities — irregular edges, organic shapes, variable weight, ink-wash textures. The player who disables motion still experiences a hand-painted scroll; it is simply a still scroll.

#### Additional Accessibility

| Feature | Specification |
|---|---|
| **High contrast mode** | All text rendered at maximum opacity. Interactive element outlines rendered with an additional 1px darker stroke for edge definition. Background overlay opacity increased for menu screens (60% → 80%) to ensure text contrast. |
| **Screen reader support** | All menu text, button labels, and interactive element descriptions must have accessible text labels. Navigation order must be logical (top-to-bottom, left-to-right per scroll convention). Memory painting descriptions (alt text) provided for key scenes. |
| **Input remapping** | All keyboard and gamepad inputs are remappable. Default bindings displayed in settings. |
| **Subtitle options** | Subtitle on/off. Subtitle background: none (default — text is on the scroll surface), light wash (accessibility option — faint ink wash behind subtitles for contrast). Subtitle speaker indicators: colored brush dot matching speaker's color thread. |
| **Idle timeout warnings** | If the player is idle for 60+ seconds during a choice moment, a subtle pulse animation on the choice options gently reminds without urgency. No countdown — this game has no time pressure. |

---

### 7.8 Design Tests

**Test 1 — 3-Second UI Comprehension (New Player)**:
Present the Main Menu screen to 5 participants who have never seen the game before. Task: without instruction, identify (a) what the game is called, (b) how to start playing, and (c) what kind of game this is (mood/genre). Pass: 5/5 identify the title and start action within 3 seconds; 4/5 correctly identify the game as contemplative/narrative (not action) from visual cues alone. If any participant cannot find "New Journey" within 3 seconds, the menu layout has failed.

**Test 2 — Grayscale Interaction Identification**:
Present three screens (Main Menu, Pause Menu, and an Exploration scene with interaction prompts) in full grayscale — all color information removed. 5 participants. Task: identify every interactive element on each screen. Pass: 4/5 correctly identify all interactive elements across all three screens. If hover/selection states rely on vermilion color shift without sufficient shape/value change, this test will expose it.

**Test 3 — 1.5x Text Scaling Integrity**:
Apply 1.5x text scaling to all menu screens (Main Menu, Pause, Settings, Chapter Select) and to an Exploration scene with dialogue text. Inspect every screen. Pass: zero instances of text overflow, clipping, overlap, or truncation. Any failure requires layout reflow or menu redesign for that screen.

**Test 4 — Animation Duration Ceiling**:
Instrument and measure every UI transition animation in the game (menu open, menu close, selection change, choice option appear, confirmation, screen transition). Pass: no single UI animation exceeds 1.5 seconds. Total time from input to fully-resolved new state (e.g., click "Settings" to Settings fully usable) must not exceed 2.0 seconds.

**Test 5 — Choice Outcome Without Color**:
Create four 4-second clips of choice outcomes (preserved, lost, transformed, bittersweet) rendered in grayscale. Remove all text labels. 5 participants. Task: identify which outcome type each clip represents. Pass: 4/5 correctly identify preserved and lost. 3/5 correctly identify transformed and bittersweet. If grayscale viewers cannot distinguish outcomes, the shape/texture/animation differentiation (per 7.7 colorblind rules) is insufficient.

**Test 6 — Brush-Language Control Identification (Settings Screen)**:
Present the Settings screen to 5 participants. Without instruction, ask them to identify: (a) a toggle control, (b) a slider control, and (c) how to go back to the previous screen. Pass: 5/5 identify the back action within 3 seconds; 4/5 correctly identify toggle and slider as interactive controls (even if they don't know the exact function). If participants do not recognize brush-language controls as controls, the visual metaphor has failed for functional UI.

---

## 8. Asset Standards -- 资产规范

**One-line rule**: Every asset must survive inspection at native resolution. The brushstroke that left the artist's hand must reach the player's screen without a single pixel of compromise.

---

### 8.1 File Formats & Export Pipeline

| Stage | Format | Specification |
|---|---|---|
| **Source** | `.psd` | Layered master files. One `.psd` per asset variant. Never flatten source layers -- preserve brush strokes on separate layers for future revision. |
| **Export** | `.png` (PNG-24) | RGBA 8-bit, lossless. Every asset that reaches Unity must be PNG-24. |
| **Color space** | sRGB | Embedded sRGB profile. Consistent across all source and export. |
| **Alpha** | Straight (unassociated) | Alpha channel stores opacity only. RGB channels store full color, not color-multiplied-by-alpha. Premultiplied alpha creates dark halos at soft ink-wash edges where RGB and A both approach zero at different rates. Straight alpha preserves the feathered brush quality. |
| **Anti-aliasing** | Off at export | Brush edges carry their own feathered quality from the brush tool itself. Export AA would homogenize this natural edge variation. |

**Forbidden**:
- JPEG at any stage -- compression artifacts destroy thin ink-line texture and introduce block noise in wash gradients
- Premultiplied alpha exports
- Export-time anti-aliasing or resampling
- Any format conversion that strips the sRGB profile

---

### 8.2 Naming Conventions

All assets follow category-specific schemas derived from the character naming convention (Section 5.8). Every field is lowercase, underscore-delimited, ASCII only.

| Category | Schema | Example |
|---|---|---|
| **Characters** | `char_[figure]_[season]_[distance]_[variant].png` | `char_spirit_spring_scroll_drift.png` |
| **Backgrounds** | `bg_[location]_[season]_[layer]_[variant].png` | `bg_home_spring_l2_base.png` |
| **Environment Props** | `env_[type]_[name]_[season]_[scale].png` | `env_furn_table_autumn_med.png` |
| **UI Elements** | `ui_[category]_[element]_[state].png` | `ui_btn_newjourney_hover.png` |
| **VFX Sprites** | `vfx_[type]_[name]_[season]_[f000].png` | `vfx_wash_inkbloom_spring_f003.png` |
| **Animation Frames** | `anim_[subject]_[action]_[season]_[f000].png` | `anim_spirit_idle_spring_f000.png` |
| **Spritesheets** | `ss_[subject]_[action]_[season]_[WxH].png` | `ss_spirit_idle_spring_512x128.png` |
| **Overlays** | `overlay_[type]_[season]_[variant].png` | `overlay_vignette_winter_heavy.png` |

**Field dictionaries**:

| Field | Allowed Values |
|---|---|
| `figure` | `spirit`, `fig01`--`fig06` (from cast list) |
| `season` | `spring`, `summer`, `autumn`, `winter`, `base` (menu/transition) |
| `distance` | `scroll` (exploration), `pushin` (choice moment) |
| `location` | Short slug from level design: `home`, `garden`, `temple`, `street`, `river`, `mountain`, `school` |
| `layer` | `l1`, `l2`, `l3`, `l4` |
| `type` (env) | `arch` (architecture), `nat` (natural), `furn` (furniture), `obj` (object) |
| `type` (vfx) | `particle`, `wash`, `bloom`, `transition`, `thread` |
| `scale` (env) | `small` (under 128px), `med` (128--512px), `large` (512px+) |
| `category` (ui) | `icon`, `btn`, `panel`, `prompt`, `indicator`, `menu` |
| `state` (ui) | `default`, `hover`, `active`, `disabled`, `selected`, `confirmed` |
| `variant` | `base`, `choice`, `loss`, `preserved`, `transition`, `closing`, `light`, `heavy` |
| `f000` | Zero-padded frame number: `f000`, `f001`, ... `f007` |

---

### 8.3 Resolution & Detail Tiers

All resolutions specified for 1080p target (1920x1080). Assets authored at 1x display size with 10--20% margin -- never upscaled algorithmically.

| Asset Category | Subcategory | Authored Resolution | On-Screen Size | Notes |
|---|---|---|---|---|
| **Characters** | Spirit (scroll) | 256 px tall | 54--86 px (5--8% frame) | Proportional width; hand-authored |
| | Spirit (pushin) | 768 px tall | 270--378 px (25--35% frame) | Never algorithmically scaled from scroll asset |
| | Memory figure (scroll) | 384 px tall | ~100--150 px | Larger than spirit for cast recognizability |
| | Memory figure (pushin) | 1024 px tall | ~350--500 px | Face-readable at choice distance |
| **Backgrounds** | L1 Foreground | 3840--5760 x 1080 px | 1920 x 1080 viewport | Width = fragment scroll range (2--3x) |
| | L2 Midground | 3840--5760 x 1080 px | 1920 x 1080 viewport | Highest detail plane |
| | L3 Background | 3840--5760 x 1080 px | 1920 x 1080 viewport | Stroke density: 3--8 per element |
| | L4 Far Background | 3840--5760 x 1080 px | 1920 x 1080 viewport | Stroke density: 1--5 per element |
| **Environment Props** | Small (cups, leaves, scrolls) | 128--256 px max | 64--192 px | Power-of-two dimensions |
| | Medium (furniture, doors, trees) | 256--512 px max | 128--384 px | Power-of-two dimensions |
| | Large (buildings, hero trees) | 512--1024 px max | 256--768 px | Hero-anchor props may exceed |
| **UI** | Icons | 32, 48, 64, 96 px square | Fixed 1:1 pixel | Never scaled; exact pixel placement |
| | Buttons / prompts | 48--64 px tall | Fixed height | Variable width; power-of-two height |
| | Full-screen overlays | 1920 x 1080 px | Full frame | Menu backgrounds, vignettes |
| **VFX** | Particles (ink dots, dust) | 16--32 px square | Exact 1:1 | Power-of-two |
| | Washes / blooms | 64--256 px square | 64--256 px | May composite via tiling |
| | Transitions | 512 px square | Full or partial frame | Ink-thread, wash transitions |
| **Anim Frames** | Character micro-anims | 128--256 px square | Frame-size dependent | 4--8 frames per loop |
| | Environmental micro-anims | 64--256 px max | Varies by element | Branch sway, water ripple, dust |
| **Overlays** | Vignette, haze, borders | 1920 x 1080 px | Full frame | May tile horizontally for scroll-wide fragments |

**Resolution rule**: No asset may be authored below its minimum on-screen size. The authored resolution is the quality ceiling -- assets are displayed at 1x or smaller, never larger.

---

### 8.4 LOD & Push-In Strategy

Only spirit and memory figures have two discrete LODs (hand-authored scroll-distance and pushin-distance assets). Background layers are single-resolution with filtering adjustments.

| Asset Group | LOD Count | Scaling Behavior | Filter Mode | Mip Maps |
|---|---|---|---|---|
| **Spirit** | 2 (scroll, pushin) | Swap at push-in trigger. Scroll asset pops to pushin asset at the 0.3s mark of the 1.5--2s push-in. No crossfade. | Bilinear | Off |
| **Memory Figures** | 2 (scroll, pushin) | Same swap behavior as spirit. Synchronized to push-in timeline. | Bilinear | Off |
| **L1 Foreground** | 1 | Scales up 5--10% during push-in to simulate depth. | Bilinear | On |
| **L2 Midground** | 1 | Stays 1:1 -- reference plane. No scale change during push-in. | Point | Off |
| **L3 Background** | 1 | Scales down 5--10% during push-in. | Bilinear | On |
| **L4 Far Background** | 1 | Scales down 5--10% during push-in. | Bilinear | On |
| **Environment Props** | 1 | Inherit parent layer's scaling behavior. | Bilinear | Off |
| **UI** | 1 | Never scales. Fixed pixel placement. | Bilinear | Off |
| **VFX** | 1 | May scale with parent layer. | Bilinear | Off |

**Filter mode rationale**:
- **Point (L2 only)**: L2 stays at exactly 1:1 pixel mapping. Bilinear would soften brush-stroke edges. Point preserves the artist's exact pixel -- every brush hair renders as authored.
- **Bilinear (L1, L3, L4, characters, UI)**: These scale during push-in or parallax drift at non-integer pixel positions. Bilinear prevents crawling/jitter artifacts on thin ink lines during subpixel movement.

**Mip map rationale**: L1, L3, L4 scale during push-in and shift continuously during parallax -- mip maps prevent moire on high-frequency brush texture at reduced scales. L2 stays 1:1 and uses Point filtering -- mip maps would never be sampled and only waste memory. Characters render at or below authored resolution -- mip maps unnecessary.

---

### 8.5 Unity Import Specifications

| Setting | Characters | L1 Foreground | L2 Midground | L3 Background | L4 Far Bg | Props | UI | VFX | Overlays |
|---|---|---|---|---|---|---|---|---|---|
| **Texture Type** | Sprite (2D and UI) | Sprite (2D and UI) | Sprite (2D and UI) | Sprite (2D and UI) | Sprite (2D and UI) | Sprite (2D and UI) | Sprite (2D and UI) | Sprite (2D and UI) | Sprite (2D and UI) |
| **Sprite Mode** | Single | Single | Single | Single | Single | Single | Single | Multiple | Single |
| **Mesh Type** | Tight | Full Rect | Full Rect | Full Rect | Full Rect | Tight | Full Rect | Full Rect | Full Rect |
| **Pixels Per Unit** | 100 | 100 | 100 | 100 | 100 | 100 | 100 | 100 | 100 |
| **Filter Mode** | Bilinear | Bilinear | Point | Bilinear | Bilinear | Bilinear | Bilinear | Bilinear | Bilinear |
| **Compression** | None | BC7 | None | BC7 | BC7 | None | None | None | None |
| **Mip Maps** | Off | On | Off | On | On | Off | Off | Off | Off |
| **sRGB** | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes |
| **Alpha Source** | Input Texture Alpha | Input Texture Alpha | Input Texture Alpha | Input Texture Alpha | Input Texture Alpha | Input Texture Alpha | Input Texture Alpha | Input Texture Alpha | Input Texture Alpha |
| **Alpha Is Transparency** | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes |
| **Max Size** | 1024 | 4096 | 4096 | 4096 | 4096 | 1024 | 1024 | 512 | 2048 |
| **Wrap Mode** | Clamp | Clamp | Clamp | Clamp | Clamp | Clamp | Clamp | Clamp | Clamp |

**Compression rationale**:
- **None** for characters, L2, props, UI, VFX, overlays: These assets contain thin ink lines (1--3px stroke width). BC7 block compression at 4x4 pixel blocks visibly damages stroke terminals and creates block-boundary artifacts on feathered edges. L2 is the primary gameplay plane (spirit, memory figures, interactive anchors, main architecture) -- brushstroke fidelity on this layer is non-negotiable per the "every frame is a painting" rule (Section 1). The memory cost of uncompressed RGBA for these categories is acceptable at 1080p target.
- **BC7** for L1, L3, L4 backgrounds: These are large canvases (3840--5760 x 1080 RGBA = 16--25 MB each uncompressed). BC7 provides approximately 3:1 compression with minimal visible quality loss on broad wash areas. L1's foreground occlusion and atmospheric haze mask compression artifacts; L3/L4's low stroke density (3--8 and 1--5 strokes per element) and atmospheric perspective make BC7 artifacts imperceptible at gameplay distance.

**Mesh Type rationale**:
- **Tight** for characters and props: Generates minimal transparent overdraw. The sprite mesh conforms to the brush silhouette.
- **Full Rect** for backgrounds and UI: These fill large rectangular regions. Tight mesh on a 5760x1080 canvas would generate excessive vertex counts for negligible overdraw savings.

---

### 8.6 Animation & Sprite Sheet Standards

| Parameter | Specification | Rationale |
|---|---|---|
| **Frame rate** | 8--12 fps | Smooth animation (24/30/60 fps) contradicts the ink painting aesthetic. The brushstroke is a held gesture, not a film frame. At 8--12 fps, the player perceives motion while still reading each frame as a brush mark. |
| **Frames per loop** | 4--8 frames | At 8 fps: 0.5--1.0s loop. At 12 fps: 0.33--0.67s loop. Micro-animations are breath-length, not narrative-length. |
| **Sprite sheet layout** | Horizontal strip (single row) | Frames arranged left-to-right. 2px transparent padding between each frame to prevent edge bleed during bilinear sampling of adjacent frames. |
| **Sprite sheet dimensions** | Power-of-two | Both width and height must be powers of two (128, 256, 512, 1024, 2048). Non-power-of-two textures degrade or fail on some GPU architectures. |
| **Frame dimensions** | Power-of-two per frame cell | Each frame occupies an equal-sized cell. If artwork does not fill the cell, it is centered with transparent padding. |
| **Frame ordering** | Left-to-right, loop wraps | Frame 0 is the "rest" or neutral pose. The loop plays forward and wraps from last frame back to frame 0 without a reverse playback. |

**Sprite sheet dimension calculator** (horizontal strip, 2px padding):

| Frames | Frame Size | Strip Width (raw) | Next Power of Two | Final Sheet |
|---|---|---|---|---|
| 4 | 128 x 128 | 128x4 + 2x3 = 518 | 1024 | 1024 x 128 |
| 6 | 128 x 128 | 128x6 + 2x5 = 778 | 1024 | 1024 x 128 |
| 8 | 128 x 128 | 128x8 + 2x7 = 1038 | 2048 | 2048 x 128 |
| 4 | 256 x 256 | 256x4 + 2x3 = 1030 | 2048 | 2048 x 256 |
| 6 | 64 x 64 | 64x6 + 2x5 = 394 | 512 | 512 x 64 |
| 8 | 64 x 64 | 64x8 + 2x7 = 526 | 1024 | 1024 x 64 |

**When horizontal strip exceeds 2048 width**: Switch to a 2-row grid layout. Place frames 0--3 on row 1, frames 4--7 on row 2. Maintain 2px padding between all adjacent frames, both horizontal and vertical.

**Animation scope -- what animates**:
- Spirit idle (breathing wash shift): 6 frames, 10 fps, 256x256
- Spirit drift (body lean cycle): 4 frames, 8 fps, 256x256
- Memory figures idle (subtle posture shift): 4 frames, 8 fps, 384x384
- Environmental micro-animation (branch sway, water ripple, dust mote): 4--6 frames, 8--10 fps, 64--256 px
- UI element feedback (ink dot pulse, selection bloom): 4 frames, 10 fps, 32--96 px

**What does NOT animate**: Background layers L2--L4 (still scroll painting), architectural elements, static props, UI panels and text.

---

### 8.7 Quality Gates

Every asset must pass all applicable gates before import into Unity. Checks are performed at three stages: source (PSD), export (PNG), and import (Unity Inspector).

#### Gate 1: Brush-Line Fidelity

| Check | Method | Threshold |
|---|---|---|
| No JPEG or lossy artifacts | Visual inspection at 400% zoom | Zero block artifacts, zero ringing at stroke edges |
| Thin lines survive at native resolution | View asset at 1:1 display size | All 1--2px brush strokes are distinct and unbroken |
| No export anti-aliasing | Visual inspection of stroke edges at 400% | Edge pixels show natural brush feathering, not uniform stair-step smoothing |

#### Gate 2: Seasonal Palette Compliance

| Check | Method | Threshold |
|---|---|---|
| Dominant hue falls within season range | Color picker sample of 5 random non-transparent pixels | 4 of 5 within seasonal hue/saturation range (Section 4) |
| No forbidden colors | Check against Section 4 violation list | Zero pure black (#000000), pure white (#FFFFFF), or 100% saturated primaries |
| Warm/cool ratio compliant | Area analysis (tool-assisted) | Per seasonal compositional rules (Sections 4.1--4.4) |

#### Gate 3: Stroke Density Budget

| Check | Method | Threshold |
|---|---|---|
| Character asset stroke count | Count distinct brush marks in PSD | Scroll: 15--25 strokes. Pushin: 30--50 strokes |
| Environment prop stroke count | Count per asset | Small: ≤8. Medium: ≤20. Large: ≤50 (hero props) |
| Background layer density | Spot-check 3 regions per layer | L1: 5--15/element. L2: per hierarchy (Section 3). L3: 3--8/element. L4: 1--5/element |

#### Gate 4: Edge Treatment per Layer

| Check | Method | Threshold |
|---|---|---|
| L1 edges dissolve on inner side | Visual inspection | Inner edge (facing L2) shows ink-wash fade; no hard occlusion boundary |
| L2 edges are defined | Visual inspection at 1x | Contour lines present; no unintended feather-to-nothing |
| L3/L4 edges are soft | Visual inspection | No hard contour lines; edges defined by wash shape only |

#### Gate 5: Naming Convention

| Check | Method | Threshold |
|---|---|---|
| Schema compliance | Regex validation against category schema (8.2) | 100% of assets match their category pattern |
| No uppercase, spaces, or special characters | Automated filename check | Zero violations |
| Season field matches asset content | Manual spot-check (1 per 20 assets) | 100% match rate |

#### Gate 6: Technical Compliance

| Check | Method | Threshold |
|---|---|---|
| Power-of-two dimensions | Inspector or file properties | 100% of non-fullscreen assets |
| PNG-24 RGBA format | File header check | 100% of imported assets |
| sRGB color profile embedded | File metadata | 100% of exports |
| Straight alpha (not premultiplied) | Check for dark halos on 50% opacity edges at 400% zoom against #808080 background | Zero halo artifacts |
| 8-bit per channel (not 16-bit) | File metadata | 100% compliance |
| Unity import settings match 8.5 table | Inspector verification | 100% per asset type |

#### Gate 7: Animation Compliance

| Check | Method | Threshold |
|---|---|---|
| Frame count 4--8 | Count frames in spritesheet or sprite set | 100% |
| Frame rate 8--12 fps | Check animation clip settings in Unity | 100% |
| Spritesheet padding 2px | Measure gap between frames at 400% zoom | Exactly 2px transparent gap on all sides |
| Power-of-two sheet dimensions | Inspector | 100% |
| No single-frame "animations" | Review animation clips | Zero single-frame clips labeled as animations |

---

### 8.8 Design Tests

**Test 1 -- Brush-Stroke Survival at Native Resolution**:
Open 20 randomly selected character and prop assets at 1:1 display size (100% zoom) on a 1080p monitor. Task: identify any stroke that appears broken, aliased, or compression-damaged. Pass: 0 of 20 assets show visible degradation. Any failure requires re-export from PSD with corrected settings.

**Test 2 -- Seasonal Palette Blind Sort**:
Export 40 prop and character assets (10 per season) with filenames stripped of season identifiers. Present to 3 team members unfamiliar with the specific assets. Task: sort into four seasonal groups. Pass: 35 of 40 correctly sorted (87.5%). Mis-sorts between spring/autumn are acceptable; summer/winter mis-sorts are not.

**Test 3 -- Push-In LOD Readability**:
Display a spirit figure at scroll-distance resolution on a 1080p screen. Trigger a simulated push-in that swaps to the pushin-distance asset at the 0.3s mark. 5 observers. Task: identify the moment the asset swaps. Pass: 0 of 5 detect the swap as a "pop" or discontinuity. The transition should read as "coming into focus," not "changing resolution."

**Test 4 -- Layer Filtering Artifact Check**:
Pan a full four-layer memory fragment horizontally at exploration drift speed (spring rate, 0.8x). Record 10 seconds at 60fps capture. Review frame-by-frame. Pass: zero instances of moire, crawling edges, or shimmer on L1, L3, or L4. L2 must show zero bilinear softening on brush edges at any frame.

**Test 5 -- Animation Frame-Rate Aesthetic**:
Render the same spirit idle animation at 8 fps, 12 fps, 24 fps, and 60 fps. Present all four side-by-side to the creative director. Pass: 8 or 12 fps is selected as "most appropriate for the ink painting aesthetic." 24/60 fps must be rejected as "too smooth -- feels like a cartoon, not a painting."

**Test 6 -- Compression A/B for Ink Lines**:
Export one character asset and one L2 background section. Create two Unity imports: one with the specified compression (None for character, BC7 for L2), one with the opposite. View both at 400% zoom. Pass: the specified compression shows superior or equivalent quality to the alternative. If BC7 creates visible block artifacts on ink lines in the L2 sample, the compression strategy must be re-evaluated.

**Test 7 -- Alpha Halo Audit**:
Open 30 assets with soft-edged brushwork (spirits, ink washes, L1 foreground edges) on a middle-gray (#808080) background in Unity. Inspect each at 200% zoom. Pass: zero assets show dark or light halos at the transition between opaque and transparent regions. Any halo indicates premultiplied alpha contamination in the export pipeline.

---

### 8.9 Unity 6.3 Import Pipeline — Gotchas and Flags

Settings where Unity 6.3 LTS (Dec 2025) differs from what a May 2025 LLM cutoff would assume:

| Area | Pre-6 Assumption | Unity 6.3 Reality | Risk |
|---|---|---|---|
| Addressables error handling | `LoadAssetAsync` returns null on failure | Throws by default — use try/catch or `TryLoad` variants | **HIGH** |
| Crunch compression library | Pre-6 Crunch algorithm | Updated library in Unity 6 — re-validate quality levels (25/50/75/100) on brushstroke assets | MEDIUM |
| Sprite Atlas version | Sprite Atlas v1 | v2 may be available — verify Tight-mesh sprites don't develop edge artifacts when packed | MEDIUM |
| URP 2D mipmap bias | Default zero bias | Bias may have changed — verify sprites at 1:1 pixel mapping; adjust 2D Renderer asset if softer than expected | LOW |
| `Texture2D.DuplicateTexture()` | Works | Deprecated — migrate custom asset scripts to `GetRawTextureData` / `LoadRawTextureData` | LOW |
| Addressables group schema | 1.x schema fields | 2.x may rename schema fields — do not assume old field names | MEDIUM |

**Critical action item:** All asset-loading code must wrap `Addressables.LoadAssetAsync<T>()` in try/catch or use `TryLoad` variants. This is a breaking change from Unity 6.2+ (see `docs/engine-reference/unity/breaking-changes.md`).

---

### 8.10 Memory Budget per Asset Category

| Category | Budget | Breakdown |
|---|---|---|
| Background layers (active scene) | 28 MB | L2: 15.8 MB uncompressed; L1/L3/L4: 3.95 MB each |
| Character assets (active scene) | 15 MB | ~5 figures at 2-3 MB each uncompressed (all variants) |
| Paper grain overlay | 5 MB | 2048x2048 BC7 + mip chain |
| UI assets (resident) | 8 MB | Single UI atlas, font textures |
| VFX assets (active scene) | 5 MB | Particle sprite atlases, ink-drop sheets |
| Audio (active scene) | 15 MB | Streaming ambient + loaded SFX |
| Engine overhead | 30 MB | Unity runtime, URP, Addressables catalog, script memory |
| Headroom | 14 MB | Buffer for spikes, future content |
| **Active Scene Total** | **120 MB** | Ceiling |
| **Global pool (remaining)** | ~1,880 MB | Other chapters, shared assets, OS, Editor |

**Enforcement:** Any texture exceeding 4096 on any axis triggers a CI error. Any texture exceeding 20 MB uncompressed requires art-director sign-off annotation.

---

---

### 8.11 Sprite Atlas Configuration

| Asset Category | Atlased? | Strategy | Rationale |
|---|---|---|---|
| Background layers (L1-L4) | No | Standalone sprites | A single 3840x1080 layer already fills most of a 4096 atlas. Combining layers exceeds atlas limits and breaks per-layer Addressables loading. |
| Character sprites | Yes — per figure | One atlas bundles all seasons + variants for one figure (~8-16 sprites) | Loads atomically when the figure spawns. ~4-8 MB per atlas. |
| UI elements | Yes — single atlas | All UI sprites in one 2048x2048 atlas | UI sprites are small and always needed. Stays resident. |
| VFX sprites | Yes — per type | One atlas per VFX category (ink drops, wash blooms, seal stamps, atmosphere) | Each VFX system needs its full set simultaneously. Per-type avoids loading unused sprites. |
| Paper grain | No | Standalone, tiled | Requires Repeat wrap mode; atlased sprites cannot tile. |
| Animation sheets | N/A | Self-atlased by definition | Already packed sprite sheets. |

**Atlas settings:** Tight Packing enabled (except FullRect sprites); 4px minimum padding; Include in Build via Addressables.

**Draw-call estimate (typical exploration scene):** 4 background layers + 2-5 character atlases + 1 UI atlas + 1-2 VFX atlases + 1 paper grain = **9-13 draw calls** (well within the 50-100 budget).

---

---

### 8.12 Addressables Group Recommendations

Organized by **chapter and layer** with shared assets in dedicated groups.

| Group | Content | Load Trigger | Unload Trigger |
|---|---|---|---|
| `Shared_UI` | UI atlas, font textures | App start | Never |
| `Shared_PaperGrain` | Paper grain overlay | App start | Never |
| `Shared_VFX` | Common VFX atlases | App start | Never |
| `Shared_Audio_Base` | Core ambient, UI SFX | App start | Never |
| `Ch01_L1` through `Ch04_L4` | Background layers (16 groups) | Chapter load | Chapter unload (with crossfade buffer) |
| `Ch01_Characters` through `Ch04_Characters` | Character atlases per chapter | Chapter load | Chapter unload |
| `Ch01_Props` through `Ch04_Props` | Chapter-specific props | Chapter load | Chapter unload |
| `Ch01_Audio` through `Ch04_Audio` | Chapter ambient audio | Chapter load | Chapter unload |

**Why per-chapter-per-layer:** During memory transitions, L1/L2 dissolve while L3/L4 continue parallax at 0.5x rate (per Section 6.7). Per-layer granularity allows unloading foreground layers before loading the next chapter's layers — this minimizes peak memory during the most demanding operation (cross-chapter transition).

**Group settings:** Bundle Mode = Pack Separately; Bundle Naming = Use group name; Load Path = `[BuildTarget]` (local only — no remote content for PC).

---

---

### 8.13 Build Stripping and Asset Bundle Settings

| Setting | Value | Rationale |
|---|---|---|
| Strip engine code | Enabled | Standard for IL2CPP builds |
| Managed stripping level | Medium | "High" may strip reflection types used by UI Toolkit and Addressables |
| Texture mipmap stripping | **Disabled** | Background layers use mipmaps for parallax scaling during push-in moments. Without mipmaps, scaling produces shimmer on fine brushstroke detail. |
| Asset bundle compression | **LZ4** | Block-level decompression without full-bundle cost of LZMA. Critical for background layers — LZMA would require decompressing the full 15.8 MB L2 before use; LZ4 streams only needed blocks. |
| Exclude from build | Editor-only assets, `.psd`/`.ai`/`.clip` source files (store in `assets_src/` outside Unity project), EditorOnly-labeled test assets |
| Build report | Generate after every build; audit for unexpected asset inclusions |

---

#### Asset Validation Rules (CI/CD)

Run on every push and PR. Implement as Unity Editor tests or standalone scripts targeting `Assets/`.

#### Texture Validation

| ID | Check | Threshold | Severity |
|---|---|---|---|
| TX-01 | Max texture dimension | No texture > 4096 on longest axis | Error |
| TX-02 | Background layer resolution | `bg_L*` textures must be 3840x1080 (+/- 1px) | Warning |
| TX-03 | Uncompressed size cap | RGBA 32-bit textures > 20 MB require approval annotation in `.meta` | Warning |
| TX-04 | Compression by folder | `Assets/Backgrounds/L2/` = None; `L[134]/` = BC7 (per Section 8.5) | Error |
| TX-05 | Paper grain tiling | `paper_grain` WrapMode must be Repeat | Error |
| TX-06 | UI compression | `Assets/UI/` = RGBA 32-bit | Error |
| TX-07 | Mipmap generation | Background layers must have mipmaps enabled | Warning |
| TX-08 | sRGB color space | Color textures = sRGB; data textures = Linear | Error |

#### Naming and Format Validation

| ID | Check | Threshold | Severity |
|---|---|---|---|
| NM-01 | Character naming | Must match `char_[figure]_[season]_[distance]_[variant].png` (per Section 5.8) | Error |
| NM-02 | Background naming | Must match `bg_[location]_[season]_[layer]_[variant].png` (per Section 8.2) | Error |
| NM-03 | UI naming | Must match `ui_[category]_[element]_[state].png` (per Section 8.2) | Warning |
| NM-04 | VFX naming | Must match `vfx_[type]_[name]_[season]_[f000].png` (per Section 8.2) | Warning |
| NM-05 | No source files | No `.psd`, `.ai`, `.clip`, `.kra` in `Assets/` | Error |
| NM-06 | Lossless format | All 2D assets must be `.png` | Error |

#### Atlas and Budget Validation

| ID | Check | Threshold | Severity |
|---|---|---|---|
| AT-01 | Atlas size limit | No atlas > 4096x4096 | Error |
| AT-02 | Atlas padding | Minimum 4px between sprites | Warning |
| BD-01 | Chapter bundle size | Sum of all groups per chapter < 150 MB compressed | Warning |
| BD-02 | Active texture memory | Sum of 10 largest textures (uncompressed) < 50 MB | Warning |
| BD-03 | UI atlas cap | UI atlas must not exceed 2048x2048 | Error |

---## Section 9: Reference Direction — 参考方向

**One-line rule**: Study the masters to understand the brush; then set them aside. The scroll you paint has never been painted before.

---

**9.1 "Feeling from Mountain and Water" (山水情, 1988) -- Dir. Te Wei**

**What it solves:** How ink moves. How stillness and motion coexist in a brush-based frame.

This 18-minute animated short is the definitive achievement of Chinese ink-wash animation. Every frame is a painting; every painting breathes. Trees sway in wind rendered as 3-4 brush strokes shifting subtly; water moves as negative space disturbed by ink lines; a figure's sleeve lifts in a breeze communicated through two strokes changing weight.

**Draw from:** The micro-animation vocabulary -- how ink-wash images transition from still to moving without losing their "painting-ness." The specific technique of animating by redrawing the brushstroke (not tweening or transforming a static image) so that every frame of animation carries its own brush gesture. The ratio of stillness to motion: in 山水情, a character may hold still for 4-5 seconds while only the background element moves -- this is the direct ancestor of 回响's 9:1 stillness-to-motion ratio (Section 1, Principle 1). Also: the use of ink-wash dissolves (not cuts) between scenes -- the direct ancestor of 回响's memory transition ink-bleed.

**Diverge from:** 山水情 is landscape-dominant with minimal figure detail -- figures are often at minimum-viable-person density. 回响 needs Memory Figures at Hero-level stroke density with full visual signatures (Section 5.2). 山水情 is also purely instrumental (music-only, no text) -- 回响 must integrate calligraphic text as a first-class visual element. And 山水情's color palette is limited to ink-and-wash monochrome with rare sepia warmth -- 回响's four-season color system (Section 4) requires far more chromatic range.

---

**9.2 Tang Dynasty Figure Painting -- 周昉 (Zhou Fang) and 张萱 (Zhang Xuan)**

**What it solves:** What Tang Dynasty visual culture actually looks like -- the material truth behind the brush.

Zhou Fang's "Court Ladies Adorning Their Hair" (簪花仕女图) and Zhang Xuan's "Spring Outing of the Tang Court" (虢国夫人游春图) are the primary visual documents of Tang material culture: architectural details, furniture types, garment construction, hair ornamentation, screen and scroll formats, the specific objects that populate a Tang interior.

**Draw from:** Specific architectural elements already named in Section 6.1 -- the proportions of a Tang bracket set (斗拱), the curve radius of flying eaves (飞檐), the relationship between platform (台基), pillar (柱), and roof. The way fabric drapes on Tang figures -- heavier silks with specific fold patterns, sash (披帛) that floats rather than hangs. The specific material objects: the shape of a Tang ceramic vessel (the sancai 三彩 glaze colors of amber/green/cream referenced in Section 6.6 Example 2), the structure of a low table (案几), the proportions of a standing screen (屏风). The way Tang painters organized figure groups in space -- figures spaced with deliberate negative space between them, each figure a complete visual statement, no overlapping crowds.

**Diverge from:** Tang court painting depicts aristocracy -- lavish garments, elaborate ornaments, formal settings. 回响's memories are of everyday life: a child's home, a scholar's study, a street, a garden. The material vocabulary is Tang but the social register is domestic, not courtly. Also: Tang figure painting uses the "outline-then-fill" (勾线填色) technique with relatively uniform line weight and flat color fields. 回响's six brush technique system (Section 3) requires far more expressive, variable brushwork -- dry brush, wet wash, broken ink. The Tang outline is a container; the 回响 brushstroke is an emotional event.

---

**9.3 "Gris" (Nomada Studio, 2018)**

**What it solves:** How color carries emotional narrative in an interactive, text-minimal game context.

Gris is a 2D watercolor-platformer about grief in which color is not decoration but progression: the world begins in monochrome and gains colors one by one as the protagonist processes stages of loss. Each new color is a mechanical and emotional unlock. The game also demonstrates how UI can dissolve into the art surface -- there is no HUD, no persistent UI chrome, no floating text.

**Draw from:** The structural relationship between color and emotional arc -- how the game maps specific hues to specific emotional states and transitions between them as narrative punctuation, not background atmosphere. This is the closest existing implementation of 回响's Principle 2 (颜色即是情绪 / Color Is Emotion). Also: the confidence to let the art carry the story without text explanation. Gris's visual storytelling -- a figure's posture, a color shift, a structural collapse -- communicates emotional states that most games would put in dialogue. 回响's "the brush speaks first, text follows" philosophy (Section 5.3) has its strongest game precedent here.

**Diverge from:** Gris's shape language is European geometric modernism -- the protagonist is built from angular, faceted planes; architecture is sharp and structural. 回响's shape language is East Asian organic brushwork (Section 3: 70/30 organic/geometric, unclosed contours, no hard angles except at emotional peaks). Gris also uses platforming mechanics (jumping, collecting, physics puzzles) -- its visual design supports action. 回响 has no action mechanics (drift-only exploration, choice-based interaction). The frame must hold the player's contemplative attention for minutes, not seconds. Gris's color system is additive (colors accumulate as the game progresses); 回响's is seasonal/cyclical (each chapter has its own complete palette, and the spirit revisits all four seasons in one journey).

---

**9.4 吴冠中 (Wu Guanzhong, 1919-2010)**

**What it solves:** Maximum expression through minimum marks. The bridge between ink tradition and modern visual economy.

Wu Guanzhong synthesized Chinese ink painting with Western modernist abstraction. His work is defined by extreme economy: a village is three ink dots and a curved wash line; a river is negative space bounded by a single gesture stroke; a mountain range is layered gray washes with no contour lines. He wrote extensively about the concept of "form" (形式) as the independent carrier of beauty, separate from subject matter -- a philosophy that aligns with 回响's claim that the brushstroke itself carries emotional information independent of what it depicts (Section 5.3, Brush Story).

**Draw from:** The "dots, lines, and planes" (点线面) vocabulary -- how Wu uses just three element types to construct an entire visual language. This maps directly to 回响's visual hierarchy (Section 3): the ink dot (点) as memory node, progress indicator, and spirit's eye; the brush line (线) as contour, gesture, and ink-thread connection; the ink wash plane (面) as atmosphere, interior, and emotion field. Also: Wu's deliberate tension between abstraction and legibility -- his villages are recognizable as villages, but only through the barest minimum of marks. This is the model for 回响's Supporting and Background hierarchy levels: the minimum strokes to communicate "tree," "person," "room," and no more.

**Diverge from:** Wu's late-career work tends toward pure abstraction -- color fields, gestural marks that reference landscape but do not depict it. 回响 needs its memory world to remain legible as a world: architecture must read as architecture, figures as figures, props as props. The game's 写意 (spirit-over-likeness) philosophy (Section 5.3) must land on the side of suggestion, not abstraction. Also: Wu worked primarily in bright, saturated color in his later period (oils and gouache alongside ink) -- the saturated European palette is not a reference for 回响's restrained seasonal palettes (Sections 4.1-4.4).

---

**9.5 Black Myth: Wukong (黑神话悟空, Game Science, 2024) -- Illustrated Scroll Sequences**

**What it solves:** The scroll as game-narrative format. How a panning, text-integrated ink scroll reads in a player's hands.

At the end of each chapter in Black Myth: Wukong, the game cuts to a 2D illustrated scroll sequence -- a horizontal handscroll that pans across ink-wash paintings, summarizing the chapter's story through image and calligraphic text. These sequences are the most prominent recent example of a mainstream game using Chinese scroll aesthetics as a narrative device.

**Draw from:** The horizontal scroll pan as a storytelling rhythm -- the camera moves left-to-right across a continuous composition, revealing the story in sequence. This is the direct ancestor of 回响's scroll drift (Section 6.7). Also: the integration of calligraphic text INTO the painting rather than overlaid on top -- chapter titles and narrative text appear as brushed characters within the scroll surface, not as UI. The sparing use of color: the Wukong scrolls are predominantly monochrome ink with isolated vermilion seals and occasional warm accents -- this demonstrates how a single warm accent in a cool frame commands total attention, directly informing 回响's semantic color vocabulary (Section 4.6). The seal-stamp as punctuation: red vermilion seals mark chapter transitions and significant moments, which 回响 adapts into its visual language of confirmation and completion (朱墨 Vermilion Ink as the accent ink, Section 4.5).

**Diverge from:** The Wukong scrolls serve as chapter-ending spectacle interstitials -- 2-3 minute reward cinematics between hours of 3D action gameplay. Their visual density is high: maximum detail, maximum drama, packed frames designed for a single viewing. 回响's scrolls ARE the game -- the player lives in them for hours. The visual density must be sustainable, contemplative, re-readable. Wukong's scrolls are illustrative (high detail, clear outlines, fully defined figures) -- 回响 must be more gestural, more 写意, with far more negative space. And Wukong's scrolls are a linear narrative device (one chapter, one scroll, one sequence) -- 回响 is a non-linear association web where memory fragments connect through ink threads, not scroll order.

---