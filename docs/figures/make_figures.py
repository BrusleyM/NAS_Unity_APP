"""Regenerate every figure in docs/ENGINEERING_REPORT.md (IEEE-style: serif, 300 dpi, greyscale-safe).

    python3 docs/figures/make_figures.py

The model-size figure reads the processed .glb files if they are present locally
(~/Documents/Projects/cgtrader_processed); otherwise it uses the sizes recorded in docs/data/model_sizes.json.
"""
import json
import os
import warnings

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from matplotlib.patches import FancyArrowPatch, FancyBboxPatch, Patch, Rectangle

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
warnings.filterwarnings("ignore")
plt.rcParams.update({
    "font.family": "serif", "font.serif": ["STIX Two Text", "Times New Roman", "DejaVu Serif"], "mathtext.fontset": "stix",
    "font.size": 8, "axes.labelsize": 8, "axes.titlesize": 8, "legend.fontsize": 7, "xtick.labelsize": 7, "ytick.labelsize": 7,
    "axes.linewidth": 0.6, "lines.linewidth": 1.0, "savefig.dpi": 300, "axes.grid": True, "grid.linewidth": 0.3, "grid.alpha": 0.5,
})
COL1, COL2 = 3.5, 7.16


def save(fig, name):
    fig.savefig(os.path.join(HERE, name), bbox_inches="tight", pad_inches=0.03)
    plt.close(fig)
    print("wrote", name)


def box(ax, xy, w, h, text, fc="#f2f2f2", fs=7):
    ax.add_patch(FancyBboxPatch(xy, w, h, boxstyle="round,pad=0.004,rounding_size=0.012", fc=fc, ec="black", lw=0.6))
    ax.text(xy[0] + w / 2, xy[1] + h / 2, text, ha="center", va="center", fontsize=fs)


def arrow(ax, a, b, ls="-", rad=0.0, label=None, lpos=0.5, fs=5.8, ldx=0.0, ldy=0.02):
    ax.add_patch(FancyArrowPatch(a, b, arrowstyle="-|>", mutation_scale=7, lw=0.7, color="black", ls=ls, connectionstyle=f"arc3,rad={rad}"))
    if label:
        ax.text(a[0] + (b[0] - a[0]) * lpos + ldx, a[1] + (b[1] - a[1]) * lpos + ldy, label, fontsize=fs, style="italic", ha="center")


# ---------- Fig. 1: event-driven architecture ----------
fig, ax = plt.subplots(figsize=(COL2, 4.1)); ax.axis("off"); ax.set_xlim(0, 1); ax.set_ylim(0, 1)
box(ax, (0.37, 0.42), 0.26, 0.22, "EventBus\nstatic, type-safe\nSubscribe / Unsubscribe / Publish\n36 event structs (GameEvents.cs)", fc="#cccccc", fs=6.3)
box(ax, (0.01, 0.74), 0.24, 0.22, "Card controllers\nLogin, Register,\nDealershipSelection,\nCarSelection, Estimator\n(added with AddComponent)", fc="#e6e6e6", fs=6.2)
box(ax, (0.01, 0.40), 0.24, 0.22, "AR scene controllers\nArViewport, ObjectPlacer,\nCarManipulation,\nSelectedCarModelLoader", fc="#e6e6e6", fs=6.2)
box(ax, (0.75, 0.74), 0.24, 0.22, "ParentPageController\npure router: swaps the card\nin response to Session*\nevents; publishes\nScreenShownEvent", fc="#e6e6e6", fs=6.2)
box(ax, (0.75, 0.38), 0.24, 0.26, "GameManager\n(DontDestroyOnLoad)\nsole subscriber to the raw\nauth / car / dealership events;\nrepublishes Session* events;\nowns telemetry sending", fc="#d9d9d9", fs=6.0)
box(ax, (0.40, 0.05), 0.24, 0.14, "ScreenVisitTracker\n(pure logic, EditMode tested)", fc="#f2f2f2", fs=6.2)
box(ax, (0.75, 0.05), 0.24, 0.14, "NAS_Backend\n/api/telemetry/*", fc="#bbbbbb", fs=6.3)
arrow(ax, (0.25, 0.85), (0.37, 0.60))
ax.text(0.27, 0.665, "publish raw events", fontsize=5.6, style="italic", ha="left")
arrow(ax, (0.25, 0.51), (0.37, 0.53)); ax.text(0.31, 0.575, "publish", fontsize=5.6, style="italic", ha="center")
arrow(ax, (0.63, 0.58), (0.75, 0.58)); ax.text(0.69, 0.615, "raw events", fontsize=5.6, style="italic", ha="center")
arrow(ax, (0.75, 0.46), (0.63, 0.46), ls="--"); ax.text(0.69, 0.425, "Session*Event", fontsize=5.6, style="italic", ha="center")
arrow(ax, (0.60, 0.64), (0.78, 0.74)); ax.text(0.655, 0.725, "Session*Event", fontsize=5.6, style="italic", ha="center", rotation=28)
arrow(ax, (0.75, 0.86), (0.52, 0.64), ls="--"); ax.text(0.66, 0.83, "ScreenShownEvent", fontsize=5.6, style="italic", ha="center", rotation=-35)
arrow(ax, (0.79, 0.38), (0.64, 0.17)); ax.text(0.72, 0.265, "owns", fontsize=5.6, style="italic", ha="center", rotation=48)
arrow(ax, (0.87, 0.38), (0.87, 0.19)); ax.text(0.905, 0.285, "HTTPS", fontsize=5.6, style="italic", ha="left")
ax.text(0.01, 0.27, "GameManager publishes a Session*Event only AFTER updating its own fields, so no\nsubscriber can observe stale state, whatever the Awake/OnEnable order.", fontsize=5.9, style="italic", va="top")
save(fig, "fig01_event_architecture.png")

# ---------- Fig. 2: screen flow ----------
fig, ax = plt.subplots(figsize=(COL2, 3.3)); ax.axis("off"); ax.set_xlim(0, 1); ax.set_ylim(0, 1)
W, H, Y = 0.14, 0.20, 0.50
cols = [0.01, 0.20, 0.39, 0.58, 0.77]
names = [("splash", "splash"), ("login / register", "login | register"), ("dealership selection", "dealership_selection"), ("car selection", "car_selection"), ("AR viewport\n(AR Scene)", "ar_viewport")]
for x, (label, ident) in zip(cols, names):
    box(ax, (x, Y), W, H, f"{label}\n\n[{ident}]", fc="#cccccc" if "AR" in label else "#e6e6e6", fs=6.1)
box(ax, (0.77, 0.12), W, H, "estimator (calculator)\n\n[estimator]", fc="#e6e6e6", fs=6.1)
events = ["SplashDismissed", "AuthSucceeded", "DealershipSelected", "CarSelected"]
for i, ev in enumerate(events):
    a, b = (cols[i] + W, Y + H / 2), (cols[i + 1], Y + H / 2)
    arrow(ax, a, b)
    gx = (a[0] + b[0]) / 2
    ax.plot([gx, gx], [Y + H / 2 + 0.01, 0.84], color="grey", lw=0.4, ls=":")
    ax.text(gx, 0.85, ev, fontsize=5.6, style="italic", ha="center", va="bottom", rotation=0 if i != 2 else 0)
arrow(ax, (0.84, Y), (0.84, 0.32)); ax.text(0.855, 0.41, "Confirm: ReturnToEstimatorRequested", fontsize=5.6, style="italic", ha="left")
arrow(ax, (0.80, Y), (0.69, Y), ls="--", rad=-0.55); ax.text(0.745, 0.345, "Back: ExitArRequested", fontsize=5.6, style="italic", ha="center")
arrow(ax, (0.62, Y), (0.46, Y), ls="--", rad=-0.55); ax.text(0.45, 0.335, "ChangeDealershipRequested", fontsize=5.6, style="italic", ha="center")
arrow(ax, (0.77, 0.22), (0.68, Y), ls="--"); ax.text(0.755, 0.115, "ReturnToCarSelectionRequested", fontsize=5.6, style="italic", ha="right")
ax.text(0.01, 0.30, "Every transition publishes ScreenShownEvent(ScreenNames.X). The ScreenVisitTracker\ncloses the previous visit and sends it as telemetry; pause closes the current\nvisit, resume re-opens it, quit closes it. Visits under 0.5 s are dropped.", fontsize=6, style="italic", va="top")
ax.text(0.01, 0.04, "Solid: forward flow. Dashed: back or sideways navigation. Identifiers in [brackets] are the ScreenNames sent to the API.", fontsize=5.6, style="italic")
save(fig, "fig02_screen_flow.png")

# ---------- Fig. 3: two counters timeline (the unit-test scenario) ----------
fig, ax = plt.subplots(figsize=(COL2, 1.9))
ax.broken_barh([(0, 635)], (2.2, 0.55), facecolors="#cccccc", edgecolor="black", lw=0.5)
ax.text(317, 2.47, "session timer (wall clock): 635 s", ha="center", va="center", fontsize=6.5)
ax.broken_barh([(0, 20)], (1.2, 0.55), facecolors="#777777", edgecolor="black", lw=0.5)
ax.broken_barh([(20, 600)], (1.2, 0.55), facecolors="white", edgecolor="black", lw=0.5, hatch="////")
ax.broken_barh([(620, 15)], (1.2, 0.55), facecolors="#777777", edgecolor="black", lw=0.5)
ax.text(10, 1.47, "20 s", ha="center", va="center", fontsize=5.5, color="white")
ax.text(320, 1.47, "app in the background (10 min) - not screen time", ha="center", va="center", fontsize=6.3, bbox=dict(fc="white", ec="none", pad=1))
ax.text(627, 1.95, "15 s", ha="center", va="center", fontsize=5.5)
ax.annotate("Pause():\nvisit 1 sent now", (20, 1.2), (60, 0.35), fontsize=6, arrowprops=dict(arrowstyle="-|>", lw=0.5))
ax.annotate("Resume(): the same screen\nre-opens, new visit starts", (620, 1.2), (380, 0.55), fontsize=6, arrowprops=dict(arrowstyle="-|>", lw=0.5))
ax.annotate("Show(car_selection): visit 2 sent", (635, 1.2), (480, -0.05), fontsize=6, arrowprops=dict(arrowstyle="-|>", lw=0.5), ha="center")
ax.set_xlim(-10, 660); ax.set_ylim(-0.3, 3.0); ax.set_yticks([1.47, 2.47]); ax.set_yticklabels(["screen timer\n(estimator)", "session"]); ax.set_xlabel("seconds"); ax.grid(axis="y", alpha=0)
save(fig, "fig03_two_counters.png")

# ---------- Fig. 4: processed model sizes ----------
sizes_path = os.path.join(ROOT, "docs", "data", "model_sizes.json")
src = os.path.expanduser("~/Documents/Projects/cgtrader_processed")
sizes = {}
if os.path.isdir(src):
    for f in sorted(os.listdir(src)):
        if f.endswith(".glb"):
            sizes[f[:-len("-demo.glb")]] = round(os.path.getsize(os.path.join(src, f)) / 1e6, 2)
    json.dump(sizes, open(sizes_path, "w"), indent=1)
else:
    sizes = json.load(open(sizes_path))
names = sorted(sizes, key=sizes.get)
fig, ax = plt.subplots(figsize=(COL1, 2.2)); ypos = np.arange(len(names))
ax.barh(ypos, [sizes[n] for n in names], color=["#555555" if sizes[n] > 50 else "#cccccc" for n in names], edgecolor="black", lw=0.5)
for y_, n in zip(ypos, names):
    ax.text(sizes[n] + 1.5, y_, f"{sizes[n]:.1f} MB", va="center", fontsize=6.5)
ax.set_yticks(ypos); ax.set_yticklabels(names); ax.set_xlabel("processed .glb size (MB)"); ax.set_xlim(0, 118)
save(fig, "fig04_model_sizes.png")
print(sizes)
