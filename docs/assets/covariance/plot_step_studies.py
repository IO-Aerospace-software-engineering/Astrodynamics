"""Plot the step studies of the force partials from the CSV files written by the tests.

Usage: python plot_step_studies.py <directory>

The directory holds fd-step-study.csv (FiniteDifferenceStepStudyTests) and density-step-study.csv
(DensityGradientStepStudyTests); the figures are written next to them, as fd-step-study.png and
density-step-study.png. Each figure has its CSV beside it in the documentation, as its table view.
"""

import csv
import sys
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402

# Categorical slots in fixed order, validated for colour-vision deficiency on adjacent pairs (light surface). The state
# is the secondary encoding, by line style.
SERIES = ["#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#6250d6", "#e34948"]
STYLES = {"LEO 400 km": "-", "LEO 800 km": "--", "GEO": ":", "HEO perigee": "-."}
SURFACE = "#fcfcfb"
INK = "#0b0b0b"
MUTED = "#52514e"
GRID = "#e4e3df"


def read(path):
    with open(path, newline="", encoding="utf-8") as stream:
        return list(csv.DictReader(stream))


def style(axis, title, ylabel=None):
    axis.set_xscale("log")
    axis.set_yscale("log")
    axis.set_facecolor(SURFACE)
    axis.grid(True, color=GRID, linewidth=0.6)
    axis.set_title(title, color=INK, fontsize=10)
    axis.set_xlabel("relative step δ", color=MUTED)
    if ylabel:
        axis.set_ylabel(ylabel, color=MUTED)
    axis.tick_params(colors=MUTED)
    for spine in axis.spines.values():
        spine.set_color(GRID)


def default_step(axis, value, label):
    axis.axvline(value, color=INK, linestyle=(0, (3, 3)), linewidth=0.8, label=label)


def legend(figure, axes, columns):
    handles, labels = [], []
    for axis in axes:
        for handle, label in zip(*axis.get_legend_handles_labels()):
            if label not in labels:
                handles.append(handle)
                labels.append(label)
    figure.legend(handles, labels, loc="lower center", ncol=columns, frameon=False, fontsize=8,
                  labelcolor=MUTED)


def plot_fd_study(directory):
    rows = read(directory / "fd-step-study.csv")
    forces = list(dict.fromkeys(r["force"] for r in rows))
    figure, axes = plt.subplots(1, 3, figsize=(17, 5.6), facecolor=SURFACE)
    panels = [("dadr", "relative_error", "∂a/∂r, relative to the block of the force"),
              ("dadr", "scaled_error", "∂a/∂r, scaled by the central point-mass block"),
              ("dadv", "relative_error", "∂a/∂v (drag), relative")]
    for axis, (block, column, title) in zip(axes, panels):
        for index, force in enumerate(forces):
            for state, line in STYLES.items():
                points = sorted((float(r["relative_step"]), float(r[column])) for r in rows
                                if r["force"] == force and r["state"] == state and r["block"] == block)
                if points:
                    axis.plot([p[0] for p in points], [max(p[1], 1e-20) for p in points], line,
                              color=SERIES[index % len(SERIES)], linewidth=1.4,
                              label=force if state == "LEO 400 km" else None)
        default_step(axis, 3e-6, "default δ = 3e-6")
        style(axis, title, "‖ΔJ‖_F / ‖J‖_F" if axis is axes[0] else None)
    for state, line in STYLES.items():
        axes[0].plot([], [], line, color=INK, label=state)
    figure.suptitle("Default central differences of the force partials: error against the relative step "
                    "(2021-03-20 12:00 UTC)", color=INK)
    legend(figure, axes, 5)
    figure.tight_layout(rect=(0, 0.14, 1, 0.95))
    figure.savefig(directory / "fd-step-study.png", dpi=110, facecolor=SURFACE)


def plot_density_study(directory):
    rows = read(directory / "density-step-study.csv")
    states = list(dict.fromkeys(r["state"] for r in rows))
    figure, axes = plt.subplots(1, 2, figsize=(12, 5), facecolor=SURFACE)
    panels = [("gradient", "∇ρ by central differences of the density chain"),
              ("dadr", "∂a/∂r of drag, relative to Ridders' reference")]
    for axis, (quantity, title) in zip(axes, panels):
        for index, state in enumerate(states):
            points = sorted((float(r["relative_step"]), float(r["relative_error"])) for r in rows
                            if r["state"] == state and r["quantity"] == quantity)
            axis.plot([p[0] for p in points], [p[1] for p in points], STYLES.get(state, "-"), color=SERIES[index],
                      linewidth=1.6, label=state)
        default_step(axis, 3e-7, "default δ_ρ = 3e-7")
        style(axis, title, "relative error" if axis is axes[0] else None)
    figure.suptitle("Density gradient of the drag partials: error against the relative step "
                    "(NRLMSISE-00, 2021-03-20 12:00 UTC)", color=INK)
    legend(figure, axes, 4)
    figure.tight_layout(rect=(0, 0.08, 1, 0.94))
    figure.savefig(directory / "density-step-study.png", dpi=110, facecolor=SURFACE)


if __name__ == "__main__":
    target = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).parent
    plot_fd_study(target)
    plot_density_study(target)
