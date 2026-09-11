# Handoff: Multi-User Workout Tracker → .NET MAUI + Google Drive backend

## Overview
A multi-user (family / household) workout tracking mobile app. Each person picks a profile, follows a
schedule of routines (AM/PM slots per weekday), runs a guided session, and logs sets/reps/weight per
exercise. It ships with a manufacturer exercise library (Bowflex-style home gym plus a Vibration Plate
category) and lets users author their own workouts with visibility controls (Just Me / Public /
Manufacturer). There is also a HIIT/interval player with spoken cues.

Target implementation: **.NET MAUI** (iOS + Android, ideally Windows too), with **Google Drive** as the
**application-level** backing store for the development phase (see "Tenancy" below — Drive is the app's
store, not each user's personal Drive, and the backend location will be re-established before go-live).

---

## Tenancy — read this before anything else

This is the part the prototype does *not* yet model, and it drives the schema.

**Three levels, not two:**

1. **Account** — the billable, ownable unit. The app is downloaded and used by many independent accounts.
   One account has one **primary account holder** (the owner).
2. **Member** — a person under an account. The prototype's "profile" is a member. An account may hold one
   member (solo) or several (a **family plan**).
3. **Profile data** — a member's own schedule, history, and set log, which must remain intact and portable.

**Requirements this implies:**

- **Granular permissions, set by the primary holder, per member.** Not one admin flag — separate,
  independently grantable capabilities. At minimum: view others' history; create/edit shared workouts;
  edit *another member's* workouts; edit the schedule (own vs anyone's); delete content; manage members
  (invite/remove); manage billing; export data. Design this as a capability set on the membership record,
  not a hardcoded role enum — though ship named role presets (Owner / Adult / Teen / Child / Guest) on top
  of it so the UI is not a permissions matrix.
- **Junior → independent account split-off.** A minor member must be able to be promoted out into their own
  standalone account **carrying their full history and workouts with them**, leaving the origin account
  intact. This is a first-class migration operation, not a data export. It is the single hardest constraint
  on the schema: member-owned data must never be entangled with account-owned data, and every record must
  carry both an `accountId` and an `ownerMemberId` so the split is a re-parent, not a rebuild.
- **Billing, later.** No payment system now, but the account record is where a plan/entitlement/seat-count
  will attach. Leave the seam: `Account { planId, seatLimit, entitlements[], billingStatus }` even if every
  value is a hardcoded free-tier constant today. Member count must be checkable against a seat limit at
  invite time, through a service the billing layer can later own.
- **Multi-account on one install.** The app may hold more than one signed-in account on a device, and one
  person may belong to two accounts (e.g. a teen in a family plan who also owns their own). Do not assume
  one account per install anywhere in the storage layer.
- **The backend location is provisional.** Drive is the dev-phase store and will be replaced before go-live.
  Every read/write must go through a repository interface with no Drive types leaking into view models — the
  swap to a real backend must be a single implementation change.

**Terminology in this document:** where the prototype and the sections below say "profile", read **member**
under an account. The word "profile" is retained where it names an existing screen (the profile gate).

## About the Design Files
The files in this bundle are **design references created in HTML**. They are interactive prototypes that
demonstrate the intended look, information architecture, and behavior — they are **not production code to
port**. The task is to **recreate these designs in .NET MAUI** using XAML + MVVM and MAUI-native controls,
following the platform's conventions (CollectionView, Shell navigation, Border surfaces, etc.).
Do not embed the HTML in a WebView.

- `Workout Tracker.dc.html` — the source design (streaming component format; read the markup plus the
  `<script data-dc-script>` logic class for exact structures and behavior).
- `Workout Tracker (standalone).html` — the same app as one self-contained file. **Open this in a browser
  and click through every flow.** Fastest way to understand the product.

## Fidelity
**High-fidelity.** Colors, type, spacing, radii, and interaction states are final and come from a design
system ("Nocturne", tokens listed below). Recreate the UI closely, mapping tokens to MAUI Styles and
resource dictionaries. Layout is a 390x844 iPhone frame; treat that as the reference viewport.

---

## Information Architecture

Bottom tab bar (MAUI Shell TabBar), 4 tabs plus a modal layer:

1. **Home** — week strip, streak card, today's scheduled workout(s) with a Start button, or a rest-day card.
2. **Workouts** — routine library. Filter chips: source (Manufacturer / Mine / Public) and type. Tapping a
   routine opens its detail overlay. Header actions: **New** (standard builder) and **New HIIT**.
3. **Schedule** — assign routines to weekday AM/PM slots.
4. **History / Logs** — calendar view and a sortable/filterable set-log table (search, date range, sort by
   date/exercise/weight), with export and print.

Modal / overlay layer:
- **Profile gate** (blocking, on launch): "Who's working out?" avatar grid, Add Profile, delete.
- **Exercise detail overlay**: muscles worked, equipment, bench position, accessory, arm position, tips.
- **Workout builder**: name, visibility, exercise picker, per-exercise **set groups** (sets / reps / weight),
  "Add new set" per exercise.
- **HIIT builder / player**: ordered sections (Warm Up, Exercise, Rest, Cool Down, Custom) with durations,
  cycle repeats, spoken announcements, end beep, volume/mute, voice picker.
- **Session runner**: per-exercise grouped-set inputs, blank unless the workout has saved values.
- **Finish Workout modal**: collects any missing sets/reps/weight before saving; edits write back to the
  workout so the next session pre-fills.

---

## Screens / Views (detail)

### 1. Profile Gate
- **Purpose**: choose who is training. Blocks the app until a profile is selected.
- **Layout**: full-screen overlay, padding 32px 20px, centered header ("Who's working out?" 20px/600 heading
  font; subline "Pick a profile to continue" 12px muted), then a **3-column grid**, gap 16px 10px.
- **Profile tile**: 60x60 circle, background = the profile's avatar color, centered initial (22px/600, color
  `--color-bg`). Name below, 12px, single line, ellipsized at 70px. An 18x18 circular X delete button at
  top:-4px / right:10px (surface fill, 1px divider border).
- **Add Profile tile**: 60x60 circle with a **1px dashed** divider border and a plus icon (20px, muted).
- **New profile draft panel** (below, when Add is tapped): surface panel, radius-md, padding 16px, gap 12px —
  a Name field, a row of six 28px color swatches (2px border marks the selected one), and Cancel / Create
  buttons (Cancel = transparent + divider border; Create = accent fill, `--color-bg` text, 600 weight).
- Avatar colors: `oklch(65% 0.13 H)` for H = 280, 200, 340, 100, 30, 160.

### 2. Home
- Header: tab title 20px/600, letter-spacing -0.01em, left; right side = 30px circular profile button
  (avatar color, initial) — tapping it reopens the profile gate.
- **Week strip**: 7 equal flex cells, gap 6px, each padding 8px 0, radius-md, background varies by state
  (today / has-workout / logged). Inside: day label 10px uppercase letter-spacing 0.05em muted, and a 7x7
  dot whose color encodes status.
- **Streak card**: surface, radius-md, padding 10px 12px, flex row gap 8px; filled flame icon 20px in accent;
  text "N day streak" (600) plus " · N workouts logged" (muted).
- **Today's workout card**: label "TODAY'S WORKOUT — AM/PM" (11px, uppercase, letter-spacing 0.08em, muted).
  Card: surface, radius-lg, shadow-md, padding 18px, gap 12px. Title 22px/600, count subline 12px muted, then
  wrapped exercise pills (11px, padding 3px 9px, radius 20px, background `--color-accent-800`, color
  `--color-accent-100`), then a full-width Start button (padding 12px, 15px/600, radius-md) whose
  fill/label/icon change with state (Start / Resume / Completed).
- **Rest day card**: surface, radius-lg, padding 28px 18px, centered; moon-stars icon 28px muted, "Rest day"
  16px/600, copy 12px muted max-width 220px, and an outlined accent "Browse Workouts" button.

### 3. Workouts (library)
- Two filter chip rows: source chips (solid 1px border) and visibility chips (**1px dashed** border), both
  11-12px, padding 6px 12-13px, radius 20px; active state = accent tint background plus accent text.
- **Routine row**: surface, radius-md, padding 14px, gap 6px, hover
  `color-mix(in srgb, var(--color-text) 4%, var(--color-surface))`. Name 15px/600 left, owner badge right
  (manufacturer name / "You" / other profile name).
- Header buttons: **New HIIT** (transparent, divider border, timer icon) and **New** (transparent, accent
  border plus accent text, plus icon). Both padding 6px 12px, 13px/500, radius-md; hover/active tints are
  color-mix of the border color at 12% / 22%.

### 4. Session runner
- Exercises in workout order. Each shows its name, equipment/position meta, and one row per **set group**
  with three numeric inputs: **sets**, **reps**, **weight** (weight only when equipment is Bowflex Machine
  or Kettlebell).
- **Inputs start blank** unless the workout carries saved values. No invented defaults, ever.
- "Add new set" adds an additional blank set group to that exercise.
- **Vibration Plate** exercises instead show: position/stance, mode, duration, frequency (Hz), level (1-30).

### 5. Finish Workout modal
- Lists every set group missing sets, reps, or weight, with editable inputs for all three.
- On confirm: writes a history entry for the date, appends set-log rows, **and writes the values back onto
  the workout definition** so the next session pre-fills.
- Reopening a **completed** workout shows the logged values; the primary button becomes **"Update Workout"**,
  which **replaces that day's log entry** rather than appending a duplicate.

### 6. History / Logs
- Toggle: **calendar** view and **table** view.
- Table columns: date, workout, exercise, set, reps, weight. Sortable (sortKey, sortDir), filterable by
  search string and date range. Export menu plus a print stylesheet that prints only the log table
  (black on white).

---

## Data Model

Persisted today as one JSON blob in browser storage. In the real app this becomes the Google Drive document.
Shapes as implemented:

```
Profile        { id, name, color, initial }   // → becomes Member; see Tenancy above

Exercise       { id, name, category, muscles, bench, accessory, armPos, equipment,
                 tips: string[], visibility: 'manufacturer'|'justme'|'public',
                 ownerId: string|null, ownerName }

VibExercise    { id, name, category, muscles, stance, tip }   // plus mode, duration, hz, level at use time

SetGroup       { uid, sets, reps, weight }                    // '' means "not entered"

RoutineExercise{ exerciseId, sets, reps, groups?: SetGroup[] }

Routine        { id, name, custom, modified, type: 'standard'|'hiit',
                 visibility, ownerId, ownerName, exercises: RoutineExercise[],
                 sections?: HiitSection[] }

HiitSection    { type: 'Warm Up'|'Exercise'|'Rest'|'Cool Down'|'Custom',
                 title, description, seconds, isCycleRest? }

Schedule       { [Sun..Sat]: { AM: routineId[], PM: routineId[] } }

HistoryEntry   { date: 'YYYY-MM-DD', routineId, routineName, slot: 'AM'|'PM', completedAt }

SetLogRow      { date, routineId, routineName, exerciseId, exerciseName,
                 setIndex, sets, reps, weight }

HiitSettings   { endBeepEnabled, voiceEnabled, voiceURI, volume: 0-100, muted }

PersistedRoot  { profiles: Profile[],
                 customRoutines: Routine[],
                 customExercises: Exercise[],
                 profileData: { [profileId]: { schedule, history, setLog, nextCustomId, hiitSettings } } }
```

**Key invariants** (these were bugs once — keep them):
- Custom routine IDs must be globally unique; the "already completed today" check matches on **id OR name**.
- Empty string is not zero. A blank field means "the user hasn't told us" and must stay blank through
  save/reload.
- Manufacturer content is read-only; visibility is manufacturer / justme / public, and justme items are
  visible only to their ownerId.
- Schedule, history, and set log are **per profile**; the exercise and routine libraries are shared
  (scoped by visibility).

**Enumerations**
- Categories: All, Chest, Shoulders, Back, Arms, Abs, Legs, Full Body, Cardio, Vibration Plate
- Equipment: All, Bowflex Machine, Bodyweight, Kettlebell, Vibration Plate, Stretch, Cardio Machine, Custom
- Vibration modes: Vibration (linear), Oscillation (pivotal), Tri-planar, Combined / dual, Massage pulse
- Vibration stances (11): feet together (narrow), shoulder-width, wide, staggered/split, single leg, balls of
  feet/toes, heels only, kneeling on plate, hands on plate, forearms on plate, seated on plate
- Vibration exercises (19): static squat hold, deep squat hold, calf raise, lunge hold, glute bridge, plank,
  side plank, push-up hold, tricep dip, seated massage, and others — read the `VIB_EXERCISES` array in the
  design file for the full list with muscles and coaching tips.
- Vibration level range 1-30; frequency in Hz; duration in seconds.
- The manufacturer exercise library and seeded routines (`EXERCISES`, `ROUTINE_DEFS`) are in the design
  file — **lift them verbatim** into seed data; they carry real bench positions, arm positions, and tips.

---

## Design Tokens

The design consumes the "Nocturne" system stylesheet (`ds/nocturne-styles.css`, in this bundle). Read that
file for authoritative values and mirror it as a MAUI ResourceDictionary. The load-bearing ones:

| Token | Value / role |
| --- | --- |
| `--color-bg` | #161826 — app ground |
| `--color-text` | #e9e9ed |
| `--color-accent` | #9184d9 — blurple, used as line/glow, never a flood |
| `--color-surface` | card / panel fill |
| `--color-divider` | 1px borders |
| `--color-neutral-400/500` | muted text, icon strokes |
| `--color-accent-800` / `--color-accent-100` | pill fill / pill text |
| `--color-accent-900` | radial page glow behind the device |
| `--font-heading`, `--font-body` | Inter (headings 500-600, never bolder) |
| `--radius-md`, `--radius-lg` | 8px base scale |
| `--shadow-sm/md/lg` | edge plus ambient darkness, never stacked |
| `--space-*` | compact scale, density 0.70x |

Conventions to preserve:
- **Outlined primary actions** (1px accent border on transparent), not solid fills. The one accent-filled
  button in the design is "Create" in the profile draft.
- Hover/pressed states are color-mix tints of the element's own border color at ~12% / ~22%.
- Focus: 2px accent outline, 2px offset — in MAUI, an equivalent visual focus state; never platform-default blue.
- Icons: **Phosphor** (regular plus fill). In MAUI, ship Phosphor as a font asset and use FontImageSource glyphs.
- Chrome is dark-only here. If you support light mode, derive it from the ramps; don't invent.
- Minimum hit target 44pt.

## Assets
- `ds/nocturne-styles.css` — design token and component stylesheet (source of truth for values).
- Phosphor Icons — phosphoricons.com (MIT). Not vendored; add the font to the MAUI project.
- Inter — Google Fonts (OFL). Add as a MAUI font asset.
- No photography or raster assets are used.

---

# Part 2 — What I want you (Claude Code) to do

## A. Stand up the .NET MAUI project
1. Scaffold a .NET MAUI app (latest LTS .NET) targeting iOS and Android, named `WorkoutTracker`. Use
   **MVVM** (CommunityToolkit.Mvvm), **Shell** navigation with a bottom TabBar, and DI-registered services.
2. Create the token ResourceDictionary from `ds/nocturne-styles.css`: colors, then Styles for Button
   (primary-outline / secondary / ghost), Border (card, surface, radius), Entry, Label (heading / body /
   meta / kicker scales), and the chip/pill look. No inline hex values anywhere in XAML.
3. Register Inter and Phosphor as font assets; expose Phosphor glyphs as StaticResource keys so XAML says
   `{StaticResource IconFlame}` rather than raw code points.
4. Model the data layer as POCOs matching the schema above plus a repository interface, so the storage
   backend is swappable (local file vs Google Drive).
5. Ship a **local-first** store (JSON file in `FileSystem.AppDataDirectory`) as the primary path, with Drive
   as sync. The app must work fully offline.

## B. The backend — help me get this connected
Google Drive holds the **application's** data for the development phase. This is not per-user Drive storage;
the backing location will be re-established before go-live, so treat it as one swappable implementation of
the repository interface. Please:
1. **Advise me on the right shape first.** Compare, with a recommendation and reasoning:
   a) one JSON document per profile in a Drive app folder (simple, atomic, offline-friendly);
   b) Google **Sheets** as relational-ish tables (profiles / exercises / routines / schedule / history /
   set_log) via the Sheets API — human-readable and editable, but chattier and easy to corrupt;
   c) Drive **appDataFolder** (hidden, per-app) vs a visible folder the user can see and back up.
   Tell me which you'd pick for a small multi-profile household app and why.
2. **Walk me through Google Cloud setup step by step**, assuming I've never done it: create the project,
   enable the Drive (and Sheets, if we go that way) API, configure the OAuth consent screen, add test users,
   create the OAuth client IDs — noting that **iOS and Android each need their own client ID**, that Android
   needs the SHA-1 of my signing keystore, and that mobile apps use PKCE with **no client secret**. Give me
   the exact redirect URI / URL scheme to register for each platform.
3. Implement OAuth in MAUI with `WebAuthenticator` plus PKCE, request the **narrowest** scope that works
   (`drive.appdata` or `drive.file`, not full `drive`), store the refresh token in `SecureStorage`, handle
   silent refresh and re-consent. Add the platform plumbing (iOS CFBundleURLTypes in Info.plist, Android
   intent-filter activity).
4. Implement the sync service: read-modify-write with Drive revision/ETag conflict detection, last-write-wins
   merge on a per-record `updatedAt`, an outbox queue for offline edits, and a visible sync state
   (synced / pending / conflict) in the UI.
5. **Note on ownership of the Drive:** the Drive is the **application's** store, not each user's personal
   Drive — users never authenticate to their own Google account for storage. So the auth question is about
   *service* credentials for the app, and separately about how *end users* sign in to their account and
   members (which is app-level identity, not Google identity). Tell me the right split there, including
   whether a service account plus a shared Drive is preferable to an OAuth-installed-app flow for a
   server-side/app-owned store, and where the credential can safely live given a mobile client can't hold a
   secret. If the honest answer is "a mobile app should not talk to an app-owned Drive directly, put a thin
   API in front of it" — say so, and sketch the smallest version of that.
6. **Data partitioning for tenancy:** given Account → Member → per-member data, propose the file/collection
   layout (one document per account? per member? an index?), and make sure the **junior split-off** is cheap:
   moving one member's data into a new account should not require rewriting other members' records.
7. Write integration tests / a smoke-test harness I can run, plus a fallback path if the backend is
   unreachable — the app must stay fully usable offline and reconcile later.

Explain each step as you go and stop to ask me for the values only I can provide (project name, bundle id,
keystore SHA-1, test account). Don't guess at credentials.

## C. Review and improve the design for multi-user
This prototype grew organically and the multi-user story is its weakest part. **Review the design critically**
and come back with concrete recommended changes — call out anything wrong, confusing, or that won't scale:

1. **Account / member model.** The prototype has a flat list of colored profiles with no auth — anyone can
   tap any profile and any profile can delete another. Restructure it to the **Account → Member** model in
   the Tenancy section: propose the schema, the permission capability set, the named role presets, how a
   member proves they're themselves on a shared device (PIN? nothing? biometric for the owner only?), how
   invites work, and what the primary holder's management screen looks like. Then tell me what breaks when
   one person uses two devices, and when one person belongs to two accounts.
2. **Visibility model.** manufacturer / justme / public is doing a lot of work with three values, and with an
   account boundary it's now ambiguous: "public" almost certainly means **account-public** (visible to the
   other members of my family plan), which is a different thing from a future cross-account/community share.
   Propose the corrected scale — I'd expect something like private / account / community(future) /
   manufacturer — plus sharing *to a specific member*, a copy-on-modify rule when a member edits content
   they don't own, and clearer ownership attribution in the UI. Say how visibility is re-evaluated when a
   member splits off: account-shared workouts they authored should travel with them; ones they didn't
   author shouldn't.
3. **Flow.** Walk the whole path — launch, profile gate, home, start workout, session, finish, history — and
   tell me where it's too many taps, where state is ambiguous (what do "Resume" vs "Completed" vs "Update
   Workout" mean to a user?), and where a first-run user with zero data gets stuck. The empty states need
   particular attention.
4. **The blank-by-default decision.** Fields intentionally start empty and the Finish modal collects what's
   missing. I like that it never invents numbers — but tell me honestly whether "last time's values" as a
   *greyed placeholder* (not a value) would be better, and how to keep "not entered" and "entered as zero"
   legibly distinct.
5. **Data model for sync and tenancy.** What must change in the schema: stable GUIDs instead of slug ids,
   `accountId` + `ownerMemberId` on every record, per-record `updatedAt` / `deletedAt` tombstones, and
   separating the *definition* of a workout from a *performed instance* — right now finishing a workout
   mutates the routine, which is clearly wrong once a routine is shared between members of an account
   (one member's logged weights would overwrite another's). Show me the corrected schema, and show the
   **junior split-off** as a concrete step-by-step migration against it.
6. **Anything I haven't thought of** — rest timers, weight units (lb/kg), progression suggestions,
   accessibility (the session UI is used mid-exercise, at arm's length, with sweaty hands), and whether the
   HIIT player's spoken cues should work with the screen locked. Also: minors and data (what a parent can
   see of a child's log, and what changes at 18), and what a member sees the moment their permissions are
   reduced mid-session.

Deliver the review as a prioritized list — **must-fix before building** vs **nice-to-have later** — and where
you recommend a change, show me the revised schema or flow rather than describing it. **Ask me questions
before writing code**; I'd rather settle the model than refactor it.

## Suggested order of work
1. Read the design files, run the standalone HTML, then do the **Part C review** — starting with the
   Account → Member schema — and ask me your questions.
2. Once we agree the model: scaffold MAUI plus tokens plus navigation shell with local JSON storage, behind
   the repository interface.
3. Then the backend: Drive wiring for the dev phase, with the swap seam intact.
4. Then screen by screen: Home, Workouts, Session, Finish, History, Schedule, HIIT, then account/member
   management.
5. Leave the billing seam stubbed and unimplemented, but present.

## Files in this bundle
- `README.md` — this document.
- `Workout Tracker (standalone).html` — runnable prototype. Open in a browser first.
- `Workout Tracker.dc.html` — design source; the `<script data-dc-script>` block holds the exercise library,
  routine definitions, vibration data, and all behavior logic.
- `ds/nocturne-styles.css` — design tokens and component styles.
