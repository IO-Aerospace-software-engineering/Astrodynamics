---
title: API Reference
---

# API Reference

Systematic documentation of all types organized by domain.

## Core

Foundation services for kernel loading, time, frames, celestial bodies, and spacecraft.

- [SpiceAPI & Data Providers](spice-api.md) — kernel loading, SPICE operations, data provider pattern
- [Time & Windows](time-and-windows.md) — `Time`, `Window`, time frame conversions
- [Frames & Orientation](frames.md) — reference frames, `StateOrientation`, Earth orientation
- [Celestial Bodies](celestial-bodies.md) — `CelestialBody`, `CelestialItem`, physical properties
- [Spacecraft](spacecraft.md) — spacecraft modeling, components, body axes
- [Sites](sites.md) — ground sites, launch sites, horizontal coordinates

## Orbital Mechanics

Orbit representations and conversions.

- [StateVector](state-vector.md) — Cartesian position/velocity state
- [Keplerian Elements](keplerian-elements.md) — classical orbital elements
- [TLE](tle.md) — Two-Line Elements, SGP4/SDP4 propagation

## Propagation

Numerical integration and force models.

- [Propagators](propagators.md) — `CentralBodyPropagator`, `TLEPropagator`, propagation solution
- [Integrators](integrators.md) — `VVIntegrator`, `RK78Integrator`
- [Force Models](force-models.md) — gravity, drag, SRP, albedo, thermal
- [Propagator Builder](propagator-builder.md) — fluent builder API
- [Event Detection](event-detection.md) — g-function zero-crossing, `BisectionEventFinder`
- [Covariance Provenance](covariance-propagation-provenance.md) — source and tests of each covariance equation

## Maneuvers

Orbital and attitude maneuvers.

- [Impulse Maneuvers](impulse-maneuvers.md) — event-driven orbital maneuvers
- [Attitudes](attitudes.md) — pointing modes, TRIAD attitude
- [Lambert Solver](lambert.md) — transfer orbit computation

## SSA

Space situational awareness.

- [Conjunction Assessment](conjunction-assessment.md) — screening, analysis, collision probability
- [Avoidance Studies](avoidance.md) — impulsive avoidance trade studies

## CCSDS

Standards-compliant data interchange.

- [CDM](cdm.md) — Conjunction Data Message
- [OMM](omm.md) — Orbit Mean-elements Message
- [OPM](opm.md) — Orbit Parameter Message

## Utilities

Math, constants, and predefined objects.

- [Math](math.md) — `Vector3`, `Quaternion`, `Matrix`, `Lagrange`, `Tsiolkovski`
- [Enumerations](enumerations.md) — `Aberration`, `OccultationType`, `ShapeType`, etc.
- [Predefined Objects](predefined-objects.md) — solar system bodies, barycenters, frames
