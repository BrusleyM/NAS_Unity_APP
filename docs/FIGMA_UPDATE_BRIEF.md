# Figma update brief: what the built apps now do, and what to redesign

Prepared 5 October 2026. Source of the current designs: the latest code-bundle export (`~/Downloads/NAS Showroom 3`, 4 Oct 2026; identical to "NAS Showroom 2" except for `mobile-designs.tsx` changes made before 20 Aug). The "three apps" compared are:

1. **Customer app** (Unity, mobile) against `mobile-designs.tsx` (6 screens: Splash, Login, Register, Car Selection, AR Viewport, Estimator).
2. **Staff portal** (NAS-Admin, Admin and Sales Manager roles) against `landing-page`, `dashboard-home`, `dealer-dashboard`, `vehicle-inventory`, `staff-management`.
3. **Sales portal** (NAS-Admin, Sales role) against `salesperson-dashboard`.

Part A is the comparison. Part B is the prompt to paste into Figma. Part C lists what the apps and backend still need so the new designs can be built.

---

## Part A — Comparison: design versus what is built

### A1. Customer app (Unity)

| Screen | In the Figma design | As built now | Design action |
|---|---|---|---|
| Splash | Splash | Same | None |
| Login | Email, password, validation | Same, plus a red error state under each field (`email`, `password`) and a general error line for wrong credentials / network | Add the field-error and general-error states |
| Register | Six fields | Same, plus a per-field error under each of the six fields and a general error line | Add the error states |
| **Select dealership** | Does not exist | Built as a plain list shown after every login | **Redesign from scratch** (Part B, section 3) |
| Car selection | Title "Select Your Car", type dropdown + search, one car card with **prev/next chevron buttons**, "Start AR Experience" | Same layout, but **swipe-only carousel** (no chevrons, deliberate), car types come from the catalogue, empty state "No cars match your search.", and a **dealership pill ("name · Change") added under the subtitle** | Remove the chevrons from the design, add the empty state, **remove the pill** (the dealership moves to the hamburger menu) |
| AR viewport | Top bar (Back, car name, cyan check), bottom-centre Settings button, Customize sheet (Wheel / Paint / Trims / Dashboard grid, five colour swatches) | Top bar and Settings as designed. Customize sheet opens but only says "Coming soon" (no data model for options yet). **Added:** rotation slider (-180° to 180°, labelled "Rotate"), vertical-offset slider (±0.5), reset-position button (↻), live hint "Pinch to scale [1:x]" (bottom-left), **"Request test drive" pill** beside Settings (states: default, sending, confirmed per car, failed and retryable), native ARKit coaching overlay while tracking starts, loading overlay "Loading vehicle...", sliders and Customize hidden until a car is placed | Add every "added" item to the AR design; keep the Customize contents as designed (still to build) |
| Estimator | Back to AR, price strip, deposit, trade-in, loan term, financed amount, APR slider, balloon slider, monthly payment, disclaimer, "Send to Dealer" | Same fields | Add the two new overlays below |
| Sending to dealer | Not designed | Overlay "Sending to dealer..." then a confirmation with "Back to Car Selection" | Design both states |
| Loading | Not designed | Generic overlay "Loading..." | Design it once, reuse |
| Profile / settings | Not designed | None | **New: hamburger menu** (Part B, section 2) |

### A2. Staff portal (Admin, Sales Manager)

| Area | In the Figma design | As built now | Design action |
|---|---|---|---|
| Top navigation | Home, Leads, Vehicles, Staff, Insights, Settings (6 items), name, no logout design | Up to **9 items** (Home, Leads, Vehicles, Customers, Builds, Telemetry, Staff, Reports, Settings) filtered by role, role badge, name and a logout icon | **Hamburger menu**; trim primary nav (Part B) |
| Home dashboard | "Dealer Dashboard": new leads today, High Priority (Score ≥ 80), avg response time, conversion rate; lead management and market insight cards | **Operations dashboard**: new leads, open pipeline, high-priority open, close rate, avg first response, AR sessions; sales pipeline, buyer classification mix, lead-generation trend, demand vs capture, body-type, powertrain and colour demand, showroom activity, staff workload | Redesign the page to match |
| Lead score | Numeric "ML Lead Score" (0-100), "Avg Score", "Score ≥ 80" | **No numeric score.** A **buyer class** (Hot / Warm / Cold) with a confidence, **"Unscored"** (grey) when a customer has no app activity, and a **Priority** badge when the model flags a lead (flagged leads and hot leads without a flag both count as "High priority") | Replace every score with class badge + confidence + Unscored + Priority |
| Lead statuses | New, Contacted, Follow-up, Qualified, **Closed** | New, Contacted, Follow-up, Qualified, **In progress, Sold, Lost**. Marking Lost **requires a reason** (saved into notes) | Update status pills, filters and the Lost-reason prompt |
| Lead list and detail | Search, status filter, call/email, engagement (time in AR, customisations), configuration, AI recommendation, notes | Adds: **Intent column**, **Sold** KPI, **Test drive requested** badge, a note explaining when a test drive raised the confidence, **Re-classify / Classify** button (with "temporarily unavailable, try again" error and a "this customer has no app activity" message), whole-row click to open, "Assigned staff" | Add these elements |
| Vehicles | Stock, inventory value, categories, views, AR sessions | Name, year, body type, powertrain, base price, **stock quantity**, low/out of stock, catalogue views, trims / colours / interiors / wheels, vehicle image, **3D model key** | Add the new fields and option editors |
| Staff | Roles Sales / Manager / Admin / Support, performance, revenue, rating | Roles are **Admin, Sales Manager, Sales** only; no revenue or rating; active/inactive, password reset | Remove Support, revenue and rating |
| **Customers** | Not designed | Registered app users, status, sessions, events, views, AR, affordability, builds, leads | **New page** |
| **Builds** | Not designed | Saved configurations (customer, vehicle, trim, colour, interior, wheels, converted to lead) | **New page** |
| **Telemetry** | Not designed | Tabs: Sessions, Events, Vehicle views, AR sessions, **Screens** (screen-visit count, time per screen, filter by screen); delete by role | **New page** |
| **Reports** | "Insights" (not detailed) | Decision packs: funnel, buyer-class bands, owner capacity and SLA, demand concentration, exportable working papers | Redesign "Insights" as Reports |
| Settings | "Settings" (not detailed) | **Dealership settings**: name, address, phone, logo URL, primary colour, default interest rate; read-only unless Admin | Design it |
| Login | Not designed | Staff login page | Design it |

### A3. Sales portal (Sales role)

| Area | In the Figma design | As built now | Design action |
|---|---|---|---|
| Navigation | My Leads, **Performance**, Settings | **My Leads only** (a salesperson cannot open other pages) | Drop Performance and Settings, or mark Performance "later" |
| Lead pool | Not designed | **Unassigned pool** beside "My leads": All / My / Unassigned filter, "Unassigned" badge, **Claim** (row menu and lead view; editing locked until claimed; a lost race shows the server's message) | **New states** |
| KPIs | Total, High Priority, Need Contact, Qualified | Same four, **High Priority** now counts flagged leads (and hot leads without a flag) of mine only | Update meaning, add Priority badge |
| Lead view | Lead score, engagement, configuration, contact, AI recommendation, notes | Buyer class (or Unscored), Priority badge with the **reason a lead is worth contacting first**, real time-in-AR and customisation counts, test-drive badge, Re-classify/Classify, Lost-with-reason prompt, Sold in one tap | Update |

---

## Part B — The Figma prompt (paste everything in the box)

```text
You are updating the existing "NAS Showroom" Figma file (a dark, glassy AR car-showroom product). Keep the current visual language exactly:
- Background #121212; text silver #C0C0C0 (secondary rgba(192,192,192,0.6)); accent cyan #00D4FF.
- Glass cards: fill rgba(255,255,255,0.05), 1px border rgba(192,192,192,0.2), radius 16-24, backdrop blur.
- Primary button: gradient cyan (rgba(0,212,255,0.8) to rgba(0,150,200,0.8)), 1px cyan border, soft cyan glow.
- Logo wordmark "NeoXR" with the X in cyan. Mobile frames are 400x800.
Update the existing screens, add the new ones listed, and keep every other screen unchanged. Produce light touch-ups, not a restyle. Show empty, loading and error states wherever a list or a request is involved.

=== 1. CUSTOMER MOBILE APP: changes to existing screens (these are already built; make the design match) ===

LOGIN and REGISTER
- Add an error state for every input: red border on the field and a small red message under it (email, password; on Register also first name, last name, cell number, confirm password).
- Add a general error line above the button for wrong credentials or a network problem.

CAR SELECTION
- Remove the previous/next chevron buttons. The car card is swiped left/right. (Proposed, not built yet: a subtle swipe hint and a small position indicator such as "2 / 9" under the card. Design them, and I will build them.)
- Car types in the dropdown come from the catalogue (All Types plus the types that exist).
- Add the empty state: "No cars match your search."
- Do NOT show the dealership name or a Change button in the header any more (it moves to the hamburger menu, section 2). Header = hamburger button at top-left, title "Select Your Car", subtitle, nothing else, so the car card keeps its full height.

AR VIEWPORT
- Keep: top bar (Back, car name centred, cyan circular check button), bottom-centre Settings button, "Customize" bottom sheet with the Wheel / Paint / Trims / Dashboard grid and the five paint swatches (this sheet is still to be built; keep it as designed).
- Add a "Rotate" slider (-180 to 180 degrees) above the Settings button.
- Add a vertical-offset slider (small, plus/minus) and a circular reset-position button (circular arrow).
- Add a live hint at bottom-left: "Pinch to scale [1:1]" (the ratio changes, e.g. [1:1.5]).
- Add a "Request test drive" pill next to the Settings button. States: default, sending, confirmed (check icon, "Test drive requested"), failed (stays tappable).
- Add the coaching overlay shown while the camera is still finding the floor. It is drawn by iOS (ARKit), so show it as a placeholder frame labelled "system coaching overlay", not as custom artwork.
- Add a loading overlay "Loading vehicle..." and a fallback state where the car could not load and a plain placeholder block is shown instead.
- All sliders, the Customize button and the test-drive pill are hidden until a car has been placed; show the screen in both states ("before placing" with only Back and the coaching hint, "after placing" with everything).

ESTIMATOR
- Keep the current design. Add two states: an overlay "Sending to dealer..." and a confirmation with a "Back to Car Selection" button.
- Add one shared full-screen loading overlay ("Loading...") for reuse.

=== 2. HAMBURGER MENU, ALL THREE APPS (new) ===
Add a hamburger button to every app. It opens the profile and settings area. Same structure everywhere, different contents.

CUSTOMER APP: hamburger at top-left of Car Selection and Estimator (not on the AR screen, which stays clean). It opens a left side drawer (80% width, glass panel, dimmed backdrop, close on tap outside):
- Header: avatar with initials, full name, email.
- "Edit profile" (first name, last name, cell number editable; email read-only) with Save and error states.
- "Change password".
- "Dealership": shows the current dealership name and address with a "Change" action. Change opens the Select Dealership screen (section 3) in change mode.
- "Privacy and data" (what the app records, e.g. screens visited and AR activity).
- "Sign out".
- Footer: app version.
Design the drawer, the Edit profile screen, the Change password screen and the Privacy screen.

STAFF PORTAL (Admin, Sales Manager): top navigation currently has up to 9 items and is crowded. Keep the main items (Home, Leads, Vehicles, Customers) and move the rest into the hamburger drawer at the far right: Builds, Telemetry, Reports, Staff (Admin only), Dealership settings (Admin edits, Manager read-only). The drawer also holds: profile header (initials, name, role badge, dealership), Edit profile (first name, last name, phone), Change password, Sign out. On narrow widths the whole navigation collapses into the hamburger.

SALES PORTAL (Sales): navigation is just "My Leads"; the hamburger holds profile header, Edit profile, Change password, Sign out.

=== 3. NEW SCREEN, CUSTOMER APP: SELECT DEALERSHIP (design this properly; there is no existing design to copy) ===
Purpose: the customer says where they are buying from. A lead always starts under the dealership they choose.
When it is shown: ONLY when the customer's profile has no selected dealership, that is, on the first login or straight after registration. After that the app goes straight to Car Selection. It is also reachable later from hamburger > Dealership > Change, in "change mode": a back arrow, the current dealership is marked "Current", and a confirmation sheet warns "Changing dealership changes the cars you can see; your selected car will be cleared."
Content per dealership (this is all the data the app has): name, address (optional), phone (optional), logo (optional; fall back to a tinted monogram using the dealership's primary colour), primary colour.
Layout goals: friendly and quick, one clear choice, large touch targets, readable on a 400x800 screen with 2 to 8 dealerships. Title like "Where are you buying from?" with one line of explanation ("We'll show you that dealership's cars and send your enquiries there."). Select a card, then a bottom-fixed primary button "Continue" (disabled until a card is selected) so a mis-tap cannot commit. Show a search field only when there are more than 6 dealerships.
States to design: loading (skeleton cards), loaded, one selected, empty ("No dealerships have cars available yet. Please check back soon."), error with "Try again", change mode with a "Current" badge.
Version 2 (design as a clearly marked variant, do not build yet): location-aware suggestions. A "Near you" group on top with a distance chip on each card (e.g. "4 km"), an opt-in prompt "Use my location to suggest the nearest dealership" with Allow / Not now, and the nearest dealership pre-highlighted but never auto-confirmed.

=== 4. STAFF PORTAL (Admin, Sales Manager): changes to existing designs, plus new pages ===
GLOBAL REPLACEMENTS
- Remove every numeric lead score ("Lead Score", "ML Lead Score", "Avg Score", "Score >= 80"). Replace with: a buyer-class badge (Hot, Warm, Cold) with a small confidence percentage; a grey "Unscored" badge when the customer has no app activity; and a cyan "Priority" badge when the model flags the lead. "High priority" KPIs count flagged leads plus hot leads without a flag, excluding Sold and Lost.
- Lead status set: New, Contacted, Follow-up, Qualified, In progress, Sold, Lost. Remove "Closed". Sold is green, Lost is red. Choosing Lost opens a prompt that requires a reason.
- Remove staff role "Support", and the staff revenue and rating figures. Roles are Admin, Sales Manager, Sales.

HOME becomes an "Operations dashboard": KPI row (New leads, Open pipeline, High-priority open, Close rate, Avg first response, AR sessions); cards for Sales pipeline, Buyer classification mix, Lead generation trend, Demand vs capture, Body type / Powertrain / Exterior colour demand, Showroom activity, Staff workload. Include a period selector.

LEADS page: table with Intent column, class badge, Priority badge, status, assigned staff, Sold KPI; whole row opens the lead; the lead view has Re-classify (or Classify when there is no class), with states "Temporarily unavailable, try again" and "This customer has no app activity yet, so there is nothing to classify"; a "Test drive requested" badge; a note explaining when a test drive plus a submitted form raised the confidence.

VEHICLES: add stock quantity with low/out-of-stock states, body type, powertrain, year, catalogue views, vehicle image upload, trim / colour / interior / wheel option editors, and a "3D model key" field.

NEW PAGES: Customers (registered app users, status, counts of sessions, events, views, AR, affordability, builds, leads; create / edit / deactivate); Builds (saved configurations with trim, colour, interior, wheels and a "converted to lead" flag); Telemetry (tabs Sessions, Events, Vehicle views, AR sessions, Screens; the Screens tab shows total visits, a time-per-screen summary and a filter by screen; delete only for roles that may); Reports (decision packs: digital-to-desk funnel, buyer-class bands, capacity and SLA by owner, demand concentration, export); Dealership settings (name, address, phone, logo URL, primary colour, default interest rate; read-only for Sales Manager); Staff login page.

=== 5. SALES PORTAL (Sales role) ===
- Navigation is only "My Leads" plus the hamburger. Remove Performance and Settings from the navigation (keep a Performance design as a "later" page if wanted).
- Add the lead pool: a filter All / My / Unassigned, an "Unassigned" badge on pool leads, and "Claim" in the row menu and in the lead view. Unclaimed leads open read-only with a banner "Claim this lead to edit it". Add the lost-race state: "Someone else claimed this lead" with the list refreshed.
- KPIs: Total, High Priority (my flagged leads plus my hot leads without a flag), Need Contact, Qualified.
- Lead view: class badge or Unscored, Priority badge with a one-line reason it is worth contacting first, real time in AR and customisation count, test-drive badge, Re-classify / Classify, mark Lost with a required reason, mark Sold in one tap.

Deliver: updated frames for every item above, plus a short list of components that changed so they can be updated in the library.
```

---

## Part C — What the apps and backend still need before these designs can be built

The hamburger menu and the "first time only" dealership rule need data the system does not have yet. These are build tasks, not design tasks.

| Need | Current state | What to add |
|---|---|---|
| Remember the customer's dealership on their **profile** | The choice lives on the telemetry session and in the device's saved preferences; `GameManager` clears it on every login. `CustomerUser` has no dealership column. | A nullable `PreferredDealershipId` on the customer, returned at login, set when the customer chooses; the app shows the Select Dealership screen only when it is null. |
| Edit customer profile, change password | `CustomerAuthController` has only `register` and `login`. | `GET/PUT /api/customer/profile` and `POST /api/customer/auth/change-password`, each with a test (standing rule). |
| Staff edit own profile, change own password | `GET /api/staff/me` exists; `PUT /api/staff/{id}` is Admin-only. | `PUT /api/staff/me` and a change-password endpoint available to every staff role. |
| Customize sheet contents (wheel, paint, trims, dashboard) | The sheet says "Coming soon"; no customer-facing option data. | Decide the data model first; unchanged from the earlier deferral. |
| Privacy screen | No text exists. | Write the statement; ties to the retention gap raised in the Backend report. |
| Performance page for Sales | Not built. | Optional, later. |

Decision that affects the design: when a customer changes dealership from the menu, `GameManager` already clears the selected car if the dealership differs, so the confirmation sheet wording above is accurate.
