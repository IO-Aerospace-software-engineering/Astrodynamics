# API ergonomics review of 11.0.0: the complete design (#668)

Annex of the design pull request. The pull request description is the design in short; this file holds every detail it cites: the four cross-cutting decisions with their alternatives, sub-choices and risks, each recommendation with its measured results, its open choices (one recommendation and the alternative each), its candidate defects with their evidence, the plan of short PRs, the dependencies and the claims still to measure.

Base commit 2893404 (main, 11.0.0 untagged). Nothing here is implemented. Paths such as `study/...`, `verify/...` and `work/...` are the study's scratch programs (see Provenance). This file is removed before anything merges.

**Decisions.** D1 to D4 approved by S. Guillet, 2026-11-13. The three references of R19 and R22 (the default NAIF id −100000 − NORAD, the RTN axes of an ITRF state, SGP/SGP4 as a MEAN_ELEMENT_THEORY value) confirmed by S. Guillet, 2026-11-13; the recommendations R01 to R29 await his review.

**Precedence.** Section C (resolutions of the completeness review) prevails over the text of the decisions and recommendations where they differ.

## Contents

- A. Decisions D1 to D4
- B. Recommendations R01 to R29
- C. Resolutions of the completeness review
- D. Plan of short PRs
- E. Dependencies the sections did not state
- F. Claims to measure again in the implementation PRs
- G. Candidate defects (each to get its own issue and PR)
- H. Provenance

## A. Decisions

### D1. One fine-control mechanism: one sealed options record per operation, `options = null` last, null = inferred, object-owned quantities on their objects with conflicts rejected, old paths [Obsolete] until 12.0.0

**Rule.**

1. Each operation (propagation, search, conjunction analysis, CCSDS read/write, batch) takes one sealed record <Operation>Options: init-only properties, `public static X Default { get; } = new();`, passed last as `options = null` (null = Default), before out parameters and a CancellationToken. A variant is `X.Default with { ... }`. No bool flags, builders or Func factories; no public member takes two bools. CancellationToken and IProgress<T> are parameters.
2. A constant default is a non-nullable initializer. A default that depends on the inputs is nullable: null = inferred. A setting that applies to some calls only is nullable; set where it cannot apply, it throws. Default reads no PlanetsAndMoons, Stars or Barycenters (#623).
3. A one-property check runs in init (ArgumentOutOfRangeException naming the property; NaN rejected), so `with` runs it again. Cross-property and input rules run at the start of the call, before any state changes.
4. Durations, steps and time tolerances of a tick or more are TimeSpan. A tolerance useful below 100 ns is a double named ...Seconds (BisectionToleranceSeconds: 1e-9 s is 0 ticks). Docs use FromMilliseconds(43), not FromSeconds(0.043) (429999 ticks).
5. Options hold values only; an IReadOnlyList<T> is stored as a value-equal copy. Never an integrator, force, field or factory: each run builds its own. Integrators: closed abstract record IntegratorOptions, sealed RK78IntegratorOptions and VVIntegratorOptions.
6. What an object is stays on it (body GM, geopotential, atmosphere, albedo; spacecraft mass, Cd, Cr, HBR). An options value of the same quantity is a fallback: object only, the object's; options only, the options'; both equal, that value; both different, ArgumentException naming both; neither, the documented default.
7. No base class. Shared groups are contained (PropagationOptions.Covariance); siblings repeat a shared setting with the same name, type, default and check.
8. Maneuvers take optional named constructor parameters after their target (engine, minimumEpoch, holdDuration, maneuverCenter; null = inferred), not init (MinimumEpoch is computed).
9. Each replaced entry point is [Obsolete] (one DiagnosticId) and [EditorBrowsable(Never)] in 11.0.0, forwards to the options path with internal legacy settings, bit for bit, and is removed in 12.0.0. The IIntegrator constructor of CentralBodyPropagator and RK78Integrator's double constructor stay.
10. PropagationSolution and EncounterCase carry the resolved options.

**Recommendation and evidence.**

Adopt the rigor-first proposal as the base and add five ideas from the adoption-first one. The two proposals agree on the core: sealed records with init, a static Default, `with`, `options = null` last, TimeSpan except below the tick, integrators as values, object attributes on objects, maneuvers by constructor parameters, and old paths [Obsolete] until 12.0.0. They disagree on four points. I checked each one against the source or by compiling (work/d1-judge).

1. Base class for the search options. The rigor proposal wins: no base.
- A FieldOfViewSearchOptions passed to a method typed WindowSearchOptions compiles, and its TargetShape is dropped without an error (measured).
- That breaks the rule both proposals share: a value that is set but cannot apply throws.
- Base and derived records with equal shared members compare unequal (measured False).
- DRY is kept inside the library: internal shared constants and one internal validator. The reflection test checks that siblings repeat each shared setting with the same name, type, default and check.

2. Precedence when an object and the options both give a value. The rigor proposal wins: the options value is a fallback, and a conflict throws.
- Today's override is a silent drop. Analyze adds options.SecondaryHardBodyRadiusMeters to the protected asset's HBR (ConjunctionAssessment.cs:2404) and never reads the secondary's own Spacecraft.HardBodyRadius (Spacecraft.cs:105). The study measured the Pc 33 % high.
- A strict rule can be relaxed later without breaking a caller. A loose rule cannot be tightened later without breaking one.
- The degree sweep keeps working, because PlanetsAndMoons.EARTH_BODY carries no geopotential (PlanetsAndMoons.cs:176).
- Cd and Cr never go into options, because Spacecraft.ToOpm writes them (Spacecraft.cs:967-969).

3. What the old paths do. The rigor proposal wins: they forward to the one options path, with internal legacy settings: the Velocity-Verlet default at the output step, each path's central-body rule, and the albedo/thermal body as given.
- This keeps one force-building engine (DRY).
- The existing test call sites become the legacy reference, and the full-matrix tests prove the forwarding bit for bit.
- The adoption proposal's "keep their code" would leave two engines to maintain for a release.

4. CovariancePropagationOptions.Mode. The adoption proposal wins: Mode becomes nullable, and null means inferred.
- Null resolves to Covariance inside the analysis or when ProcessNoise or ConsiderParameters is set, and to StateTransitionOnly otherwise. A prototype resolves it (measured).
- Today `new CovariancePropagationOptions { ProcessNoise = q }` fails with 'Mode must be Covariance' (CovariancePropagationOptions.cs:50-65). Presetting ConjunctionAnalysisOptions.Default only moves that error.
- Every call that runs today keeps its result. Reading Mode now returns a nullable type, a source break listed under Breaking.

Also taken from the adoption proposal:
- [EditorBrowsable(Never)] on obsolete members, so a newcomer's IntelliSense shows one path.
- Try* methods get an overload pair: (path, out, out) and (path, options, out, out).
- CancellationToken and IProgress are parameters. BatchOptions.CancellationToken and Progress (BatchOptions.cs:22, :27) become obsolete forwarders.
- The docs use exact TimeSpan factories: FromSeconds(0.043) is 429999 ticks, FromMilliseconds(43) is 430000 (measured).
- The reflection test also rejects a double named *Step*, *Duration* or *Tolerance* without a Seconds suffix.
- One suffix for every settings type, Options. MonteCarloConfiguration keeps its name and takes a PropagationOptions member.

Kept from the rigor proposal:
- The IntegratorOptions hierarchy is closed (private protected constructor). Integrator.WithSameSettings returns null for any subclass outside the library (Integrator.cs:119), so a user-defined subclass could not be built. The nested-`with` idiom compiles through a typed local (measured).
- Default reads no SPICE-backed static class (#623). A ColdStart child process checks this.
- One DiagnosticId, so a TreatWarningsAsErrors build can suppress it.
- RK78Integrator's double constructor is not renamed and gets no TimeSpan overload: the overload would make new RK78Integrator() ambiguous (CS0121), and a rename breaks 36 named call sites. The TimeSpan steps live in RK78IntegratorOptions.

Before:
  spacecraft.Propagate(window, bodies, new RK78Integrator(1e-12, 1e-12), true, true, TimeSpan.FromSeconds(60), cov);
  opm.SaveToFile("p.xml", true, false, true);
After:
  spacecraft.Propagate(window);
  var rk = new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 };
  spacecraft.Propagate(window, PropagationOptions.Default with { Integrator = rk, AtmosphericDrag = true, SolarRadiationPressure = true, Covariance = cov });
  opm.Save("p.xml", new CcsdsWriteOptions { WrapInNdmContainer = false });

Verifier corrections applied:
- `options.Integrator with {...}` on an abstract-typed property does not compile (CS0117). Use a typed local.
- ImmutableArray gives no value equality, and new AvoidanceSearchOptions() == new() is False. Collections are stored as an internal value-equal copy, which also stops a caller's array from changing the options after init.
- BisectionToleranceSeconds stays a double. It is not a pure rename: 1e-9 s is 0 ticks, and the value drives the clamp at ConjunctionAssessment.cs:1027-1030.
- J's sealed class becomes a sealed record.
- CovariancePropagationOptions is kept, not flattened.
- Each public switch of OmmValidator, OpmValidator, OmmWriter, OpmWriter and CdmWriter gets a field in CcsdsReadOptions or CcsdsWriteOptions before those classes become internal.
- A non-zero AdjustValue with a non-absolute operator throws. This is listed under Breaking, since CSPICE gfposc also ignores the value.
- The maneuver center defaults to the initial orbit's observer only when it is a CelestialBody, and is required otherwise (SSB mode). Engine resolution goes with #586.
- SetStandbyManeuver(m, minimumEpoch) stays [Obsolete], because it is the only lasting gate (#573).
- GeopotentialModelParameters is single-use (GeopotentialModelReader.cs:19). Fixing it is a prerequisite.

Each gets its own issue and its own PR, not this change:
- #640 (the OutOfMemoryException) ships first and alone.
- RK78Integrator accepts NaN tolerances (RK78Integrator.cs:114-116).
- The secondary's HBR ignored at ConjunctionAssessment.cs:2404. It changes the Pc, so it is measured and stated.

Where the owner's criteria conflict, I favour explicit errors and FDG over brevity:
- no base class, at the cost of three repeated properties;
- a conflict throws instead of a silent override.
Both cost a newcomer one line at most, and both can be relaxed later without a break.

Tests:
- A reflection test over the public surface. Every *Options type is a sealed record (or the closed IntegratorOptions) with Default equal to new(), no setter, and no Integrator, ForceBase or Func member. The options parameter is last before out parameters and a CancellationToken. No member has two or more bools. No double named *Step*, *Duration* or *Tolerance* lacks the Seconds suffix. Sibling settings agree.
- One theory per property: an invalid value or NaN throws through init and through `with`, naming the property.
- Collection equality and hash codes follow the contents.
- The precedence table: 5 rows per object-owned quantity.
- A ColdStart child process reads every Default before any kernel is loaded.
- Each options path is bit for bit equal to its [Obsolete] path over both integrators, drag, SRP, albedo, thermal, the covariance modes, maneuvers, SSB mode and a non-ICRF start.
- CCSDS: byte-identical XML through the new verbs.

**Alternatives not taken.**

- Adoption-first proposal as a whole: one WindowSearchOptions base for the searches, options override objects, obsolete paths keep their own code. Not taken. A derived options object passed to a base-typed search compiles and loses its own members silently (measured). Overriding objects is today's silent HBR drop (ConjunctionAssessment.cs:2404). Two engines would be maintained for a release.
- Sealed classes with init and hand-written With* methods: no `with` and no value equality, and all 9 existing options types would be retyped. Rejected.
- Mutable options classes (Microsoft.Extensions.Options style): options are shared across threads by Scenario, Batch and MonteCarlo, and any caller could change a mutable static Default. Rejected.
- Fluent builders per operation (CentralBodyPropagatorBuilder generalised): a second concept beside options, single-use state, and 'already called' failure modes (CentralBodyPropagatorBuilder.cs:43-96). Rejected; the builder becomes [Obsolete].
- Optional positional parameters on each method: each new setting is a binary break after 11.0.0, and the 23 members with two or more bools stay positional. Rejected.
- Options override objects ('nearest scope wins'): simpler to state, but it keeps the silent drop that makes the analysis Pc 33 % high. Rejected for 11.0.0; it can still be relaxed to this later without breaking anyone.
- Integrator instances in options, cloned with WithSameSettings: an Integrator is mutable (AddForce, Initialize; CentralBodyPropagator.cs:46-49), and a subclass from outside the library clones to null (Integrator.cs:119). Rejected.
- TimeSpan for every time quantity, tolerances included: 1e-9 s is 0 ticks (measured), so the conjunction results would change. Rejected.
- Remove the replaced entry points in 11.0.0: it forces the migration of every caller and every test call site for no difference in results, and unmigrated callers would get no compile-time hint. Deferred to 12.0.0.
- Preset ConjunctionAnalysisOptions.Default.CovariancePropagation instead of a nullable Mode: `new CovariancePropagationOptions { ProcessNoise = q }` would still fail with 'Mode must be Covariance'. Rejected for null = inferred.

**Sub-choices.**

1. **Record or sealed class with init.** Recommendation: Nominal sealed record: init properties, a parameterless constructor, no positional parameters. Data work items such as PropagationTask(Spacecraft, Window, PropagationOptions Options = null) stay positional records. All 9 existing options types are already records. Alternative: Sealed class with init and hand-written With* and Equals.
2. **A static Default.** Recommendation: `public static X Default { get; } = new();` on every options type. A test checks Default == new(). Default holds only constants and nulls, and reads no PlanetsAndMoons, Stars or Barycenters (#623); a ColdStart child process checks this. Alternative: No Default; callers write new().
3. **Deriving a variant.** Recommendation: `X.Default with { ... }` is the one documented idiom. A nested integrator goes through a typed local (`var rk = new RK78IntegratorOptions {...}; o with { Integrator = rk with {...} }`), because a `with` on the abstract-typed property does not compile (CS0117, measured). Alternative: Fluent With* methods per property.
4. **Where validation runs.** Recommendation: A single-property check runs in init: ArgumentOutOfRangeException, ParamName = the property name, NaN and infinity rejected through !(x > 0) || !double.IsFinite(x). It runs again on `with` (measured). Rules across properties or on the inputs run at the start of the call, before any state changes, and throw ArgumentException naming options. Examples: MaximumEventSearchStep against SampleStep, consider parameters without their force, a setting that cannot apply. #640 ships first, alone. Alternative: Everything at use, as ValidateAnalysisOptions does today for 4 of 11 properties (ConjunctionAssessment.cs:2888-2926).
5. **Parameter position and name.** Recommendation: Last, named options, `= null` meaning Default. It goes before out parameters (Try* methods get a pair: (path, out, out) and (path, options, out, out)) and before `CancellationToken cancellationToken = default`. CancellationToken and IProgress<T> are parameters, never options members; BatchOptions.CancellationToken and Progress become obsolete forwarders. Alternative: An extra overload per method (the occultation pattern, public-api.txt:216-217) and no optional parameter.
6. **How 'inferred' is expressed.** Recommendation: A nullable property, with the inference rule in its XML doc. A constant default is a non-nullable initializer. A setting that applies to some calls only is nullable too, so 'set where it cannot apply' can be detected and throws (AdjustValue with a non-absolute operator, listed as Breaking). CovariancePropagationOptions.Mode becomes nullable. PropagationSolution and EncounterCase expose the resolved options with no null left, so a caller can see and pin what was inferred. Alternative: An Auto enum member, or sentinels such as TimeSpan.Zero = inferred.
7. **Durations: TimeSpan or double seconds.** Recommendation: TimeSpan for every duration, step, period, lead time and time tolerance of a tick or more. A tolerance useful below 100 ns is a double named ...Seconds: only BisectionToleranceSeconds today (1e-9 s is 0 ticks, measured). RK78IntegratorOptions takes TimeSpan steps; its 60 s, 1 µs and 86400 s defaults are exact. Docs use exact factories (FromMilliseconds(43) is 430000 ticks; FromSeconds(0.043) is 429999). Alternative: Double seconds with a Seconds suffix everywhere, against the 113 existing TimeSpan lines.
8. **Object attributes against per-call options, and precedence.** Recommendation: What an object is stays on it: a body's GM, radii, geopotential, atmosphere, albedo and emissivity; a spacecraft's mass, areas, Cd, Cr and HBR. Cd and Cr stay on the spacecraft because Spacecraft.ToOpm writes them (Spacecraft.cs:967-969). An options value of the same quantity is a fallback named for that role (e.g. DefaultSecondaryHardBodyRadiusMeters, ScreeningOptions.cs:36). When both are given and differ, an ArgumentException names both, before any state changes. Models compare by value, instances by reference, never by NAIF id. Albedo and thermal forces read the body's coefficients (today BuildCentralBodyForces never does, CentralBodyPropagator.cs:227-258). Alternative: The option overrides the object (option, then object, then default).
9. **Integrator settings: values or instances.** Recommendation: Values. IntegratorOptions is a closed abstract record (private protected constructor, CS7036 outside the assembly). RK78IntegratorOptions holds every RK78Integrator constructor parameter; VVIntegratorOptions holds Step. Each run builds a fresh integrator. This removes Func<Integrator> from Scenario, PropagationTask and MonteCarlo, and removes the shared-instance guard of Scenario.SimulateAsync. The CentralBodyPropagator IIntegrator constructor stays, not obsolete, for a user-built force list. Alternative: Integrator instances in options, cloned internally with WithSameSettings.
10. **Maneuver and attitude definitions.** Recommendation: Optional named constructor parameters after the target: engine, minimumEpoch, holdDuration, maneuverCenter; null means inferred. The center is the initial orbit's observer only when it is a CelestialBody, and is required otherwise. Engine resolution is designed with #586. MinimumEpoch is computed (Maneuver.cs:74), so init would not read back what was set. The old constructors stay [Obsolete], with no overload collision. The targetOrbit forms are covered bit for bit. Alternative: Init properties, with MinimumEpoch split into a given and an effective public property.
11. **Base class.** Recommendation: None, across or within families. Shared groups are contained: PropagationOptions.Covariance, MonteCarloConfiguration.Propagation, and Screening/Avoidance holding a ConjunctionAnalysisOptions. WindowSearchOptions, OccultationSearchOptions and FieldOfViewSearchOptions stay separate sealed records. They repeat Step, Aberration and ConvergenceTolerance with the same names, types, defaults and checks, through internal shared constants and one validator, and the reflection test enforces it. A derived record passed to a base-typed parameter drops its members silently (measured). Alternative: An unsealed WindowSearchOptions base with OccultationSearchOptions and FieldOfViewSearchOptions deriving from it.
12. **Fate of CentralBodyPropagatorBuilder and the existing constructors and overloads.** Recommendation: [Obsolete] under one DiagnosticId, with a message naming the replacement, plus [EditorBrowsable(Never)], in 11.0.0. This covers the builder, the flag constructors, the Propagate/PropagateAsync/SimulateAsync flag overloads, PropagationTask's positional constructor, the MonteCarlo factories, flags and DeltaT, the CCSDS Load*/Read*/Save*/Write* verbs, the public Reader/Writer/Validator switches, and the positional searches. Each forwards to the one options path with internal legacy settings that keep its results bit for bit. All are removed in 12.0.0. Alternative: Each obsolete member keeps its own code path until removal.
13. **Naming of settings types.** Recommendation: One suffix, Options: RK78IntegratorOptions, VVIntegratorOptions (the existing integrator type name plus Options), CcsdsReadOptions, CcsdsWriteOptions, WindowSearchOptions. MonteCarloConfiguration keeps its name, because its members are required inputs of a run. Alternative: VelocityVerletIntegratorOptions (FDG: no abbreviations), breaking the symmetry with VVIntegrator.

**Risks.**

- Two entry points per operation for one release. The forwarding through internal legacy settings must be proven bit for bit on the full matrix: both integrators, every force, the covariance modes, maneuvers, SSB mode and a non-ICRF start. Coverage stays above 95 % until 12.0.0.
- The options path takes the builder's central-body rule (CentralBodyPropagatorBuilder.cs:138-143). A caller who migrates from Spacecraft.Propagate without listing the central body gets different results with drag or SRP. The R06 PR measures and states this; the obsolete overloads keep the old rule.
- The strict conflict rule makes a call throw when a body carries a geopotential or atmosphere and the options give a different one. The message must name both values and the fix. The rule can be relaxed later without a break.
- Applying the precedence rule to the secondary's HBR changes the Pc (33 % in the study's case). It ships as its own issue and PR, measured and stated, not inside this change.
- Thread safety rests on options holding descriptions. The single-use GeopotentialModelParameters fix (GeopotentialModelReader.cs:19) and per-run field construction must land before CelestialBodiesFactory becomes obsolete.
- Nullable CovariancePropagationMode is a source break for code that reads Mode. It is listed under Breaking.
- Init checks reject values accepted today (negative or NaN steps and tolerances, MaxResults 0, a non-zero AdjustValue with a non-absolute operator). Each is a Breaking entry.
- TreatWarningsAsErrors builds fail on [Obsolete] until the user migrates or suppresses the one DiagnosticId, which the release notes name. [EditorBrowsable(Never)] hides members only for package references.
- A value-equal collection type behind IReadOnlyList<T> breaks a caller who cast LeadTimes to TimeSpan[]. A hand-written Equals must cover any member added later; the reflection test checks this.
- Users who migrate double seconds through TimeSpan.FromSeconds can be one tick short. This is harmless for a step but is not bit for bit; the migration table uses the exact factories.

### D2. Naming, namespaces, usings and the migration of renames

**Rule.**

N1 Casing (FDG 3.1.2): PascalCase, camelCase parameters, no underscore. A 2-letter acronym in capitals (TT, JD), a longer one as a word (Tle, Utc, Tdb, Icrf, Isp, Api, Ccsds). No name is exempt: SpiceAPI becomes SpiceApi.
N2 Symbols: an enum member, or a static instance declared on its own type, whose whole name is a SPICE, IAU/IERS or CCSDS symbol is written as the standard writes it: Frame.ICRF, TEME, ECLIPJ2000, GALACTIC; Aberration.LT; TimeScale.UTC, TT, TDB; CcsdsTimeSystem.UTC; CdmReferenceFrame.EME2000, GCRF, ITRF. Other names follow N1 (ToUtc, J2000Tdb, GetStateOrientationToIcrf, TemeFrame); catalogue fields are not symbols (PlanetsAndMoons.Earth, GroundStations.Dss13).
N3 Words: full English, one term per concept: Sidereal, Derivative, Tsiolkovsky; RelationalOperator with ExpressionType's names (LessThan, GreaterThanOrEqual...), AbsoluteMin and LocalMax kept, values and [Description] kept; TLE fields take their OMM names; epoch for every Time parameter; TimeScale, Time.Scale, TT, no TDT; Max/Min for bounds; HalfPi, TwoPi; a parameter is named as the member it sets.
N4 A namespace changes only to fix a defect users hit or to hide types: OrbitalParameters.TLE (CS0426, CS0104), TimeSystem.Frames and Atmosphere.NRLMSISE_00 merge into their parents; CCSDS.* becomes one Ccsds (no duplicate name); SSA becomes Ssa, PDS Pds, Schemas Pds.V4.Schemas; DTO, helpers and solvers internal. No plural rename; Body.Spacecraft stays (a merge gives CS0138 on every old using).
N5 Migration: a member of a type that keeps its name, shipped in 10.1.0, renamed by more than case, neither virtual nor an interface member, keeps an [Obsolete(DiagnosticId = "IOA0001")] [EditorBrowsable(Never)] forwarder equal to its target until 12.0.0. RelationnalOperator and TimeFrame become obsolete static classes of forwarding members. All else is hard: types, namespaces, parameters, enum members, virtual and interface members, case-only renames (VB BC31429), the catalogues as one block, names first shipped after 10.1.0. versioning.md carries the table and a sed script.
N6 No number, SPICE string, CLI input or file changes; only Time.ToString of TT ends TT, not TDT.
N7 build/IO.Astrodynamics.props imports root, Body, Body.Spacecraft, TimeSystem, Frames, OrbitalParameters, SolarSystemObjects, Propagator, Propagator.Integrators, Maneuver, Math when ImplicitUsings is on, unless IOAstrodynamicsImplicitUsings=false.
N8 CI: PublicApiAnalyzers, CA naming errors, NamingRuleTests, cspell.

**Recommendation and evidence.**

Adopt N1 to N8 in 11.0.0, before the tag. Land them as one migration with R02 (catalogue shape, EARTH vs EARTH_BODY), D3 (TimeScale), D1 (options), R27, R04 (DTO, IDataProvider) and R21 (Tle), so each user line changes once. The base is the adoption-first proposal. Six parts come from the rigor-first one.

Checks against the source and by compiling (work dir design/work/d2-judge/):

1. Acronyms (values vs tokens). The rigor proposal keeps frame designators in capitals in every name. Taken strictly, that renames the public types TemeFrame, GcrfFrame, CirsFrame and TirsFrame to TEMEFrame and so on (public-api.txt:1673-1743). Users build these types (`new TemeFrame(eop)`, frames.md:87,174), and the rigor proposal does not list them. The values rule needs no list of names. It follows FDG in every name the library coins, and it keeps the exact SPICE and CCSDS strings where users type them as values.
   - Cost of the values rule: 9 *ICRF names become *Icrf (161 test lines, 25 doc lines). This includes IDataProvider.FrameTransformationToICRF and 4 Frame virtuals. R04 already breaks IDataProvider.
   - Cost of the token rule: CcsdsTimeSystem changes on 14 test lines, and the 4 frame types also change.
   - D3's after-code writes TimeScale.UTC and Tt. Under N2 they are TimeScale.UTC and TT.
   - Frame.ECLIPTIC_J2000, ECLIPTIC_B1950 and GALACTIC_SYSTEM2 take the SPICE strings they pass: ECLIPJ2000, ECLIPB1950 and GALACTIC (Frame.cs:99-109). These match CcsdsReferenceFrame (public-api.txt:761). This part comes from rigor's designator list.

2. Catalogues. The 7 classes have 411 public static members. 247 of them have no underscore, so MERCURY to Mercury is a case-only rename. The rigor proposal forwards every catalogue member, which goes against its own case-only rule. A case-only twin breaks VB and triggers CA1708. So the catalogues are a hard rename, done as one block with a generated table, as the adoption proposal says.

3. Forwarder cutoff. The CDM shipped in 10.1.0: rigor's apidiff old.txt holds 328 CCSDS.CDM entries. Adoption's "the CDM is new in 11.0.0" is wrong. The rigor proposal's cutoff (members of the last stable package, 10.1.0) is the right test.

4. Obsolete static classes. Adoption's RelationnalOperator and TimeFrame static classes rebuilt and ran. Old arguments and case labels compile with an IOA0001 warning, and Enum.GetNames has no duplicate names. Only RelationnalOperator renames enum values by more than case, so no enum gets aliases.

5. Body.Spacecraft. Merging it into Body gives CS0138 on the old using (compiled). It would break 113 test files, 50 library files and every user's spacecraft file, and gain nothing once implicit usings import both. It stays. This part comes from rigor.

6. Implicit usings. Math stays in the implicit set, against rigor. The StateVector constructor takes Vector3 (public-api.txt:2250), and 16 doc pages use Vector3 or Quaternion. CS0104 with System.Numerics occurs only in files that import it explicitly, and the SDK's implicit set does not include it. List it under Known limitations.
   - Within the 11 namespaces, the only duplicate simple name is Configuration, which TleFitOptions removes (scan: types.txt).
   - Merging CCSDS.* (63 types) creates no duplicate.
   - EventInfo (Propagator, 0 doc or test uses) is renamed so it does not clash with System.Reflection. This part comes from rigor.

7. Max/Min. 11 public names use Max and 6 use Maximum/Minimum. The BCL uses Max and Min. The 6 are forwarded: all shipped in 10.1.0, and MinimumEpoch becomes MinEpoch.

Where the owner's criteria conflict: SpiceAPI becomes SpiceApi (criterion 2 over criterion 1). Every 10.1.0 program already breaks on the catalogues and on TimeFrame, so this adds one sed line to a migration users must do anyway. It also removes the only named exception. The tests already call the file SpiceApiTest.cs.

Migration (also from rigor):
- Forwarders go in partial *.Obsolete.cs files, so 12.0.0 deletes those files.
- IOA0001 is the same DiagnosticId as D1 rule 9.
- The sed script is applied to the repository's own tests and docs in each rename PR, so the script itself is tested.
- Release note under Changed (Breaking): the old-to-new table, the namespace table with a global-using block, and the 411-row catalogue table, all generated. Forwarders are listed under Deprecated.

Landing order (from rigor):
1. This design.
2. An infrastructure PR. It adds Microsoft.CodeAnalysis.PublicApiAnalyzers with PublicAPI.Shipped.txt seeded from main, without the '#nullable enable' header (Nullable is disabled; the header gives RS0041). It also adds AnalysisModeNaming=All at warning level and NamingRuleTests with a baseline.
3. One rename PR per area, with its docs. Each shows the test suite and the conformance report unchanged (report_meta ignored).
4. A final PR that turns the analyzers into errors, empties the baseline and generates the tables.

Renames change no dynamics, so long-validation.yml is not needed. The CLI parses operators by [Description] and aberrations by name with ignoreCase, so its syntax does not change.

CI after step 4:
- CA1050, 1707, 1708, 1711, 1716, 1720, 1721 and 1725 as errors. CA1724 is suppressed with a justification for the listed type and namespace pairs. PDS is marked generated_code.
- RS0016 and RS0017 as errors.
- NamingRuleTests checks:
  - no run of 3 or more capitals outside the N2 value list;
  - a deny-list: Relationnal, Sideral, Derivation, rigth, Oject, Dictionnary, Tsiolkovski, Bissection;
  - no exported type outside IO.Astrodynamics.*;
  - no duplicate simple name in the implicit set or against the SDK's implicit namespaces;
  - each IOA0001 forwarder is ReferenceEquals or bit for bit its target and has its row in the table.
- cspell on the added lines of PublicAPI.Unshipped.txt.
- The #662 docs job compiles the doc blocks with the implicit usings on.

Defects found during a rename get their own issue. One example: SideralRotationPeriod truncates through TimeSpan.FromSeconds (CelestialBody.cs:253).

**Alternatives not taken.**

- Token rule (rigor-first R2): one spelling per token on the whole surface, with frame designators and aberration codes in capitals everywhere (GetStateOrientationToICRF kept) and time scales as words everywhere (TimeScale.UTC, CcsdsTimeSystem.Utc). It keeps the 9 *ICRF names, including an interface member and 4 virtuals. But the public TemeFrame, GcrfFrame, CirsFrame and TirsFrame would become TEMEFrame and so on, it departs from FDG in coined names, and it renames CcsdsTimeSystem away from the CCSDS keyword values.
- Keep SpiceAPI as the only listed exception (adoption-first). It saves one sed line on about 650 lines, but the rule then carries a list of names and NamingRuleTests an exception.
- Forward every rename C# accepts, catalogue fields and case-only renames included. No C# error at all, but CA1708 fires and VB callers can use neither name (BC31429, BC30560, compiled by both designers). 247 of the 411 catalogue members are case-only.
- Hard renames only, with the table and sed script (study J-naming-rule). Less code, but about 65 shipped non-catalogue members become CS0117/CS1061 errors with no hint, against the Deprecated-then-Removed precedent of LowThrustManeuver (versioning.md:2467-2470, 1696-1697).
- Plural feature namespaces (Bodies, Orbits, Maneuvers). Fixes CA1724, but rewrites every user's usings for a collision that code outside IO.Astrodynamics.* does not hit (verifier: user gain low).
- No package implicit usings, documented usings only. No CS0104 risk, but a first propagation keeps 10 usings and 112 doc blocks that fail only for missing usings stay as they are.

**Sub-choices.**

1. **Acronym casing.** Recommendation: Values rule (N2). Enum members and static instances on their own type whose whole name is a SPICE, IAU/IERS or CCSDS symbol keep it (Frame.ICRF, Aberration.LT, TimeScale.UTC, CcsdsTimeSystem.UTC, CdmReferenceFrame.GCRF). Every coined name follows FDG (ToUtc, GetStateOrientationToIcrf, TemeFrame, Tle). D3 writes TimeScale.UTC and TT. Alternative: Token rule: designators in capitals everywhere and time scales as words everywhere (TimeScale.UTC, GetStateOrientationToICRF).
2. **SpiceAPI.** Recommendation: Rename it SpiceApi as a hard, case-only rename, one sed line. Every 10.1.0 program already breaks on the catalogues and TimeFrame, and the rule then has no list of named exceptions. Alternative: Keep SpiceAPI as the only listed exception.
3. **Catalogues (PlanetsAndMoons, Stars, Barycenters, Asteroids, Comets, GroundStations, LagrangePoints: 411 members).** Recommendation: PascalCase, hard, as one block with a generated 411-row table. Land it in the PR of R02's EARTH vs EARTH_BODY decision. 247 members are case-only, so they cannot be forwarded. Alternative: Forward the 164 underscore members (EARTH_BARYCENTER to EarthBarycenter) and hard-rename the 247 case-only ones.
4. **Migration form.** Recommendation: Mixed, by what C# can forward. Members of a surviving type that shipped in 10.1.0, are renamed by more than case, and are neither virtual nor interface members get an IOA0001 [Obsolete] and [EditorBrowsable(Never)] forwarder in a partial *.Obsolete.cs file. RelationnalOperator and TimeFrame become obsolete static classes. Everything else is hard, with the table and a sed script tested on the repository. Alternative: All hard renames with the table, or forwarders wherever C# accepts them.
5. **Which identifiers get forwarders.** Recommendation: Only names in the last stable package, 10.1.0 (rigor's apidiff old.txt). The CDM shipped there, so its non-case renames qualify. Names first published in a preview or in 11.0.0 get none. Alternative: Forwarders for every name on main before the tag.
6. **Namespace plan.** Recommendation: Minimal. OrbitalParameters.TLE, TimeSystem.Frames and Atmosphere.NRLMSISE_00 merge into their parents. CCSDS.* becomes one Ccsds. SSA becomes Ssa and PDS becomes Pds. Schemas moves to Pds.V4.Schemas. DTO, helpers and solvers become internal. Body.Spacecraft and the feature namespaces stay. Alternative: Also merge Body.Spacecraft into Body: one using fewer, but CS0138 on every old using (compiled).
7. **Implicit usings.** Recommendation: build/IO.Astrodynamics.props, for direct references only. On when ImplicitUsings is enable or true; opt-out IOAstrodynamicsImplicitUsings=false. 11 namespaces, Math and Maneuver included. Ships in the same release as TleFitOptions and the EventInfo rename. CS0104 with System.Numerics goes under Known limitations. Alternative: 9 namespaces without Math or Maneuver (rigor), so Vector3 needs an explicit using.
8. **Name of OrbitalParameters.TLE.Configuration.** Recommendation: TleFitOptions (D1's <Operation>Options). Its positional parameters take the OMM names. Classification becomes TleClassification. Alternative: TleConfiguration, the alias in tle-and-omm.md:75.
9. **RelationalOperator values.** Recommendation: ExpressionType names (GreaterThan, LessThan, GreaterThanOrEqual, LessThanOrEqual, Equal, NotEqual). AbsoluteMin, AbsoluteMax, LocalMin and LocalMax stay, under the Max/Min rule. Numeric values and [Description] stay. Land in one PR with the 11 doc lines (#483). Alternative: Less and LessOrEqual, with Greater kept.
10. **Max/Min vocabulary.** Recommendation: Max/Min prefixes: 11 of the 17 public names already use them, as the BCL does. Forward MaximumEventSearchStep, MaximumLeadTime, MinimumLeadTime, MaximumOperatingMass, MinimumVectorSeparation and MinimumEpoch (all shipped in 10.1.0). D1's minimumEpoch parameter becomes minEpoch. Alternative: Maximum/Minimum everywhere, which renames the 11 Max names instead.
11. **Frame instances with underscores.** Recommendation: The SPICE names they pass: Frame.ECLIPJ2000, ECLIPB1950, GALACTIC (Frame.cs:99-109), the same as CcsdsReferenceFrame. Forwarded, because each differs by more than case. Alternative: FDG compounds: EclipticJ2000, EclipticB1950, GalacticSystem2.

**Risks.**

- Coordination: D3 must write TimeScale.UTC and TT (its after-code has Utc and Tt). R02 decides EARTH vs EARTH_BODY. D1 renames minimumEpoch. R04 changes IDataProvider. If a rename lands before these, users migrate the same lines twice.
- The values rule gives one token two casings (TimeScale.UTC beside ToUtc, Frame.ICRF beside GetStateOrientationToIcrf). This is stated knowingly, but a reader must learn it. NamingRuleTests holds the closed list of N2 values.
- Hard case-only renames give CS0117/CS1061 errors with no hint: SpiceApi, ToTdb, Tle, the 9 *Icrf names (including IDataProvider.FrameTransformationToIcrf for custom providers and 4 Frame virtuals for subclasses) and the 411 catalogue members. The table and the sed script are the only guide, and the RS0016/RS0017 diff checks that the table is complete.
- Record forwarders (the 10.1.0 option records that D1 does not replace) print both names in ToString during 11.x (rigor measured 'MaximumResults = 7, MaxResults = 7'). Equality and 'with' are unchanged. State this in the release note.
- Implicit usings give CS0104 on Vector3, Quaternion and Plane in a file that imports System.Numerics (measured). App-model implicit usings such as MAUI's are not measured (to be verified by S. Guillet). The fix is an alias or the opt-out property.
- [EditorBrowsable(Never)] hides forwarders only for package references. How Rider handles it is not measured (to be verified by S. Guillet). The repository sets WarningsAsErrors IOA0001 so its own code does not use the old names.
- No CA rule checks acronym casing: CA1709 is not in SDK 10.0.404, and IDE1006 accepts ToUTC. The rule depends on the reflection test.
- Diff size: 564 TimeFrame.*Frame lines, about 392 catalogue lines, 173 RelationnalOperator lines, 483 SpiceAPI lines and 161 *ToICRF lines in the tests, plus the docs. Each area PR must carry its docs, to avoid a repeat of #483.
- Defects outside the scope get their own issues: SideralRotationPeriod truncates through TimeSpan.FromSeconds (CelestialBody.cs:253); the Configuration.cs:14 doc of FirstDerivationMeanMotion contradicts the ṅ/2 semantics.

### D3. Time: a required scale at every entry, equality on the TDB tick, one Parse grammar, nearest-tick arithmetic

**Rule.**

1. Scale. enum TimeScale { TDB = 0, TT, TAI, UTC, GPS } replaces ITimeFrame; default(Time) is 0001-01-01 TDB. Time.Scale replaces Time.Frame. ITimeFrame, TimeFrame and its classes, Time.Frame, ToTDT and the ITimeFrame overloads stay as warning-level [Obsolete] forwarders, bit for bit, until 12.0; casing per D2. Local is not a scale: LocalFrame and ToLocal get [Obsolete(error: true)] naming t.ToUtc().DateTime.ToLocalTime().
2. No implicit scale. Constructors with a required scale last: (y,m,d,scale), (y,m,d,h,mi,s,scale), +ms, +ms,us. The calendar constructor of Time.cs:40 and Time(string) get [Obsolete(error: true)] naming the fix (CS0619), removed in 12.0. Add Time.UtcNow and FromDayOfYear(y, doy, s, scale): year >= 1, doy in the year, s in [0, 86400). Create(int,int,double) is warning-obsolete. new Time(DateTime, scale) throws ArgumentException on Kind Local, and on Kind Utc with a scale other than Utc. Omm.cs:242, Opm.cs:227 and OpmStateVector.cs:116 pass their calendar with Kind Unspecified, bit for bit.
3. Equality. One key, the TDB tick, for Equals, GetHashCode, CompareTo and the six operators. TDB vs TDB unchanged bit for bit. Same scale and ticks 0 or >= 2 apart: decided on the ticks. EqualsExact compares ticks and scale. operator - unchanged (#508); a == b gives |a - b| <= 1 tick, documented. Window equality is that of its ends. Frame.cs:22 and Star.cs:21 key on an internal exact comparer.
4. Parse. Parse, TryParse and ISpanParsable<Time> on one grammar in TimeSystem: ISO 8601 calendar or CCSDS day of year, optional time, the fraction to the nearest tick; then Z or +-hh:mm (the UTC instant), or UTC, TT, TDT, TDB, TAI or GPS, any case, space optional; or 'JD n' / 'n JD' with a tag. Untagged: FormatException, unless Parse(s, scale); a contradicting tag throws. Parse throws FormatException only; TryParse never throws. CcsdsEpochParser's strict branch and the CLI use this core; #619's fallback and the CLI's bare seconds stay theirs.
5. Format. IFormattable and ISpanFormattable: null, G and O give ToString(); any other format is a DateTime custom format then the tag. TT is written TT.
6. AddDays, AddHours, AddMinutes and CreateFromJD round to the nearest tick, ties to even (Time.cs:182); NaN and infinities throw ArgumentOutOfRangeException.
7. CLI: time-converter <epoch> --to tdb|tt|tai|utc|gps|local --format iso|jd|seconds, defaults tdb and iso; -t -u -l -j -e -d aliases; local is iso only; an extra positional argument is an error.

**Recommendation and evidence.**

Take designer 2's proposal (rigor-first) as the base. Graft designer 1's error-level Obsolete and its removal of Local as a scale. Land it as one PR, sequenced with D2's naming table, so each name changes only once.

Why designer 2 is the base. Criterion 4 asks for one mechanism. The scale is one TimeScale argument everywhere: the constructors, FromDayOfYear, Create, CreateFromJD, Parse(s, scale) and ConvertTo. Designer 1 adds Time.Utc/Tdb/TT/Tai/Gps on top. That is a second spelling of the same choice, and it overlaps with ToUTC()/ToTDB(). Designer 2 also brings Window equality, ISpanFormattable, the 'n JD' form and the Window.Intersects argument (Window.cs:77). That argument rules out the "strict equality, ties broken by scale" alternative.

Why the error-level Obsolete is grafted. I compiled the overload set in design/work/d3-judge/Program.cs:
- The new constructors (y,m,d,scale), (y,m,d,h,mi,s,scale), (+ms) and (+ms,us) compile beside the old optional-frame constructor. So the verifier's note that the scale cannot sit in the 7th position is wrong.
- new T(2024,1,1), new T(2024,1,1,12,0,0,0) and new T(2024,1,1,12,0,0,0,0) all bind the old constructor. With error: true each gives CS0619 with the message.
- With the old constructor removed (12.0), the literal-0 calls bind the new ones with scale Tdb, which was the old default. So the removal moves no value.
- An error costs the same migration as a warning: 255 literal calendar calls in the test projects (236 in IO.Astrodynamics.Tests, 10 SmokeTests, 4 CLI.Tests, 4 ConformanceRunner.Tests, 1 ColdStart), 52 in the docs, 0 in the library and the CLI. Add 119 Time(string) calls in tests and 3 in docs. In exchange, the silent 69.18 s TDB default closes in 11.0.0, not 12.0.
- The ITimeFrame family stays warning-level. Those are renames with identical results, not traps.

Why Local goes. A Time labelled Local depends on the machine's zone and on DST. Its TDB key would differ between machines. Library use: Time.cs:30/59/232/288 and one CLI site (TimeConverterCommand.cs:55). The CLI keeps 'local' as a display output only.

Checked against the source where the proposals disagree:
- Kind sites. The Kind check hits three internal sites, each passing a Utc-kind CCSDS calendar with the TIME_SYSTEM scale: Omm.cs:242, Opm.cs:227 and OpmStateVector.cs:116. Designer 1 cited Opm.cs:222 and missed OpmStateVector.cs; designer 2 missed Omm.cs:242. They pass the calendar unspecified, and the #485 tests show them bit for bit.
- Caches. Frame.cs:22/180 stores a StateOrientation that carries its epoch (Frame.cs:234). TemeFrame.cs:64 uses the same dictionary, and Star.cs:21 stores StateVectors. Under instant equality the first caller's scale wins, so results would depend on call order (designer 2's risk). Designer 1's fix, normalising to TDB on insert, moves the TEME ERA at the 6-in-5M instants where the TDB to UTC round trip misses. An internal exact comparer keeps every cache value bit for bit, with cross-scale misses as today. That is the rule that meets "no change of results". A cross-scale cache hit is a performance gain only; measure it later in its own issue.
- Equality key. Both designers and the verifier agree on the TDB tick. Measured over 2025: 33,084 TDB tick pairs share a TAI tick and 33,160 TT pairs share a TDB tick. The TDB key keeps the library's labels exact. Hash cost: TDB 3.4-7 ns (16-31 ns today); non-TDB one conversion, 110-140 ns.
- Same-scale fast path. Rates differ from 1 by under 2e-8, so ticks 2 or more apart stay ordered after conversion. Only a 1-tick difference needs the key.

Criteria in conflict. Designer 1's factories are shorter (criterion 1). I favour criterion 4 (one mechanism), because new Time(2024, 6, 21, 12, 0, 0, TimeScale.UTC) has the shape of DateTime(…, DateTimeKind) that every .NET user already knows. Also, criterion 3 (automatic) would convert a Local DateTime silently. I favour a loud ArgumentException whose message names ToUniversalTime(). The DST fold makes the conversion a choice the user should see.

Tests:
- Each new constructor and factory against the old one with an explicit frame, bit for bit.
- A property test that Equals, ==, GetHashCode and CompareTo agree across the five scales on random ticks from 1972 to 2100.
- Transitivity on G7's 8 TDB pairs and the verifier's 8 TT pairs.
- TDB-only Equals and CompareTo unchanged over 1e6 pairs.
- EqualsExact.
- Window.Intersects at both ends with UTC instants.
- CS0619 at the old constructors (compile test).
- The Kind throws.
- FromDayOfYear(0, …), doy 400 and 2023-366 throw.
- default(Time) is 0001-01-01 TDB.
- Parse accepts every form Time(string) and CcsdsEpochParser accept today, bit for bit, except the lenient DateTime forms, which are listed.
- TryParse returns false for each rejected form without throwing.
- Parse(t.ToString()).EqualsExact(t) for each scale.
- The four rounding methods against an exact BigInteger reference; their moved test values are listed as in #634.
- The CLI's --to/--format pairs and the unquoted 'TAI' rejected.
- The CCSDS reader tests unchanged.

Run the full suite with Validation, and long-validation.yml. No golden may move. A moved value is a cross-scale == of today (as in #512); the PR names it.

Own issues, outside this scope:
- Make TDB to TT the inverse of TT to TDB on the tick lattice (6 misses in 5M today).
- Cross-scale hits in the Frame/Star caches.
- MemoryDataProvider.cs:16-17 SortedDictionary keys of mixed scales, if the audit finds any.

**Alternatives not taken.**

- Designer 1 as written: per-scale factories Time.Utc/Tdb/TT/Tai/Gps with double seconds, plus FromDateTime. Shorter call sites, but a second spelling of the scale beside the TimeScale argument (criterion 4) that overlaps ToUTC()/ToTDB(). Not taken; the error-level Obsolete and the removal of Local are grafted from it.
- Designer 2 as written: warning-level Obsolete on the calendar constructor and Time(string), unchanged until 12.0, Local kept as a display member. Source compatible, but every newcomer who copies an old example keeps the silent 69.18 s TDB default, and the offset form of Time(string) keeps reading the machine's zone (three machines, three results), for one more major version. The migration costs the same. Not taken.
- Equality on the TAI tick (the study's recommendation). 33,084 pairs of TDB labels a year merge, so a == b while a - b is 1 tick on the library's own label scale, and u != u.ToTDB() for 9 of 5,000,000 UTC instants. Rejected, as the verifier recommended.
- Strict equality (ticks and scale) with CompareTo ordered by instant, then scale. Meets the FDG contract, but Window.Intersects (Window.cs:77) and every >=/<= test across scales silently flip at equal instants, and HashSet{tle.Epoch.ToTDB()}.Contains(tle.Epoch) stays false. Rejected.
- Keep both behaviours and add Time.InstantComparer. CompareTo stays intransitive (G7) and #512 stays the default. Rejected.
- Default the calendar constructor or untagged strings to UTC, as str2et does. A silent 69.18 s shift at every existing call. Rejected.

**Sub-choices.**

1. **Scale of new Time(y, m, d, ...).** Recommendation: New constructors with a required TimeScale last, in DateTime's shape. The optional-frame constructor (Time.cs:40-43) gets [Obsolete(error: true)] with the message 'new Time(y, m, d, ..., TimeScale.TDB) keeps TDB; TimeScale.UTC for UTC' (CS0619, compiled), and is removed in 12.0. After the removal, literal-0 calls bind scale Tdb, the old default, so nothing moves. Alternative: Warning-level Obsolete until 12.0 (designer 2 and the verifier), or per-scale factories Time.Utc(...)/Time.Tdb(...) (designer 1).
2. **Untagged strings and Time(string).** Recommendation: Time.Parse(s) throws FormatException on an untagged string and names the accepted tags. Parse(s, scale) gives the scale of an untagged string; a contradicting tag throws. Time(string) gets [Obsolete(error: true)] naming Parse(s, TimeScale.TDB). The lenient DateTime forms ('06/21/2024 12:00', RFC 1123) are dropped, with a release note. Alternative: Keep Time(string) warning-level, lenient and TDB until 12.0.
3. **DateTime.Kind and now.** Recommendation: new Time(DateTime, scale) throws ArgumentException on Kind Local, with a message naming ToUniversalTime() and TimeScale.UTC. It also throws on Kind Utc with a scale other than Utc. Add Time.UtcNow. No FromDateTime: one constructor, one rule. The three CCSDS sites (Omm.cs:242, Opm.cs:227, OpmStateVector.cs:116) pass their calendar with Kind Unspecified and stay bit for bit. Alternative: Convert a Local DateTime to UTC automatically when the scale is Utc (DateTimeOffset's behaviour). This hides the choice made at a DST fold.
4. **Equality key.** Recommendation: The TDB tick, for Equals, GetHashCode, CompareTo and the six operators. TDB-TDB is unchanged bit for bit. The same-scale fast path applies when the ticks differ by 0 or by 2 or more. EqualsExact compares ticks and scale, as DateTimeOffset does. Document that a == b gives |a - b| <= 1 tick, and the about 33,100 merged pairs a year of UTC/TT/TAI/GPS ticks. Alternative: The TAI tick, which merges about 33,100 pairs a year of the library's TDB labels instead.
5. **operator -.** Recommendation: Unchanged: the calendar difference inside TDB/TT/TAI/GPS, through TAI otherwise (#508, Time.cs:230-242). Alternative: Subtract through the TDB key. That turns the TT seconds of #501/#508 into TDB seconds across scales, up to about 28 µs a day.
6. **Library hash-keyed caches.** Recommendation: Frame.cs:22 (also used by TemeFrame.cs:64) and Star.cs:21 use an internal comparer on ticks and scale (EqualsExact). Each cached value is then what it is today, bit for bit, whatever the order of calls. TDB-keyed dictionaries (TLEPropagator.cs:16, ConjunctionAssessment.cs:1517) need nothing. Alternative: Normalise the key to TDB on insert (designer 1). This gives cross-scale hits, but moves the TEME ERA by up to one tick of rotation at the round-trip misses and relabels the returned epoch.
7. **Window equality.** Recommendation: Equality of StartDate and EndDate under Time equality, and the hash of the two ends, without Length. Length can differ by one tick between equal ends of different scales. Alternative: Keep the synthesized record equality, which includes Length.
8. **Scale type and Local.** Recommendation: enum TimeScale { TDB = 0, TT, TAI, UTC, GPS }. Member and method casing follow D2's table. The ITimeFrame family, Time.Frame, ToTDT and the ITimeFrame overloads become warning-level forwarders until 12.0. LocalFrame and ToLocal get [Obsolete(error: true)] with t.ToUtc().DateTime.ToLocalTime(). Custom ITimeFrame implementations throw ArgumentException. Alternative: Keep TimeScale.Local as a display member (designer 2).
9. **Parse grammar scope.** Recommendation: Strict: an ISO 8601 calendar or a CCSDS day of year, an optional time, the fraction to the nearest tick, then Z or ±hh:mm (the UTC instant, never machine-local) or a scale tag, or 'JD n' / 'n JD' with a tag, the digits read exactly. The core is CcsdsEpochParser's strict branch, moved into TimeSystem, so the CCSDS readers stay bit for bit. #619 keeps the fallback, and the CLI keeps bare seconds. Alternative: Accept everything DateTime.TryParse accepts, plus the new forms.
10. **Formatting and the TT name.** Recommendation: IFormattable and ISpanFormattable: null, G and O give ToString(); any other format is a DateTime custom format followed by the tag (Z for UTC, ' TDB', ' TT', ' TAI', ' GPS' otherwise). ToString writes TT where it wrote TDT, and Parse still reads TDT. Listed under Changed. Alternative: Keep writing TDT, with no change to string outputs.
11. **AddDays/AddHours/AddMinutes/CreateFromJD.** Recommendation: Nearest tick, ties to even, through ToNearestTicks (Time.cs:182). NaN and infinities throw ArgumentOutOfRangeException naming the parameter. No library call exists outside Time, so no library result moves. The moved test values are listed with an exact reference. #637's TimeSpan.FromSeconds sites stay in their own issue. Alternative: Keep the truncation and only throw on non-finite values.
12. **CLI time-converter.** Recommendation: --to tdb|tt|tai|utc|gps|local and --format iso|jd|seconds, defaults tdb and iso. -t, -u, -l, -j, -e and -d stay as aliases. local is a display output, iso only. An extra positional argument is an error. Input goes through Time.TryParse(s, TimeScale.TDB), which keeps the CLI's documented untagged TDB (Helpers.cs:133). Alternative: Keep the XOR flags (TimeConverterCommand.cs:32-40) and only catch the exception.

**Risks.**

- Merged keys. Within UTC/TT/TAI/GPS, about 33,100 pairs a year of adjacent ticks become equal and unordered while a - b is 1 tick. Code that kept UTC and TDB keys of one instant apart now gets one entry. The release note states both.
- Hash cost. Hashing or comparing a non-TDB Time costs one conversion, 110-140 ns, against 16-31 ns today. Measure it in the PR on mixed-scale sorts and on MemoryDataProvider's SortedDictionary (MemoryDataProvider.cs:16-17). There, two entries of one TDB tick in different scales now collide.
- Tests that use Assert.Equal on Times stop checking the scale. Each test whose subject is the label scale must move to EqualsExact. Audit this in the PR.
- Library control flow that compared mixed scales with == changes (false today, true now). TDB-TDB paths are unchanged by construction. The full suite, Validation and long-validation.yml must show no moved golden. Any moved value is listed and explained.
- Compile breaks (CS0619) at 255 calendar and 119 string constructor calls in the test projects, and at 55 doc lines. The CLI, the ConformanceRunner tests and user code must migrate in 11.0.0. The message carries the fix.
- Warnings: the ITimeFrame forwarders add several hundred warnings in the tests and in user code. They become errors under TreatWarningsAsErrors. No [Obsolete] exists in the library today.
- New runtime exceptions: Kind contradictions, untagged strings in Parse, and the dropped lenient forms. Internal paths the tests do not reach may pass a Utc-kind DateTime with another scale. The three known sites are fixed in the same PR.
- Visible text changes: TT replaces TDT in ToString, and the CLI flags change. Consumers that match on 'TDT' break.
- Removing Local as a scale breaks the 9 test sites and 1 CLI site that use it, and any user code that does arithmetic in local time.
- Rounding: the inputs of the 276 AddDays/AddHours/AddMinutes/CreateFromJD test calls can move by one tick.

### D4. Automatic defaults that change results: new defaults only on the options entry points, the integrator trap closed at compile time, the defects filed apart

**Rule.**

A default that moves results changes only on D1's options entry points. Under an existing signature, results stay bit for bit. Each move is stated and measured in the PR that makes it.
1. Integrator null = RK7(8) at RK78Integrator's defaults (1e-9/1e-9). OutputStep null = 60 s, and it is an output step only. s07: RK7(8) ends 1.36 m from Kepler after a LEO day; VV at the output step ends 974 km off (CentralBodyPropagator.cs:210-225). Not 1e-12: SSB mode about the Earth fails there (#389, #576).
2. Members with no integrator (Spacecraft.Propagate/PropagateAsync with 5 arguments, CentralBodyPropagator with 6, Scenario.SimulateAsync(drag, srp, step)) get [Obsolete(error: true)] with D1's DiagnosticId. They forward bit for bit, and the message names the fix: new VVIntegrator(step) keeps the results (1441/1441 states, 0 m), the options path gives RK7(8). PropagationTask's factory constructor and MonteCarloConfiguration's DeltaT, CelestialBodiesFactory and IntegratorFactory lose `required` and become IOA0001 forwarders; the options path has no factory (resolution C3). All are removed in 12.0.0.
3. Covariance null = Auto. P0 is propagated when the integrator is RK7(8) and P0 is at the window start and positive semi-definite. P0 at another epoch, or under VV, is not propagated, and GetCovariance's message says why. An indefinite P0 throws and names WithRemediatedCovariance() and Covariance = None. The GetCovariance hint ships on every path.
4. Force model. The central body always takes part with its observer's models (CentralBodyPropagatorBuilder.cs:138-143). A listed body with the same NAIF id and another model throws ArgumentException; compare instances and models, because Equals compares NAIF ids. If drag is asked and the central body has no model, the Earth takes NRLMSISE-00 nominal (CelestialBody.cs:546-552) and any other body throws InvalidOperationException. Albedo, thermal radiation and third bodies are off unless asked.
5. Search step. OccultationSearchOptions.Step null = min(orbital period of the spacecraft participants)/16, capped at window/2; with no spacecraft participant the step is required. FOV and threshold steps stay required. Occultation and FOV searches throw ArgumentOutOfRangeException for a step above half that period. Hyperbolic and natural-body participants are not guarded.
6. Conjunction: no inference from LastPropagation in 11.0.0 (it is RK7(8) only, PropagatorBase.cs:609). Both overload families stay, bit for bit.
7. Defects, each with its own issue and PR, before the defaults PR:
- the secondary HBR is ignored (ConjunctionAssessment.cs:2404);
- the constructor path omits the central body and ignores a twin (CentralBodyPropagator.cs:234-262);
- requested drag is dropped silently;
- Analyze accepts another spacecraft's trajectory;
- GetAtmosphere ignores a configured Earth model (CelestialBody.cs:546 against :496);
- threshold searches lack the extremes contract;
- dense inference together with the TCA label (UTC or TDB).
Proof: each default equals its explicit settings bit for bit. Expectations come from Kepler, the Hohmann closed form, GMAT or Pc2D (mpmath), at twice the worst of Linux, Windows and macOS arm64. long-validation.yml runs on the defaults PR. The conformance runner stays bit for bit. Doc figures are measured again.

**Recommendation and evidence.**

The base is the rigor-first proposal, with three ideas taken from the adoption-first one: covariance Auto, NRLMSISE-00 for an Earth without a model, and an inferred step for occultation searches.

**Where the proposals disagreed, checked against the source:**
- **LastPropagation.** It is kept only after RK7(8) (PropagatorBase.cs:609). Inferring the dense conjunction path from it would not reach Velocity-Verlet pairs. It would also move results under the existing Analyze and Screen signatures and change the TCA label from UTC to TDB. That breaks the rule both proposals state. So there is no inference in 11.0.0. It is filed together with the label item.
- **Conjunction covariance.** It already replays P0 for any RK7(8) solution whose P0 is at the first segment's base epoch (EncounterCovariance.cs:150-160). A P0 at another epoch is held in RTN, silently.
  - So Auto adds no new "sometimes" rule. It applies the conjunction's rule to the propagation.
  - The trajectory is unchanged (0.000 m), at 1.8 times the cost.
  - That meets criterion 3, so I favour it over the rigor proposal's opt-in.
  - It throws only on the new path, so no existing call breaks.
- **Atmosphere.** GetAtmosphere already returns NRLMSISE-00 for the Earth (CelestialBody.cs:546-552). Drag reads AtmosphericModel and gets 0 without one (:496). The tutorial (numerical-propagation.md:128) promises NRLMSISE-00.
  - One Earth default (D1 rule 6, "neither → documented default") is consistent and automatic.
  - Other bodies throw.
  - The CLI's EarthStandardAtmosphere (Helpers.cs:161) moves to that default, and the change is stated.
- **Search step.** The step contract of occultation searches is proven by the verifier: all 16 eclipses of a 24 h LEO search are found at steps of 5 to 45 min. Field of view under attitude slews is not proven, and threshold searches lose passes at period/16. So only the occultation search infers its step. The guard covers occultation and FOV.
- **Hard-body radius.** This follows D1 rule 6. SecondaryHardBodyRadiusMeters becomes double?, with null as the default:
  - only the secondary Spacecraft gives a radius > 0: that radius is used;
  - only the option is set: the option is used;
  - both are set and equal: that value;
  - both are set and differ: ArgumentException;
  - neither: 5 m.
  
  Spacecraft.HardBodyRadius stays double, and 0 means "not given". A point object is the option set to 0. The conformance runner sets 0 on the option and on every secondary (ConjunctionSolver.cs:69, :77, :177), so it does not move.

**Error-level [Obsolete] instead of D1 rule 9's warning.** This applies only to the members with no integrator, where I favour criteria 2 and 3 over the ease of a warning:
- propagatorStepSize means the Velocity-Verlet step in one overload and the output step in another (Spacecraft.cs:543 against :576);
- the default these members keep is 974 km off after one day.

D3 used the same level for its traps: Time(string) and LocalFrame. Compiled binaries keep their results. A rebuild gets the one-line fix, and that fix is bit for bit (measured: 1441/1441 states, 0 m).

**Order of the work:**
1. The seven defect PRs, each measured against an independent reference: long-validation.yml for the dynamics, tools/collision_probability for the Pc.
2. The defaults PR, which states every moved figure:
   - The Pc of a pair with P0 moves when only the integrator changes: 9.98e-81 under VV, 6.71e-78 under RK7(8). The covariance source moves from InitialStateHeldInRtn to Propagated, the TCA by 13.5 ms and the miss by 0.17 m.
   - Avoidance options follow LastForceModel.
   - Burn epochs move: the Hohmann thrust starts at 05:14:58 instead of 05:18:28.
   - CLI results move.

**Alternatives not taken.**

- Adoption-first as proposed: keep the members with no integrator as warning-level [Obsolete], 974 km in the message, and infer the dense conjunction path on the ILocalizable overloads when both participants hold an RK7(8) solution. Rejected: a warning leaves the 974 km trap callable from new source, and the inference moves Analyze and Screen results and TCA labels under signatures that exist today.
- Rigor-first as proposed: covariance stays opt-in, drag with no atmospheric model always throws, and no search-step default. Rejected on criterion 3. The conjunction already applies the Auto rule silently (EncounterCovariance.cs:155-158). The library already names NRLMSISE-00 as the Earth's atmosphere (CelestialBody.cs:546-552). The occultation step contract is proven.
- Remap the existing overloads to RK7(8) in place (the study). Rejected: compiled callers would change results with no signal, and propagatorStepSize would change meaning (Scenario.cs:92-93 documents it as the VV step). No bit-for-bit baseline would be left.
- Keep Velocity-Verlet with its step decoupled from the output step, e.g. min(output, 10 s) (verifier's narrower option). Rejected: results still move, the method stays first order with drag (#439: 267 m at 1 s on a dense pass), and covariance still needs a named integrator.
- Remove the PropagationSolution conjunction overloads and infer the path from LastPropagation (the study). Rejected: it reaches only RK7(8) runs, the conformance runner uses those overloads (ConjunctionSolver.cs:89-90), and keeping every integrator's solution would cost 0.55 MB per LEO day per spacecraft.
- Apply min(period)/16 as the default step on every search (the study). Rejected: LEO passes above 30 deg elevation are lost at 4 and 5 min steps, below period/16 (5.81 min), on the threshold path (GeometryFinder.cs:17-21).

**Sub-choices.**

1. **Obsolete level of the members with no integrator (Spacecraft.Propagate/PropagateAsync with 5 arguments, CentralBodyPropagator with 6, Scenario.SimulateAsync(drag, srp, step)).** Recommendation: [Obsolete(error: true)] with D1's DiagnosticId. The members forward bit for bit, and the message reads: 'Velocity-Verlet at the output step: 974 km after one LEO day at 60 s. Pass new VVIntegrator(step) to keep these results, or use the options overload for RK7(8).' Binaries keep their results. This follows D3's treatment of traps. PropagationTask drops '= null' and MonteCarloConfiguration.IntegratorFactory becomes required. About 75 call sites in the repository migrate mechanically, and each path, SimulateAsync and PropagationTask included, gets its own bit-for-bit test. Alternative: Warning-level [Obsolete] per D1 rule 9, with the figure in the message. Easier to adopt, but the trap stays callable.
2. **Tolerances of the RK7(8) default.** Recommendation: 1e-9/1e-9, RK78Integrator's current defaults, with no new constant. A LEO day ends 1.36 m from Kepler. SSB mode runs (118 to 372 ms per hour in the two measurements, against 3 to 4 ms for VV). Alternative: 1e-12. Rejected: SSB mode about the Earth fails there (#389, #576), and a point-mass day gains about 1 m.
3. **Default output step.** Recommendation: 60 s, the value of every doc example. RK7(8) outputs are interpolated from the dense solution, so the output step does not change accuracy. Alternative: Window length / 1000, clamped to [1 s, 600 s]. Fewer states on long cruises, but harder to predict.
4. **Third bodies when none are given.** Recommendation: None: the central body only, with its observer's models. One sentence for every central body, testable against Kepler, and no ephemeris coverage of other bodies needed. Alternative: The Sun and the Moon minus the central body, as the doc examples write it. Physically arbitrary about Mars or the Sun.
5. **Covariance on the new path.** Recommendation: Auto. P0 is propagated when the integrator is RK7(8), P0 is at the window start, and P0 is positive semi-definite; this is the conjunction's own replay rule. The trajectory is unchanged (0.000 m), at 1.8 times the cost. Before shipping, test that the covariance at TCA read from the solution equals the replay's, bit for bit or within a measured bound. Alternative: Opt-in, with the hint only in GetCovariance's message (rigor). No moved result, but not automatic.
6. **Indefinite P0 under Auto.** Recommendation: Throw InvalidOperationException naming StateVector.WithRemediatedCovariance() and Covariance = None. This is consistent with the explicit mode (CentralBodyPropagatorBuilder.cs:96-103), and it is on the new path only. Alternative: Remediate silently, as the conjunction's ResolveInitialCovariance does (EncounterCovariance.cs:190-202). This contradicts the rule that the propagation rejects an indefinite P0.
7. **Drag asked and the central body has no atmospheric model.** Recommendation: The Earth takes NRLMSISE-00 with nominal space weather, the model GetAtmosphere already returns and the tutorial names. Any other body throws InvalidOperationException naming it. The same rule reaches the explicit-integrator paths through the defect PR on dropped drag, with the results it moves stated and measured. Alternative: Throw for every body without a model (rigor). Nothing is inferred, but the call is less automatic.
8. **CLI astro propagate.** Recommendation: Follow the library defaults: RK7(8) at 1e-9 instead of VV at 1 s (PropagateCommand.cs:62), and the Earth without its explicit EarthStandardAtmosphere (Helpers.cs:161). An --integrator option keeps VV. State both under Breaking. CLI.Tests expectations are re-derived from Kepler or GMAT, not snapshotted. Alternative: Keep VV at 1 s and EarthStandardAtmosphere in the CLI (about 271 m per LEO day, s07). Two defaults remain.
9. **Scope of the inferred search step.** Recommendation: Occultation only: min(spacecraft orbital period)/16, capped at window/2, measured against OccultationReference and the 16 eclipses of a 24 h search. The half-period guard covers occultation and FOV. FOV and threshold steps stay required until FieldOfViewReference under slews and the extremes contract (its own issue) prove a default. Alternative: Occultation and FOV (adoption). The FOV margin follows the attitude, so slews can beat period/16; unproven.
10. **Conjunction path of the ILocalizable overloads.** Recommendation: Sampled, bit for bit, in 11.0.0. Inferring the dense path for RK7(8) pairs is filed with the TCA label item (UTC against TDB) and #621. Analyze rejects another spacecraft's solution through an internal owner set by PropagatorBase; owner-less solutions, as in the runner, are still accepted (defect PR). Alternative: Infer the dense path when both participants hold an RK7(8) LastPropagation (adoption). Cheaper (32 ms against 117 ms) and more accurate, but it moves Analyze and Screen results and labels under existing signatures.
11. **Secondary hard-body radius (defect PR).** Recommendation: D1 rule 6. SecondaryHardBodyRadiusMeters becomes double? with null as the default. A secondary Spacecraft radius > 0 alone is used; the option alone is used; both equal give that value; both different throw ArgumentException; neither gives 5 m. The option is validated finite and >= 0 on all three options types. Spacecraft.HardBodyRadius stays double, and 0 means not given. The 67 calls of CreateSpacecraft (13 m to 16 m) and the CDM exports are re-derived from tools/collision_probability. The runner does not move. Alternative: Keep double, and use the secondary's radius when > 0, else the option (rigor). No type change, but the option can no longer override a secondary that has a radius.

**Risks.**

- The RK7(8) default moves results the study did not list, all through LastPropagation, LastForceModel and the replay that is on by default:
- the conjunction covariance source and Pc (9.98e-81 to 6.71e-78 on one pair);
- the avoidance trajectories;
- the burn epochs, ExecutedManeuvers, the attitude timeline and the SPK arcs.
The defaults PR measures and states each one, and the attitude and arc tests run on both integrators.
- Memory: every default propagation keeps LastPropagation, about 0.55 MB per LEO day per spacecraft (Spacecraft.cs:824-832). Scenario, batch and Monte Carlo runs of many spacecraft must be measured before the default lands.
- Cost: SSB mode about the Earth runs 118 to 372 ms per hour at 1e-9, against 2 to 3 ms relative to the Earth and 3 to 4 ms for VV. Drag runs cost 489 ms per LEO day, against 66 ms without drag. SSB mode needs a test and a docs line citing #389 and #576.
- RK7(8) throws after about 3 min of rejections on a trajectory whose perigee is below the surface, while VV returns states silently. The message ('relax tolerances') misleads. This is a candidate issue of its own, not part of this decision.
- Error-level obsolescence breaks about 75 call sites in the repository at compile time. The migration is bit for bit on the Spacecraft.Propagate path, which is measured. The SimulateAsync and PropagationTask paths with a factory need their own bit-for-bit test.
- Throwing on a misplaced central-body twin, and on drag about a body with no model, turns silently wrong runs into exceptions. Doc examples that pass EARTH_BODY with drag must be fixed in the same PR (#662 compile check).
- The HBR conflict rule makes tests that set the option while the secondary has a radius throw. They migrate in the defect PR, and their Pc is re-derived, never snapshotted.
- long-validation.yml runs on Linux only. The thresholds of the new-default tests take twice the worst value over the three platforms' CI logs.

## B. Recommendations

### R01. Kernel loading and the first kernel set
**Gain** high · **Cost** M · **Breaking** yes · **Results** unchanged on Data/SolarSystem; conflicting user kernels may change winner · **C++** yes · **Issues** #545, #662

**Today.** LoadKernels skips a missing path (SpiceAPI.cs:285), matches by substring (:267, :269, :279), reloads a loaded folder (211-250 ms a repeat) and loads in listing order (:322). No onboarding page names gm_de440.tpc; without it the Earth fails with "frame can't be defined" (study/A-onboarding-kernels-config/s03, s04, s08).
```csharp
var spice = SpiceAPI.Instance;
spice.LoadKernels(new DirectoryInfo("k/SolarSytem"));   // typo: no error
spice.LoadKernels(new DirectoryInfo("k/SolarSystem"));
spice.LoadKernels(new DirectoryInfo("k/SolarSystem"));  // reloads every file
spice.LoadKernels(new DirectoryInfo("k/SolarSystem2")); // reloads SolarSystem; SolarSystem2 never loads
```
**Proposed.** A registry holds each path (compared by component) and its files. LoadKernels furnishes only new files; ReloadKernels re-reads one, on top; UnloadKernels works from the registry. A folder loads in ordinal order, component by component: the later file wins on every OS. A missing or non-kernel path throws. Library pairs (CosmographiaExporter.cs:316-328) unload what they added. C++: no unload walk; a ktotal_c/kdata_c proxy lists SPICE's files. get-started.md lists de440s.bsp, latest_leapseconds.tls, pck00011.tpc, gm_de440.tpc, and when the Earth is IAU_EARTH.
```csharp
SpiceApi.Instance.LoadKernels("kernels/SolarSystem");   // DirectoryNotFoundException if missing
SpiceApi.Instance.LoadKernels("kernels/SolarSystem2");  // loaded: components, not substrings
SpiceApi.Instance.ReloadKernels("kernels/mission.bc");  // a kernel rewritten on disk
```
**Breaks and migration.** Changed (Breaking): a repeated load reads nothing and keeps its precedence (ReloadKernels); a missing or non-kernel path throws (test Exists); folder order (rename files); LoadKernels(null) is CS0121.
**Tests.** Two loads, one furnish (ktotal_c); X then X2 and the reverse both read; loads f, z, f: z wins; bad paths throw; a meta-kernel and a late \begindata load; s09 and x0.tpc beside x/ give one order on 3 OS; the exporter keeps a user's kernel. Suite and conformance report unchanged.

**Results, in detail.** Unchanged on Data/SolarSystem. Its SPKs do not overlap (segments read from the DAF summaries), and the only variables defined twice are TKFRAME_EARTH_FIXED_* in earth_fixed.tf and earth_topo_201023.tf, which are identical. So no load order moves a result there (scan in work/r01r02-rev). Where a user's kernels define the same variable or overlap in coverage, the winner can change in two ways. (1) Folder order: s09 keeps f_gm (923) on ext4 today and z_gm (204) after, on every OS. (2) A repeated load no longer moves its files to the top: KernelsLoader.cpp calls unload_c then furnsh_c, so loading f_gm, z_gm, f_gm gives 923 today and 204 after (measured in work/r01r02-rev/c). Both are measured by the s09 folder and the f, z, f case on Linux, Windows and macOS arm64.

**Depends on.** R03, D2. **C++ change.** yes.

**Pull requests.** PR R01-a: the kernel registry. It adds the string overloads, ReloadKernels, UnloadKernels from the registry, folder order by ordinal path components, the exceptions, and an internal scope for library load/unload pairs. C++: the folder walk of UnloadKernelsProxy is removed, and a ktotal_c/kdata_c proxy sits behind GetLoadedKernelFiles. The P/Invoke declarations and the native library stay in step and are built by build-native on the three platforms. PR R01-b: the get-started kernel table and the CelestialBody.Frame note, compiled by R05's docs job. Both land after the two defect PRs below and after R03's LoadKernelsProxy message.

**Open choices.**

1. **A repeated LoadKernels of a loaded path.** Recommendation: Load only the files of the path that are missing, with no native call when none is missing. Add an explicit ReloadKernels for a rewritten file or to raise a file's precedence. The common startup call is then free and safe, and the rare rewrite is explicit. Alternative: Throw InvalidOperationException on a second load. Or keep today's unload and reload, which also moves the path's files to the top of SPICE's precedence, at 4 to 7 times the cost (34-49 ms the first time, 211-250 ms each repeat on Data/SolarSystem).
2. **Order of the files of a folder.** Recommendation: Sort the paths relative to the folder ordinally, component by component, and document 'the later file wins'. A whole-path ordinal sort is not the same on every OS: '/' (0x2F) sorts before digits and capitals and '\' (0x5C) after them, so x0.tpc and x/y.tpc swap between Linux and Windows. Alternative: Keep the directory listing order and document that precedence depends on the platform.
3. **UnloadKernels of a path that was never loaded.** Recommendation: A documented no-op, as today. The postcondition 'not loaded' holds. A deleted folder that was loaded is unloaded from its registry entry (the defect PR below). Alternative: Throw ArgumentException that names the loaded entries.
4. **Library-internal load/unload pairs (CosmographiaExporter.cs:316-328).** Recommendation: An internal scope that records what it added and unloads only that. A kernel the user loaded at the same path stays loaded, and the public Load/Unload keep simple semantics. Alternative: Reference counting on the public LoadKernels/UnloadKernels. This breaks the idempotence above: two loads would need two unloads.
5. **How a newcomer gets the kernels.** Recommendation: In 11.0.0, a table in get-started.md of the four files (de440s.bsp 32.7 MB, latest_leapseconds.tls, pck00011.tpc, gm_de440.tpc) with their NAIF generic_kernels paths and a curl/PowerShell one-liner. The tutorials list what they need on top (EGM2008, earth_latest_high_prec.bpc with earth_assoc_itrf93.tf). The messages that name the missing variable come from R03. Alternative: An 'astro kernels fetch' command or a companion NuGet package of generic kernels, each filed as its own feature issue. Either one needs network access, NAIF URLs kept up to date and a refresh of the Earth BPC.

**Candidate defects found on the way.**

- **LoadKernels and UnloadKernels match paths by substring: a folder whose name extends a loaded one is never loaded, and loading or unloading the shorter one unloads it.** SpiceAPI.cs:267/269 `path.FullName.Contains(x.FullName)`, :279 `x.FullName.Contains(path.FullName)`, and :429 in UnloadKernels. study/A-onboarding-kernels-config/s04: after k/SolarSystem, LoadKernels(k/SolarSystem2) reloads k/SolarSystem and never loads SolarSystem2, so a read of L1 (391) throws SPKINSUFFDATA. In the reverse order SolarSystem2 is unloaded. Reproduced in design/work/r01r02-rev/a (listed: SolarSystem only, in both orders). The fix is a component-wise comparison, in its own PR before R01-a. No result moves on Data/SolarSystem. No issue yet.
- **UnloadKernels is a silent no-op on a deleted or missing path while SPICE keeps the data; unloading a file of a loaded folder leaves the folder listed.** SpiceAPI.cs:421 tests `path.Exists`. verify/A/deleted: a temp folder is loaded and then deleted. UnloadKernels(new DirectoryInfo(folder)) leaves the folder listed and its data readable (Moon at 4.05e8 m). ClearKernels does unload it, because the registry keeps the original FileSystemInfo with Exists cached. SpiceAPI.cs:428 removes only an exact match, and SpiceApiTest.cs:784-791 asserts the stale listing. The fix unloads from the registry entry, in its own PR. No issue yet.
- **Writers leave the kernel they write unloaded, and a rewritten CK is read with its old content.** Known limitations (Kernels the library writes): Spacecraft.WriteOrientation, SpiceAPI.WriteOrientation and WriteEphemeris. R01's registry gives the mechanism: the writer loads the written file through the registry, and reloads it if it was already loaded. Tracked by #545.
- **The CLI wraps every kernels argument in DirectoryInfo: a meta-kernel or a single kernel file is ignored without an error.** EphemerisCommand.cs:43 and PropagateCommand.cs:46 call LoadKernels(new DirectoryInfo(path)). verify/H/mk.tm (de440s, pck00011, LSK, gm_de440) gives 'Celestial celestialItem frame can't be defined'. This is not part of R01. R29 owns it (H-kernels-option: -K accepts a file or a directory). After R01, a missing path throws. No issue yet.

### R02. Predefined bodies without type initializers
**Gain** high · **Cost** M · **Breaking** yes · **Results** unchanged when kernels load first; no body frozen at GM 0 · **C++** no · **Issues** #623, #651, #661

**Today.** Static constructors build bodies (PlanetsAndMoons.cs:174-178, Stars.cs:12-15, Barycenters.cs:22-34). Before the kernels, the id MOON throws for the process; SUN_BODY keeps GM 0 or throws, by call order. NaifObject is no INaifObject; its Frame is unread (study/A-onboarding-kernels-config/s02, s02b, s10).
```csharp
try { _ = PlanetsAndMoons.MOON; } catch (TypeInitializationException) { }
var sun = Stars.SUN_BODY;            // GM 0 after the failed MOON, kept
SpiceAPI.Instance.LoadKernels(new DirectoryInfo(kernels));
var earth = PlanetsAndMoons.EARTH_BODY;  // throws again
```
**Proposed.** Catalogues hold data; ids keep the plain name, bodies take Body. EarthBody, MoonBody, SunBody and barycentres are properties over one cache: built on first success, no lock but SPICE's, published by CompareExchange, failures not kept, tagged with the provider and a generation that SetDataProvider and a load or unload that changes the pool bump. Same constructor. NaifObject(NaifId, Name) is an INaifObject; Frame goes. A NaifObject front body routes as CelestialItem.Create(id) (CelestialItem.cs:981, OccultationSearch.cs:39). Site(NaifObject, CelestialBody) takes 13 from 399013; another body's id throws.
```csharp
var moonId = PlanetsAndMoons.Moon;                  // data: no SPICE read
SpiceApi.Instance.LoadKernels("kernels");
var earth = PlanetsAndMoons.EarthBody;              // rebuilt after a kernel change
var dss13 = new Site(GroundStations.Dss13, earth);  // id 13
```
**Breaks and migration.** Changed (Breaking): D2's catalogue table; fields become properties (binary); a kernel change gives a new instance (empty Satellites; Equals by NaifId). Removed: NaifObject.Frame (18 test lines). Stars.Sun stays. The #623 rule (CLAUDE.md, SpiceAPI.cs:163-168) is rewritten.
**Tests.** ColdStart: ids and types before kernels; EarthBody throws, then equals new CelestialBody(399); s02b's order gives the kernel GM; memory provider; #623's tests; reads racing a load. The Moon id as front body in a spacecraft search gives MoonBody's windows. Body users bit for bit.

**Results, in detail.** Unchanged for a program that loads the kernels before it touches a catalogue body. The same constructor (CelestialBody.cs:111-139: ECLIPJ2000, J2000 TDB) builds the same body, so every test that uses EARTH_BODY, MOON_BODY, SUN_BODY or a barycentre stays bit for bit. No golden moves, because the suite and the conformance runner load their kernels first. Values move only where they are wrong today. A body touched before the kernels held GM 0 (Stars.SUN_BODY after a failed MOON, Barycenters.EARTH_BARYCENTER; reproduced in work/r01r02-rev/b and /c). Now it throws InvalidOperationException, and after the load it holds the kernel value (new CelestialBody(10).GM = 1.3271244004127939e20). A program that changes the data provider or loads earth_assoc_itrf93.tf after its first touch now reads the new provider or frame (ITRF93 instead of IAU_EARTH). This is measured by ColdStart cases.

**Depends on.** D2, R01, R03. **C++ change.** no.

**Pull requests.** PR R02: data-only catalogues together with D2's catalogue rename block (the 411-row table). It also brings the *Body and barycentre properties over the generation cache, NaifObject : INaifObject without Frame, the routing of a NaifObject front body through CelestialItem.Create, and the Site(NaifObject, CelestialBody) overload. The same PR rewrites the #623 rule in CLAUDE.md, the lock comment of SpiceAPI.cs:163-168 and the #623 entry of the release notes. It removes the deadlock half of #661; the double enumeration stays in #661.

**Open choices.**

1. **Shape of a catalogue entry (EARTH vs EARTH_BODY), left open by D2.** Recommendation: The id keeps the plain name (PlanetsAndMoons.Earth, a NaifObject that needs no kernel), and the cached body takes the Body suffix (EarthBody, MoonBody, Stars.SunBody). Ids are used most: 347 uses of PlanetsAndMoons ids against 109 of its bodies in IO.Astrodynamics.Tests. Stars.Sun already has the id's name. Alternative: PlanetsAndMoons.Earth becomes the body and the ids move to a separate NaifIds class. Every catalogue member would then need a lazy SPICE read (161 planets and moons), and declaring an id would read kernels.
2. **How the predefined bodies are held.** Recommendation: One internal cache. An entry is built on its first successful access, outside any lock but SPICE's, and published with Interlocked.CompareExchange. No exception is cached. Each entry is tagged with the provider instance and the kernel generation, read before the build. SetDataProvider, R04's scoped provider, and a load, reload, unload or clear that changes the pool make a tag stale. A load that adds no file (R01) bumps nothing, so the TestKernels.EnsureSolarSystem call in each test class causes no rebuild. A stale or failed entry rebuilds on its next access. Alternative: Lazy<T> with LazyThreadSafetyMode.PublicationOnly, built once. It recovers after a failure but goes stale after a kernel change: an Earth built before earth_assoc_itrf93.tf stays IAU_EARTH. The default ExecutionAndPublication mode caches the exception and takes a lock, which brings back the #623 deadlock.
3. **A cached accessor for every constant (CelestialBody.Get(INaifObject)).** Recommendation: Not in 11.0.0. new CelestialBody(PlanetsAndMoons.Jupiter) already exists (CelestialBody.cs:111). The doc's JUPITER_BODY (numerical-propagation.md:101) is fixed under R05/#662. One way to build a body keeps one mechanism. Alternative: Add CelestialBody.Get(INaifObject) over the same cache, for the 161 planets and moons. Not an implicit conversion: it would read SPICE and could throw, which FDG forbids for conversions.
4. **NaifObject.Frame.** Recommendation: Remove it, listed under Removed. The library reads it nowhere; it reads only NaifId and Name (CelestialBody.cs:111-139, LagrangePoint.cs:27). It is wrong for the Moon (IAU_MOON against MOON_ME with the usual FKs) and for NORCIA ('NEW NORCIA_TOPO'). Comets pass string.Empty and LagrangePoints null. 18 test lines (16 in IO.Astrodynamics.Tests, 2 in SmokeTests) read EARTH.Frame or MOON.Frame; they take the literal name instead ("ITRF93", "IAU_MOON"). Alternative: Keep it, documented as a nominal frame that the library does not use.

**Candidate defects found on the way.**

- **A predefined body keeps every spacecraft built about it: Satellites is an unsynchronised HashSet on a process-wide instance.** Spacecraft.cs:478 and CelestialItem.cs:162/:211 add each spacecraft to its observer's _satellites (CelestialItem.cs:62, a HashSet with no lock). Re-run in design/work/r01r02-rev/b: 2000 spacecraft built about PlanetsAndMoons.EARTH_BODY give Satellites.Count 2000, and the 20 sampled are all still alive after GC.Collect. A long-running service keeps every spacecraft it builds, and spacecraft built concurrently about the same body change the HashSet without a lock. Not tracked; it needs its own issue and PR. No issue yet.
- **A TLE cannot be built without SPICE kernels: the constructor builds the Earth before any argument check.** TLE.cs:176-177: base(new CelestialBody(399, Frame.ECLIPTIC_J2000, ExtractEpochFromTLE(line1)), ...). verify/F/v1-nokernels: without LoadKernels, new TLE("ISS", l1, l2) throws 'Celestial celestialItem frame can't be defined', and CSPICE prints SPICE(NOLOADEDFILES) and BODY399_RADII banners. NoradCatalogId, ToOmm and parsing need no kernels, and SGP4 needs only the LSK. Not tracked; it needs its own issue. The TLE type's API belongs to R21. This item is not part of R02. No issue yet.
- **GetCelestialBodyInfo of a site identifier gives the Sun as centre and barycentre 0.** Any NAIF id above 1000 is classed as an asteroid. DSS-13 (399013) gets centre 10 and barycentre 0 (study/A-onboarding-kernels-config/s12). The new Site(GroundStations.Dss13, earth) overload derives the user id 13 from 399013 - 1000 * 399 and never reads that information (the Site constructor reads no body info, Surface/Site.cs:69-95). The fix of #651 in .NET still stands. Tracked by #651.

### R03. SPICE errors: one exception type, nothing on stdout, no dropped error
**Gain** high · **Cost** L · **Breaking** yes · **Results** unchanged on success; a SPICE error throws, not an empty list or zeros; searches past 1000 windows complete (#653) · **C++** yes · **Issues** #454, #653, #655

**Today.** Of 22 proxies .NET calls, 3 keep SPICE's message (Proxy.cpp:236, :323, :753), 18 drop it, 1 never checks (:854); C++ exceptions end the process (#655). SPICE prints on stdout (:586-590); the exception names another cause (A/s08, s11).
```csharp
SpiceAPI.Instance.LoadKernels(new DirectoryInfo("k1")); // de440s, pck00011, LSK
try { var earth = new CelestialBody(399); }
catch (InvalidOperationException e) { Console.Error.WriteLine(e.Message); }
// "Celestial celestialItem frame can't be defined"; BODY399_GM only on stdout
```
**Proposed.** Three PRs.
1. Each called proxy returns `bool` with SPICE's short and long message, or the C++ exception, in `lastError`; DTOs zeroed; search cells sized by the caller, regrown when full (#653). `SpiceException : InvalidOperationException` (`ShortMessage`, `LongMessage`) names input and cause, not "standard output" (SpiceAPI.cs:291).
2. `InsufficientFuelException : InvalidOperationException` with masses; NewtonRaphson.cs:37 (IOD) throws `InvalidOperationException`; `CcsdsFormatException : FormatException` bases the 5 CCSDS types; no `ExceptionBase`; CA1032, CA2201, CA2208 as errors.
3. Last: `errprt_c("SET", "NONE")` (sooner, it hides 19 proxies' only trace). CLI: no banner (Program.cs:25), progress on stderr.
```csharp
try { var earth = new CelestialBody(399); }
catch (SpiceException e) { } // ShortMessage "SPICE(KERNELVARNOTFOUND)"
// "EARTH (399): no BODY399_GM in the kernel pool; load a GM kernel (gm_de440.tpc)"
```
**Breaks and migration.** Catches still catch. A search with a SPICE error throws. No banner, no `System.Exception` (table under Changed). TLE checksum: R21; missing kernel path: R01.
**Tests.** Per proxy: C++ (message kept, nothing printed), .NET (type, `ShortMessage`). Each search via `CelestialItem`, `Site`, `Instrument`, `SpiceAPI`, CSPICE and managed: one type. A child's stdout is its own. Past 1000 and 10000 windows: spiceypy. Goldens unchanged; any test asserting an empty list on error is listed.

**Results, in detail.** Success paths unchanged bit for bit: the same SPICE calls, and errprt changes output only. A SPICE error now throws SpiceException where the CSPICE searches returned an empty list and 18 proxies returned zeros or stack contents; measured per search and per proxy against today's call. CSPICE searches past 1000 windows (and past the 10000-window native cell) become complete, measured against one spiceypy search with a larger cell (#653). The GM of the 8 named bodies of Data/SolarSystem with a GM and no RADII (Nix, Hydra, Kerberos, Styx, Pallas, Didymos, its barycentre, Dimorphos) moves from 0 (an uninitialised value) to the kernel value; that move lands in its own defect PR, before PR 1, and is stated there.

**Depends on.** nothing. **C++ change.** yes.

**Pull requests.** Before PR 1: the two GetCelestialBodyInfo defects below, each in its own PR. Otherwise PR 1's bool contract makes the 8 bodies with a GM and no RADII throw. PR 1: the native contract (every proxy .NET calls returns bool with SPICE's short and long message, or the C++ exception, in lastError; DTO arrays zeroed; search cells sized by the caller and regrown) and SpiceException, built by build-native on the three platforms. PR 2: the library's own exception types (InsufficientFuelException, NewtonRaphson/IOD, CcsdsFormatException, ExceptionBase removed, CA1032/CA2201/CA2208 as errors). PR 3, last in 11.0.0: errprt NONE, and in the CLI the banner subcommand removed and progress sent to stderr. Their own issues and PRs, not here: #496 (front shape), #527/#626 (reads past the propagated states), #555 (null targetOrbit), #558 (managed frames on the coordinate search).

**Open choices.**

1. **Exception type of a failed SPICE call.** Recommendation: SpiceException : InvalidOperationException, with ShortMessage (e.g. SPICE(KERNELVARNOTFOUND)) and LongMessage. Callers can catch it by type. Every existing catch of InvalidOperationException keeps working: the #454/#560 tests, and the library's own catches (CelestialItem.cs:175, PropagationFrameOrientationCache.cs:77, :124). Wrappers such as PropagationFrameOrientationCache.cs:218-222 keep it as InnerException. HandleError reads only the LONG message today (Proxy.cpp:592-598), so the native side must also return SHORT. Alternative: Keep InvalidOperationException and put SPICE's short and long messages in its text.
2. **When and how SPICE's own output is silenced.** Recommendation: errprt_c("SET", "NONE") in ActivateErrorManagement, as the last PR of the same release, once every proxy .NET calls carries its message into lastError. Before that point it would remove the only trace of the 19 proxies that drop or never check their error (verifier). No sink until someone asks for one. Alternative: An opt-in TextWriter sink for SPICE's report, or keep stdout until 12.0.0. CSPICE's errdev accepts only SCREEN (stdout), NULL or a file, so stderr is not available.
3. **A full native search cell (#653).** Recommendation: The caller sizes both the CSPICE cell and the copy, and the proxy returns the count and a 'full' flag. Today the cell is a stack array of 10000 windows (GeometryFinder.cpp:23-30; 1000 for gfilum, :145-152) and the copy stops at 1000 (Proxy.cpp:353). SpiceApi grows the buffer and runs the search again. The result is one CSPICE search with no seam, so AbsoluteMin/Max need no special case (verifier). Alternative: #653's fix: resume from the end of the 1000th window, join the windows, and throw for AbsoluteMin/Max. This has edge cases at the restart bound: a window ending there, and a LocalMin/Max there that CSPICE does not count.
4. **Shape of the CCSDS exceptions.** Recommendation: One CcsdsFormatException : FormatException as the base of the 5 existing types (OmmParse, OmmValidation, OpmParse, OpmValidation, CdmParse; CdmParseException is sealed), with the standard constructors. Names and existing catches stay. One catch also covers LoadFromString("garbage"), which raises a *ValidationException rather than a parse exception (verifier). Alternative: Merge them into CcsdsParseException and CcsdsValidationException, shared by OMM, OPM and CDM. Every existing catch breaks.
5. **The 6 exported proxies that .NET never calls (LaunchProxy, TDBToStringProxy, UTCToStringProxy, ConvertTDBToUTCProxy, ConvertUTCToTDBProxy, ConvertStateVectorToEquatorialCoordinatesProxy).** Recommendation: Remove them from the export in PR 1. No P/Invoke uses them, so nothing has to be kept in step, and two of them return the SPICE message as their date string (Proxy.cpp:281, :293). The C++ classes they wrap stay, and so do the C++ tests of those classes. Alternative: Bring them to the bool + lastError contract like the others.

**Candidate defects found on the way.**

- **A body with a GM but no RADII loses its GM: Pallas, Nix, Hydra, Kerberos, Styx, Didymos and Dimorphos read GM 0 and a garbage frame name.** GetCelestialBodyInfoProxy (IO.Astrodynamics/API/Proxy.cpp:540-558) reads RADII with bodvcd_c and no bodfnd_c. When that read fails in RETURN mode, ReadGM and cnmfrm_c return without effect, and HandleError resets the error only at the end (:579-582). ReadGM then returns its uninitialised res[0] (Body/CelestialBody.cpp:115-118), so the value is undefined, not 0 by design. ReadJValue already guards with bodfnd_c (CelestialBody.cpp:283). Measured with Data/SolarSystem (design/work/r03-r25-rev/gm): 59 ids of gm_de440.tpc have no RADII in pck00011. 10 are barycentres, which skip the RADII read. Of the other 49, the 8 that SPICE names (902-905, 2000002, 20065803, 120065803, 920065803) give GM 0 and a garbage FrameName. new CelestialBody(2000002) throws 'frame can't be defined' or builds with a garbage frame. Fix: read RADII only when bodfnd_c finds them. This changes results (GM 0 to the kernel value), so the PR states it. It must land before R03's PR 1. No open issue. No issue yet.
- **An unknown NAIF id or a barycentre returns uninitialised native memory: a stale or garbage name, a garbage frame and GM 0.** Proxy.cpp:525 declares `CelestialBodyDTO res;` and fills it only when bodc2n_c finds the id. Name and FrameName have no initialiser (IO.Astrodynamics/API/DTO/CelestialBodyDTO.h:15,18). The guard at SpiceAPI.cs:1071 accepts any id above 1000. Measured in design/work/r03-r25-rev/gm: the 41 unnamed ids of gm_de440.tpc return Id -1 with the previous call's name (2000003 gives 'PALLAS', 20090482 gives 'DIDYMOS BARYCENTER'). Barycentres 1 to 9 return a garbage FrameName. In verify/J/unk, new CelestialBody(12345678) throws 'name can't be defined', then 'frame can't be defined', then builds a body named '' or 'g' with frame 'X!..U' and GM 0, on 3 of 3 runs. Fix: zero the arrays, return a found flag, and have SpiceApi throw ArgumentException naming the id. This is the #454 family; no open issue. No issue yet.
- **An impulse whose engine was never added to the spacecraft throws NullReferenceException mid-propagation; AddEngine accepts a tank that is equal by value but was never added.** study/C-spacecraft-maneuvers-attitudes/s3 (B2, H): NullReferenceException at Engine.cs:52 (FuelTank.Spacecraft is null), through ImpulseManeuver.cs:133 and ManeuverEventDetector.cs:99. AddEngine checks FuelTanks.Contains (Spacecraft.cs:296), which uses FuelTank's value equality (name, model, serial), so a tank rebuilt by value passes. Expected: ArgumentException naming the maneuver, the engine and the spacecraft, raised in one validation pass before ResetPropagation and designed with #586's binding. Related to #586, but not the same defect. No issue yet.
- **A ground station cannot be the observer of SpiceAPI.ReadEphemeris: 'Celestial celestialItem frame can't be defined'.** SpiceAPI.ReadEphemeris(t, 399013 /* DSS-13 */, 301, frame, ab) builds the observer with CelestialItem.Create (SpiceAPI.cs:846). The site id is classed as an asteroid, so it becomes a CelestialBody, which throws at CelestialBody.cs:167-169 after a SPICE(KERNELVARNOTFOUND) BODY399013_RADII banner (verify/D/paths, first version of Program.cs). SpiceAPI.FindWindowsOnCoordinateConstraint accepts the same id as observer. Related to #651 (site ids classed as asteroids), but a separate defect. No issue yet.
- **Frame.TEME on the CSPICE coordinate search returns 0 windows with SPICE(UNKNOWNFRAME).** study/D-window-searches-geometry/s5 #6 and its verifier: a FindWindowsOnCoordinateConstraint in Frame.TEME from a CelestialItem observer goes to gfposc with the name TEME (SpiceFrameName maps only GCRF, SpiceAPI.cs:179-180) and returns an empty list. #558 lists only CIRS and TIRS for this path; extend it to TEME and route it to the managed search. This changes results (windows where there were none), so the PR states it. Tracked by #558.
- **The illumination method is ignored on the managed path, and a misspelt method gives 0 windows on CSPICE.** study/D-window-searches-geometry/s4 #2f and its verifier: method "Dsk" gives SPICE(INVALIDMETHOD) and 0 windows on the CSPICE path, and 3 windows on the managed path, which ignores the parameter. N0067 gfilum accepts 'ELLIPSOID' and the DSK forms ('DSK/UNPRIORITIZED'); the managed path has no DSK. Under D1, a setting that cannot apply throws: an enum with Ellipsoid only, or no parameter. After R03's PR 1 the CSPICE path throws; the managed path still needs the fix. No open issue. No issue yet.

### R04. Public surface: interop DTOs, provider seam, raw SpiceApi
**Gain** medium · **Cost** M · **Breaking** yes · **Results** unchanged · **C++** no · **Issues** #662, #450, #655, #664

**Today.** 14 public P/Invoke structs, 11 named as domain types (7 CS0104 in study/J-fdg-cross-cutting/s1-usings), reached by 5 members (one protected, CelestialItem.cs:72). SpiceAPI's 10 conversion overloads twin the object API, one 68.5 km off (#450, study/A-onboarding-kernels-config/s13).
```csharp
using BodyInfo = IO.Astrodynamics.DTO.CelestialBody; // CS0104 without
memory.AddCelestialBodyInfo(new BodyInfo(399, 10, 3, "EARTH", new(6378137, 6378137, 6356752), 3.986004418e14, "ITRF93", 13000, 1.08e-3, 0, 0));
Configuration.Instance.SetDataProvider(memory);  // no getter, no scope
var earth = new CelestialBody(399);  // KeyNotFoundException '3'
var raw = SpiceAPI.Instance.ConvertEquinoctialElementsToStateVector(eq);  // #450
```
**Proposed.**
- DTO internal; the 5 members take a record `CelestialBodyInfo` of its fields (FrameId, J2-J4 optional).
- Missing data: InvalidOperationException, as SPICE (own PR).
- `Configuration.Instance.DataProvider` getter, `UseDataProvider(p)` scope (AsyncLocal).
- Internal: the 10 overloads, DictionnaryExtension, Enumeration, ArrayBuilder, ProfilesConfiguration, Utf8StringWriter, both propagation caches, VVIntegrator.DeltaTs.
- EventInfo, EventResult, IntegrationResult: readonly.
```csharp
memory.AddCelestialBodyInfo(new CelestialBodyInfo(NaifId: 399, Name: "EARTH", CenterOfMotionId: 10,
    BarycenterOfMotionId: 3, Radii: new Vector3(6378137, 6378137, 6356752), GM: 3.986004418e14, FrameName: "ITRF93"));
using (Configuration.Instance.UseDataProvider(memory))
    earth = new CelestialBody(399);  // orbit null, as SPICE
```
**Breaks and migration.** No forwarder: DTO, the overloads (twin table; AtEpoch counts TT about the Earth, #508), helpers, struct setters, ExtendedInformation (private protected). Custom providers change a type; IVT: CLI, ColdStart (#623). 29 smoke-test calls go.

**Tests.** Reflection: no public or protected member exposes an internal type; no mutable struct. SpiceDataProvider's record equals the DTO for each body of Data/SolarSystem; suite, conformance report bit for bit. Scope: restore, nesting, parallel, statics.

**Results, in detail.** none: SpiceDataProvider and MemoryDataProvider results stay bit for bit, because CelestialBodyInfo holds the DTO's fields, which are already SI (Proxy.cpp:545-551 converts the radii to m). The 10 conversion overloads move to internal and their code does not change, so no golden moves. The MemoryDataProvider exception type, which lets a body without a barycentre state build, changes only in its own defect PR.

**Depends on.** D1, D2, R03. **C++ change.** no.

**Pull requests.** Lands in D2's migration. PR 1: DTO internal, CelestialBodyInfo, the IDataProvider contract, the Configuration getter and UseDataProvider. PR 2: the SpiceApi conversions and helpers internal (IVT for the CLI and ColdStart, the SmokeTests calls removed) and the readonly event structs. MemoryDataProvider's exception type lands before PR 1, in its own PR.

**Open choices.**

1. **Fate of the 10 SpiceApi conversion overloads (5 methods: ConvertTleToStateVector, ConvertStateVectorToConicOrbitalElement x2, ConvertEquinoctialElementsToStateVector, ConvertConicElementsToStateVector x3, Propagate2Bodies x3).** Recommendation: Make them internal in 11.0.0 and list them under Removed. The library calls only the internal ConvertTleToStateVector overload (TLE.cs:702, SpiceAPI.cs:1214). The public overloads are called by tests only: IO.Astrodynamics.Tests and CLI.Tests, which already have InternalsVisibleTo and use them as SPICE references; ColdStart (the #623 path of CLAUDE.md), which gets IVT; and SpiceAPISmokeTests (29 calls against the package), whose calls are removed. One overload is 68.5 km off (#450, re-run in study/A-onboarding-kernels-config/s13). Two end the process for an unnamed centre (#655). An obsolete forwarder would keep the wrong conversion on sale for one more release. Alternative: [Obsolete] IOA0001 until 12.0.0, as D1 rule 9 does. That requires fixing #450 and #655 first, which means fixing code that is about to be removed.
2. **How a test or a service chooses its data provider.** Recommendation: A public getter, Configuration.Instance.DataProvider, plus a scope, UseDataProvider(p): IDisposable over an AsyncLocal. The repository needs it: TemeFrameTests.cs:235-236 never swaps the global provider, because objects that other test classes build during a swap would capture it. Document the contract. An object keeps the provider that was current at its construction, after the scope too. The static catalogues and the static frames (Frame.ICRF and the others) are built with the process provider (SetDataProvider), never the scoped one. The window searches and kernel writers call SpiceApi directly and still need kernels. Alternative: The getter only. It is smaller, but parallel tests cannot isolate a provider.
3. **Iau2006Model and Iau2006FundamentalArguments (missed by the study, area G).** Recommendation: Keep them public. frames.md:204 documents Iau2006Model as the CIO chain, and validation.md:1238 cites both against SOFA. VVIntegrator.DeltaTs and HalfDeltaTs have no reader outside VVIntegrator.cs and become internal. Alternative: Make them internal with the other helpers. Readers of frames.md then lose the reference implementation they can call.

**Candidate defects found on the way.**

- **MemoryDataProvider throws ArgumentException or KeyNotFoundException for data it lacks, so a body that builds on SPICE cannot be built from memory.** A missing state throws ArgumentException in GetEphemerisFromICRF (MemoryDataProvider.cs:64, :69). A missing frame throws ArgumentException in FrameTransformationToICRF (:33). A missing body info throws KeyNotFoundException from the dictionary indexer of GetCelestialBodyInfo. CelestialItem.ReadStateAboutBarycenter (CelestialItem.cs:168-178) catches only InvalidOperationException, which is what SPICE throws. On SPICE, a body without its barycentre state builds with InitialOrbitalParameters null (#454). On memory, the same body throws. Measured in study/A-onboarding-kernels-config/s07-memory-provider: 'No state vectors found for this target (3)'. Re-run in work/r04r28-rev/r04b: KeyNotFoundException '3' with only the Earth's info. #559 covers frame and aberration only, and no open issue tracks this. No issue yet.
- **GetCelestialBodyInfoProxy returns uninitialised Name and FrameName, so an unknown id or a body without RADII builds from stack bytes.** IO.Astrodynamics/API/DTO/CelestialBodyDTO.h:15,18 declares `char Name[32]; char FrameName[32];` with no initialiser. Proxy.cpp:522-584 fills them only when bodc2n_c finds the id, and continues after a failed bodvcd_c. In verify/J/unk, new CelestialBody(12345678) gives three different outcomes in one process. CelestialBodyInfo can carry the 'not found' signal (CelestialItem.cs:145-148) reliably only after the native fix (zero-initialised buffers, bool plus GetLastErrorProxy). R03 owns this fix with #454. Tracked by #454.

### R05. Documentation that compiles and runs, checked by CI
**Gain** high · **Cost** M · **Breaking** no · **Results** unchanged (docs and CI) · **C++** no · **Issues** #662, #556

**Today.** #662's 100 lines in 62 blocks fail, plus 30 its placeholders hid (study/I/run-elide). CI runs check-versions.sh and `mkdocs build --strict` only (ci.yml:28-46); SmokeTests copy 3 of 240 blocks. No project sets GenerateDocumentationFile. No page covers `astro`.
```csharp
// tle-and-omm.md:24 with #662's fix compiles, under 'TLE fields'
Console.WriteLine($"{tle.Inclination() * Constants.Rad2Deg:F4}"); // 51.6622, SGP4 osculating
Console.WriteLine($"{tle.MeanInclination * Constants.Rad2Deg:F4}"); // 51.6423, the TLE field
```
**Proposed.** Compile at every PR, run at release.
- Job `docs-snippets` (ci.yml): each page of README and docs/ is one program, blocks in order, `#line` to the Markdown, D2's implicit usings once its props land. A shrink-only baseline lets it land first; each rename PR fixes its doc lines.
- HTML-comment markers before a fence: `signature`, `isolated` (VV/RK7(8) tabs), `run`. Each tutorial opens with a visible Setup block; data comes from the page (`tle.ToOmm()`).
- `run` pages execute in SmokeTests on the package, three OSs (cd.yml), asserting properties.
- Backticked table members checked against D2's PublicAPI files.
- DEVELOPER_GUIDE.md becomes a pointer, its unique content moved to docs/.
- GenerateDocumentationFile once #556 is fixed (#550 is); CS1591 a warning.
- reference/cli.md generated from the tool's `astro --help`, diffed in CI; a CliRunner (`Program.BuildApp()`, a process per run) runs its `astro` lines.
```csharp
// <!-- snippet: run --> on tle-and-omm.md; SmokeTests compare with the field text
Console.WriteLine($"Inclination: {tle.MeanInclination * Constants.Rad2Deg:F4} deg (mean)");
```
**Breaks and migration.** No API change; anchors stay as pointer headings. check-versions.sh:9,47-48, DeveloperGuideExamplesSmokeTests, DateTimeTest.cs:310, README.md:22 and CONTRIBUTING.md:15 follow.
**Tests.** On main the job reports the 130 lines at their lines; renaming `LambertSolution.V1` fails lambert.md; `ApogeeRadius()` (keplerian-elements.md:41) fails the table scan; the conjunction run asserts avoidance options and Source Propagated.

**Results, in detail.** none

**Depends on.** D2. **C++ change.** no.

**Pull requests.** PR 1: docs-snippets job with its shrink-only baseline, #662's fixes and the 30 extra lines (after D2's infrastructure PR, which brings the PublicAPI files, and before the D1-D4 rename PRs); PR 2: DEVELOPER_GUIDE.md to a pointer, check-versions.sh, DeveloperGuideExamplesSmokeTests and DateTimeTest.cs:310 retargeted; PR 3: GenerateDocumentationFile after #556; PR 4: reference/cli.md and the CliRunner

**Open choices.**

1. **Marker syntax for skipped, isolated and run blocks.** Recommendation: An HTML comment on the line before the fence (<!-- snippet: signature|isolated|run -->), carrying the 4-space indent inside a `=== "tab"`; confirm with mkdocs build --strict before merging (pymdownx was not installed where it was studied). Alternative: An info string after the language (```csharp signature), #662's proposal, if superfences accepts the bare word without losing highlighting.
2. **Unit of compilation.** Recommendation: Each page as one program, blocks in order (the reader's model, no placeholders that hide errors); pages that redeclare names are rewritten (get-started.md declares earth at :50 and :66, epoch at :52, :67 and :93, CS0128 once concatenated), alternatives marked isolated. Alternative: Each block alone with automatic chaining of earlier declarations, which keeps the pages as written but needs a resolver and still hides undeclared names.
3. **Scope when the job lands.** Recommendation: Every page of README and docs/ from the start, with a baseline of failing pages that may only shrink, so the D1-D4 rename PRs each fix and prove their doc lines. Alternative: Compile-only on README, get-started and the tutorials first (the verifier's cheap step), the reference pages added once fixed.
4. **Hidden setup code.** Recommendation: A visible Setup block at the top of each tutorial (kernels, earth, epoch, a spacecraft); no hidden setup except scaffolding a reader never needs to run. Alternative: A <!-- snippet-setup ... --> comment holding prerequisites, compiled and run but not shown, which keeps pages shorter but makes them not self-contained for the reader.
5. **DEVELOPER_GUIDE.md.** Recommendation: Retire it to a pointer whose headings link to the site pages, move its unique content (OPM covariance, Cosmographia, MemoryDataProvider) to docs/, remove its two checks from check-versions.sh, retarget DeveloperGuideExamplesSmokeTests and the comment at DateTimeTest.cs:310. Alternative: Keep it and add it to the compile job, which keeps two copies of each example to maintain (CONTRIBUTING.md:15).
6. **ApogeeRadius() listed at keplerian-elements.md:41 and DEVELOPER_GUIDE.md:480.** Recommendation: Correct the table here; adding ApogeeRadius() beside PerigeeRadius() is an additive API choice for the orbital-parameters section, out of scope of R05. Alternative: Add ApogeeRadius() in this PR so the table becomes true (the verifier's recommendation, for symmetry).
7. **New CLI commands for the TLE/OMM and conjunction scenarios.** Recommendation: Defer `astro tle` and the like to 11.1; 11.0.0 documents the CLI and tests its parser. Alternative: Add `astro tle` (SGP4 states over a window) in 11.0.0.

**Candidate defects found on the way.**

- **30 documentation lines beyond #662's tables fail, #662's fix of tle-and-omm.md:24 prints the osculating inclination under 'TLE fields', and get-started contradicts itself.** Counted in study/I/run-elide/errors.json: batch-propagation.md:69 r.ElapsedTime (CS1061; ElapsedTime is on BatchResult); monte-carlo.md:117-118 MeanRssPosition/StdRssPosition; numerical-propagation.md:163-164 .Position/.Velocity on InterpolateAt's ValueTuple; spice-api.md:96 and DEVELOPER_GUIDE.md:2067 customEarth undeclared, and the table at spice-api.md:84 not naming DTO.CelestialBody; 23 lines in 18 blocks use names declared nowhere on their page (covariance-propagation.md:215/217 WithRtnCovariance, a smoke-test helper, among them). tle.Inclination() = 51.6622 deg vs MeanInclination 51.6423 deg (TLE.cs:828 vs :122; compiled in design/work/r05-review). tle-and-omm.md:106 reads satellite.omm, which nothing provides. get-started.md:20-21 says the Frames accessor is gone while :54 and :78 use Frames.Frame.ICRF; its VV tab (10 s step) ends 1713.7 m from the RK7(8) tab after 2 h, 17.1 m at 1 s, and the page says neither (rerun of verify/I/gsmin). Add to #662. Tracked by #662.
- **Conjunction tutorial compiles only with guessed context, its covariance block throws and its avoidance block returns no option.** docs/tutorials/conjunction-assessment.md: earth, epoch, perturbingBodies, step, window, secondary1..3 undefined; ConsiderParameters(dragCoefficientSigma: 0.2) (:186) with includeAtmosphericDrag true and no atmospheric model throws 'A consider parameter needs its force in the propagation' (verify/E/v1); the avoidance block (:211) returns 0 options, because the tutorial's TCA is 9 s after the epoch and its 1, 3 and 6 h leads precede any window that starts there (study/E/s1 also got 0 with 20 to 60 min). Docs fix in #662's PR; the silent drag drop is its own defect below. Tracked by #662.
- **Requested atmospheric drag is dropped silently when no body in the list has an atmospheric model.** CentralBodyPropagator.cs:246-250 keeps only bodies with HasAtmosphericModel, and PlanetsAndMoons.EARTH_BODY has none (PlanetsAndMoons.cs:175). get-started's RK7(8) tab with .IncludeAtmosphericDrag().IncludeSolarRadiationPressure() ends at 0.000 m from the same run with SRP only, bit for bit (1.29 cm from the run with neither, the SRP; design/work/r05-review). The conjunction CDM writes ATMOSPHERIC_MODEL NONE. D4 item 7; no open issue yet. No issue yet.
- **Release notes cite a CLI option that does not exist, and the README misspells a command.** docs/contributing/versioning.md:1394 cites 'the CLI `--frame TEME`'; the frame of the CLI commands is a positional argument (e.g. EphemerisCommand.cs:29, OrbitalParametersConverterCommand.cs:28) and PropagateCommand has none. README.md:122-137 writes `find-windows-from-FOV-constraint`, help shows `find-windows-from-fov-constraint` (both work). Fix with the CLI page PR. No issue yet.

### R06. One PropagationOptions for every propagation
**Gain** high · **Cost** L · **Breaking** yes · **Results** unchanged on old signatures; new path per D4 · **C++** no · **Issues** #583

**Today.** One RK7(8) run: 6 entry points, 5 parameter orders, 3 names of one step, 6 covariance switches, 0 m apart (study/B-propagation/s03). Albedo 0.3 moves a LEO day 0.427 m by the builder, 0 m by Propagate (work/r06-r07). A sweep rebuilds 5 objects (s06).
```csharp
sc.Propagate(window, bodies, new RK78Integrator(1e-12, 1e-12), true, true, step);
new CentralBodyPropagatorBuilder(window, sc, new RK78Integrator(1e-12, 1e-12), step)
    .WithPerturbingBody(sun).IncludeAtmosphericDrag().IncludeSolarRadiationPressure().Build();
```
**Proposed.** A D1 record on Spacecraft.Propagate, CentralBodyPropagator, Scenario.Simulate (R28), PropagationTask, MonteCarloConfiguration.Propagation, BatchPropagator.Propagate: Integrator (values, TimeSpan steps, R07), OutputStep (60 s), ThirdBodies (null: none), four force bools, Geopotential (null: the body's), Covariance (null: Auto, D4). One force builder: the central body always takes part; a twin with another model throws, naming both; drag on a model-less Earth takes NRLMSISE-00 nominal; Albedo reads the body's coefficient. GeopotentialModelParameters: an immutable value, Egm2008(degree) embedded.
```csharp
var rk = new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 };
var o = PropagationOptions.Default with { Integrator = rk, ThirdBodies = [sun],
    AtmosphericDrag = true, SolarRadiationPressure = true };
foreach (var n in new[] { 2, 10, 20 })
    sc.Propagate(window, o with { Geopotential = GeopotentialModelParameters.Egm2008(n) });
```
**Breaks and migration.** Flag overloads, builder, positional task (Deconstruct kept), MonteCarlo factories: IOA0001 forwarders with legacy rules until 12.0.0. Hard: GeopotentialModelPath and the field's StreamReader constructor removed, degree int, DeltaTs internal.
**Tests.** Each forwarder equals its options twin bit for bit: both integrators, fixed-step RK7(8), drag, SRP, albedo, thermal, covariance modes, maneuvers, SSB, non-ICRF start. Egm2008(2, 10, 70) is the file's; GMAT 001-003 kept; 5 precedence rows for Geopotential; s02's misplaced Earths throw or apply; the CDM names the resolved field and atmosphere.

**Results, in detail.** None under the existing signatures. Every flag overload, the builder, the positional PropagationTask and the MonteCarlo factories forward bit for bit with their own legacy rules: each path's central-body rule, Velocity-Verlet at the output step where no integrator is given, and the albedo or thermal body as given. The following moves happen on the options path only, and D4's defaults PR states them. (1) An unlisted central body always takes part, which adds drag and the SRP shadow. In s02 (VV at 60 s, EarthStandardAtmosphere), an atmospheric Earth adds 4.500 km of drag over a LEO day when listed, and 0 m today when it is only the observer. (2) Drag on a model-less Earth uses NRLMSISE-00 nominal. (3) Under Auto, a P0 at the window start is propagated: the trajectory moves 0.000 m at 1.8 times the cost, and the conjunction Pc moves as D4 states. The run-owned geopotential field is claimed to give no change only once a test shows it bit for bit against today's body field at degrees 2, 10 and 70, with GMAT conformance propagators 001-003 kept.

**Depends on.** D1, D2, D4. **C++ change.** no.

**Pull requests.** PR 1: the single-use defect PR, then GeopotentialModelParameters as an immutable value with Egm2008(degree) embedded, bit for bit. PR 2: PropagationOptions, the integrator option records (fixed-step RK7(8) included), the one force builder and the IOA0001 forwarders, results unchanged. PR 3 is D4's defaults PR, shared with R07: the new-path rules, with the moved figures stated. #583 (no reset on the builder and constructor paths) stays its own PR.

**Open choices.**

1. **Albedo or ThermalRadiation is asked and the central body's coefficient is 0, as on every predefined body (missed by the study: today only the builder reads the coefficients).** Recommendation: Throw ArgumentException naming the CelestialBody constructor parameter, as AlbedoRadiationPressure.cs:41-43 does today (measured: 'Reflecting body EARTH must have a positive albedo (got 0)'). The library holds no cited Earth albedo or emissivity. Alternative: Use a documented Earth default (D1 rule 6, 'neither: the documented default') once a value is cited and confirmed by S. Guillet.
2. **Which geopotential field a run uses.** Recommendation: Each run builds its own field from the model, whether the model comes from the body or from the options. The immutable coefficient table is shared, and the buffers belong to the run. The forces, the RK7(8) evaluations of the solution, the conjunction replay and LastForceModel then never share a field, so Batch, Scenario and MonteCarlo can share one Earth with EGM2008, and MonteCarloConfiguration.CelestialBodiesFactory becomes obsolete. The thread rule of CLAUDE.md stays for the public CelestialItem.EvaluateGravitationalAcceleration (CelestialItem.cs:765), which still reads the body's own field. Alternative: Only a Geopotential given in the options gets a per-run field. A body's own field stays shared, and 'one CelestialBody per task' stays the rule.
3. **RK78Integrator's fixed-step mode (RK78Integrator(double fixedStepSize), RK78Integrator.cs:133-143, used by 9 test sites) in the options.** Recommendation: RK78IntegratorOptions.FixedStep, a nullable TimeSpan, where null means adaptive. Setting it together with a tolerance or step bound other than its default throws ArgumentException at the start of the call (D1 rule 3). It forwards to the double constructor bit for bit. Alternative: A third sealed record, RK78FixedStepIntegratorOptions, in the closed IntegratorOptions hierarchy.
4. **ThirdBodies on Scenario.SimulateAsync.** Recommendation: Null means the scenario's AddCelestialItem bodies. When both are given and differ, throw ArgumentException (D1 rule 6). Alternative: Make AddCelestialItem obsolete for the simulation, so ThirdBodies is the only source.
5. **How covariance propagation is turned off under Auto.** Recommendation: CovariancePropagationOptions.None, a static instance backed by a new CovariancePropagationMode.None (value 2; StateTransitionOnly 0 and Covariance 1 keep their implicit values, CovariancePropagationOptions.cs:12-24). ProcessNoise or ConsiderParameters set with it throws in init. The obsolete paths reject None. Alternative: A separate bool on PropagationOptions next to Covariance. That gives two settings for one concept.
6. **Albedo or thermal radiation of a body other than the central body (the builder's IncludeAlbedo(anyBody)).** Recommendation: The options cover the central body only. Another body goes through the CentralBodyPropagator IIntegrator constructor with a user-built force list. The obsolete builder keeps the old behaviour until 12.0.0. Alternative: AlbedoBody and ThermalBody properties on PropagationOptions, where null means the central body.

**Candidate defects found on the way.**

- **GeopotentialModelParameters is single-use: a second CelestialBody built from the same instance throws ObjectDisposedException (missed by the study; a prerequisite of R06's geopotential part, per D1).** GeopotentialModelReader.cs:18 wraps the caller's StreamReader in `using`. GeopotentialModelParameters.GeopotentialModelPath is that reader (GeopotentialModelParameters.cs:53, :92). Measured again in work/r06r07-rev, and in verify/B/c2: the second `new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch, gp)` throws 'Cannot read from a closed TextReader'. Not in open-issues.md. No issue yet.
- **The CentralBodyPropagator constructors and Spacecraft.Propagate leave out an unlisted central body and ignore a listed twin's geopotential, while the builder adds the central body.** CentralBodyPropagator.cs:227-259 takes gravity from the observer (:234). It skips any listed body equal to the observer by NAIF id (:237-243), and applies drag and the SRP shadow only for listed bodies (:245-255). CentralBodyPropagatorBuilder.cs:138-143 adds the central body. In s02 (verify/B/s02-first-propagation/out.txt, VV at 60 s), an EGM2008 Earth listed while the orbit is on EARTH_BODY ends 0.000 m from two-body, against 1.844e6 m when it is the observer. An atmospheric observer that is not listed gives 0 m of drag. An atmospheric twin listed while the orbit is on EARTH_BODY gives 4.500 km of drag, with gravity taken from the other Earth. Listed by D4. No issue yet.
- **Requested atmospheric drag is dropped silently when no listed body has an atmospheric model (every predefined Earth).** CentralBodyPropagator.cs:245-251 filters on HasAtmosphericModel, which is False for PlanetsAndMoons.EARTH_BODY (CelestialBody.cs:95, measured). With includeAtmosphericDrag: true the run gives 0 m and no error (s02). docs/tutorials/numerical-propagation.md:128 promises NRLMSISE-00 right after an example on the predefined Earth (missed by the study). Listed by D4. No issue yet.
- **CelestialBody.GetAtmosphere returns NRLMSISE-00 for the Earth whatever model the body was built with, while the drag force reads the configured model.** CelestialBody.cs:543-552 returns Nrlmsise00Model for NaifId 399 with a full context and never reads AtmosphericModel. GetAirDensity reads AtmosphericModel and gives 0 without one (:495-497). EARTH_BODY.GetAtmosphere(ctx) gives 5.24e-12 kg/m³ at 400 km while drag adds nothing (verify/B/c2). The same Earth therefore has three atmosphere defaults: GetAtmosphere, drag, and the CLI's EarthStandardAtmosphere (CLI Helpers.cs:161). Listed by D4. No issue yet.

### R07. Default integrator: RK7(8), the Velocity-Verlet trap closed at compile time
**Gain** high · **Cost** M · **Breaking** yes · **Results** old paths bit for bit; options default 974 km → 1.36 m off Kepler, Hohmann burn 210 s earlier · **C++** no · **Issues** #389, #439, #576

**Today.** Calls without an integrator step Velocity-Verlet at the output step (CentralBodyPropagator.cs:209-225). At the tutorial's 60 s, a point-mass LEO day ends 974 km from Kepler; RK78Integrator() 1.36 m, in 13-27 ms against 10-14 (study/B-propagation/s07). A Hohmann to GEO burns 3.6 m/s low and 210 s late (study/C-spacecraft-maneuvers-attitudes/s2). propagatorStepSize is the VV step in one overload, the output step in another (Spacecraft.cs:542, :576).
```csharp
var vv = sc.Propagate(window, new CelestialItem[] { earth }, false, false, step); // VV at 60 s
var rk = sc.Propagate(window, new CelestialItem[] { earth }, new RK78Integrator(), false, false, step);
```
**Proposed.** Integrator defaults to RK78IntegratorOptions.Default (1e-9/1e-9, an initializer per D1 rule 2; 1e-12 failed in SSB mode, #389). OutputStep is an output step only; VV takes its own step. Without ThirdBodies the force model is the central body (D4). Solution.Options shows what ran.
```csharp
var s = sc.Propagate(window);                          // RK7(8) 1e-9, outputs every 60 s
var vv = sc.Propagate(window, PropagationOptions.Default with
    { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1) } });
```
**Breaks and migration.** The 5-argument Propagate(Async), 6-argument CentralBodyPropagator, SimulateAsync(drag, srp, step) and a factory-less PropagationTask get [Obsolete(error: true)], IOA0002, naming new VVIntegrator(step) (974 km a day at 60 s); PropagateAsync, with no integrator overload, names Task.Run of Propagate. Binaries keep their numbers; the fix is bit for bit (1441/1441 states with Sun, Moon, SRP, on 4 paths). 75+ repository sites and PropagateCommand.cs:62 (R29) move with it.
**Tests.** The fix equals each old path bit for bit, MonteCarlo included; the default equals explicit RK78IntegratorOptions.Default bit for bit; Kepler within twice the worst of 3 platforms, the Hohmann Δv the closed form; an SSB run completes; conjunction moves stated; long-validation.yml.

**Results, in detail.** Compiled binaries keep their results, because the integrator-less members forward bit for bit. A rebuild that passes new VVIntegrator(step), or a VV factory, is also bit for bit: 1441/1441 states with Sun, Moon and SRP on Spacecraft.Propagate, PropagateAsync, PropagationTask/Batch and Scenario.SimulateAsync (work/r06r07-rev/legacy). So no repository golden moves. On the options path the default becomes RK7(8) at 1e-9/1e-9:
- The s07 point-mass LEO day moves from 974 km to 1.364 m off Kepler.
- The Hohmann circularization burn starts at 05:14:58.375 instead of 05:18:28.355 TDB. Measured at the 1e-9 default in work/r06r07-rev/hohmann: Δv 2425.732 and 1466.824 m/s, the closed form's, where Velocity-Verlet gives 2426.238 and 1463.229; final e 7.0e-11 against 3.852e-3.
- Events move from step ends to RK7(8) refinement, which moves ExecutedManeuvers, the attitude timeline and the SPK arcs.
- Avoidance options follow the participant's integrator.
- SSB mode about the Earth runs at 118 to 372 ms per propagated hour, against 3 to 4 ms for VV (D4's two measurements).
These are measured by long-validation.yml on D4's defaults PR, against Kepler, the Hohmann closed form, GMAT and Pc2D (mpmath). The CLI moves in R29.

**Depends on.** R06, D1, D4. **C++ change.** no.

**Pull requests.** D4's defaults PR, after R06's PR 2 and D4's defect PRs. It carries the RK7(8) default, VVIntegratorOptions, and the error-level [Obsolete] on the integrator-less members. In the same PR, the repository's call sites (75+ by grep) move to new VVIntegrator(step), and so does the CLI's own SimulateAsync call (PropagateCommand.cs:62), which would otherwise stop the CLI build.

**Open choices.**

1. **Default Step of VVIntegratorOptions.** Recommendation: 1 s, a constant: the CLI's step today (PropagateCommand.cs:62). It ends a point-mass LEO day 271 m from Kepler in 53 to 56 ms, against 27.1 km at 10 s and 974 km at 60 s (work/r06r07-rev; the error grows as h²). D1's Default == new() rule holds. Alternative: No default: Step is a required member, and VVIntegratorOptions is exempt from the reflection test's Default rule. The user must state the step that sets Velocity-Verlet's accuracy.
2. **PropagationTask and MonteCarloConfiguration without an integrator factory.** Recommendation: PropagationTask gets an [Obsolete(error: true)] 6-parameter constructor carrying D4's message, because removing '= null' alone gives CS7036, which names no fix. Binaries call the 7-parameter constructor and keep VV. MonteCarloConfiguration:
- DeltaT, CelestialBodiesFactory and IntegratorFactory lose `required` (a relaxation, no break) and become IOA0001 obsolete.
- A configuration with DeltaT and no IntegratorFactory runs the legacy VV bit for bit, and DeltaT's message names the 974 km trap.
- The new Propagation member set together with any legacy member throws ArgumentException.
The draft's runtime throw was dropped: it would have failed compiled binaries that omitted IntegratorFactory and ran VV. The cost is that the rebuild trap is a warning here, not an error. Alternative: D4 as written: MonteCarloConfiguration.IntegratorFactory becomes required. Options-path users must then set an obsolete member (IntegratorFactory = null), and the obsolete DeltaT and CelestialBodiesFactory stay required.

### R08. Independent runs: no state kept between propagations
**Gain** high · **Cost** M · **Breaking** yes · **Results** unchanged on correct code; #583/#584/#586 move reruns to a fresh run's · **C++** no · **Issues** #583, #584, #586, #581

**Today.** A reused integrator keeps each run's forces (work/r08r09/r08). A second `Propagate()` on one propagator fires no burn: 5.21e6 m off (work/r08r09-rev/rearm). A second `SetStandbyManeuver` is ignored (study/C-…/s4); a `TryExecute` preview arms the chain (verify/C/v1).
```csharp
var integrator = new RK78Integrator(1e-10, 1e-10);
a.Propagate(window, bodies, integrator, false, false, step);
b.Propagate(window, bodies, integrator, false, false, step); // 1.04e7 m
c.SetStandbyManeuver(m1); c.SetStandbyManeuver(m2);          // m1 fires, m2 dropped
```
**Proposed.** A run builds what it mutates. D1 gives each run its own integrator; `SetManeuvers` sets the plan. A propagator re-arms the spacecraft in `Propagate`, after its checks, and runs once: re-armed, one RK7(8) instance still ends 8.7e-4 m from a fresh run. `TLEPropagator` replaces its states, no longer adds.
```csharp
var o = PropagationOptions.Default with { Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-10 } };
sc.SetManeuvers(new ApogeeHeightManeuver(r1), new PerigeeHeightManeuver(r2), TriadAttitude.CreateLvlh());
var run1 = sc.Propagate(window, o);
var run2 = sc.Propagate(window, o);   // equal to run1 on every path
Vector3 dv = new ApogeeHeightManeuver(r1).ComputeDeltaV(state); // mutates nothing
```
**Breaks and migration.** `SetStandbyManeuver` and `TryExecute` are IOA0001-obsolete; the `minimumEpoch` overload stays to 12.0 (#573). The propagation calls an internal execute, so `TryExecute` is sealed on `ImpulseManeuver` and `Attitude`: an override it would skip gives CS0239. A second `Propagate()` on one propagator throws. A maneuver in two plans throws (#586): one object no longer serves two spacecraft.
**Tests.** Eccentric orbits, every path, both integrators, covariance, SSB, ECLIPJ2000: reruns equal a fresh spacecraft bit for bit, fuel Tsiolkovsky; a rejected build leaves run 1. Legacy paths, `SetManeuvers`: bit for bit with today. `ComputeDeltaV` equals the executed ΔV per type; tanks, chain, gates, node lock (#581) unchanged.

**Results, in detail.** None on correct code. The options path builds one integrator per run. Spacecraft.Propagate's legacy overloads stay bit for bit: today they build the propagator first and reset after it (Spacecraft.cs:550-552), and PropagatorBase.Propagate keeps that order. The PRs of #583, #584 and #586 move reruns (builder, direct constructor, same instance, a repeated SetStandbyManeuver, a shared maneuver) to a fresh spacecraft's results. Today those reruns are 7.247e6 m off (#583), 5.21e6 m off on the same instance (work/r08r09-rev/rearm) and 3.028e6 m off (#584). Each is measured against a fresh spacecraft bit for bit and against Tsiolkovsky for the fuel. The shared-integrator PR turns a silent 1.04e7 m error into an ArgumentException. TLEPropagator reruns keep only the last window's states. This is stated in PR 5.

**Depends on.** D1, D2, R06, R13. **C++ change.** no.

**Pull requests.** PR 1: #583 alone. The reset moves into PropagatorBase.Propagate, and a second Propagate() on one instance throws (re-armed, one RK7(8) instance still ends 8.7e-4 m from a fresh run). PR 2: #584. PR 3: #586, designed with R13's engine resolution. PR 4: the shared-integrator defect (own issue). PR 5 goes in D1's options PR with R13's maneuver constructors: SetManeuvers, ComputeDeltaV, TryExecute sealed and obsolete, TLEPropagator replacing its states, and the IOA0001 obsoletes.

**Open choices.**

1. **#583: what a builder or direct propagator does with a spacecraft already propagated.** Recommendation: Re-arm it. ResetPropagation becomes internal. PropagatorBase.Propagate calls it after the constructor's checks, so a rejected construction leaves run 1 as it was (Spacecraft.cs:546-552 does this today). The constructor walks the plan from InitialManeuver. Today it also walks the standby the last run left (PropagatorBase.cs:95-96). This gives one rule on every path. Alternative: Single use: Propagate throws on a spacecraft already propagated. The builder would then be the only path that cannot rerun.
2. **A second Propagate() on the same propagator instance.** Recommendation: Throw InvalidOperationException naming Spacecraft.Propagate, in #583's PR. The constructor initializes the integrator (CentralBodyPropagator.cs:51), and Propagate does not (PropagatorBase.cs:384-410). Measured in work/r08r09-rev/rearm with e = 0.035 and one apogee raise, re-armed through ResetPropagation by reflection: RK7(8) ends 8.7e-4 m from a fresh run at 1e-9 and 4.5e-8 m at 1e-12, Velocity-Verlet 0 m, and a new instance 0 m. Under D1 each Propagate(window, options) builds its own propagator. Alternative: Re-initialize the integrator, its stepper and the caches at each Propagate, with a bit-for-bit proof per integrator and for a user IIntegrator.
3. **Fate of Spacecraft.SetStandbyManeuver(m).** Recommendation: IOA0001-obsolete and EditorBrowsable(Never). It forwards to #584's rule: it sets the head, null clears the plan, and it adds nothing to ExecutedManeuvers. SetManeuvers is then the one documented way. The minimumEpoch overload stays obsolete until 12.0 (#573, D1 correction). Alternative: Keep SetStandbyManeuver as a second public way, with #584's fix, beside SetManeuvers.
4. **Previewing a burn's delta-V before a propagation (missed by the study).** Recommendation: Add a pure ImpulseManeuver.ComputeDeltaV(StateVector). It works about the maneuver's center, or about the state's observer when no center is given, and returns the delta-V in the state's frame. It touches no tank, no chain, no record, no gate and no node lock (#581). The propagation calls an internal execute bound to the propagated spacecraft (#586). TryExecute becomes IOA0001-obsolete and is sealed on ImpulseManeuver and Attitude. Today the propagation calls it virtually (ManeuverEventDetector.cs:99, PropagatorBase.cs:875), and both classes have protected constructors. A user override would be skipped silently; sealed, it fails with CS0239 (and a TypeLoadException for a compiled one). Alternative: Keep TryExecute public and called by the propagation. Document that it burns fuel, records the windows and arms the next maneuver.
5. **TLEPropagator on a spacecraft already propagated (TLEPropagator.cs:47 adds its states to the stored ones).** Recommendation: Replace the stored states, as Spacecraft.Propagate does, so one rule covers every propagator. The change is stated under Breaking, and CLAUDE.md's 'TLEPropagator still adds its own' is updated. Alternative: Keep adding and document it on TLEPropagator, as CLAUDE.md does today.

**Candidate defects found on the way.**

- **The force-building constructors add their forces to the caller's Integrator: a reused instance integrates the forces of every run.** work/r08r09/r08: one RK78Integrator(1e-10, 1e-10) is used for two spacecraft without maneuvers (point-mass Earth, a = 7128 km, 4 h). After run 2 it holds 2 forces, and run 2 ends 1.040e7 m from run 1 (re-run 2026-11-13). study/B-propagation/s03 measures 3 then 6 forces and 8.799e6 m. Source: CentralBodyPropagator.cs:46-49 (integrator.AddForce on the caller's instance), CentralBodyPropagatorBuilder.cs:157 and :164, and the public Integrator.AddForce, which nothing clears (Integrator.cs:52-55). Scenario.cs:169-172 guards against it; Spacecraft.Propagate and the builder do not. Fix (cost S): the force-building paths throw ArgumentException when the given integrator already holds forces. The IIntegrator constructor for a user-built force list is not affected. The PR runs the suite to find any test that reuses an instance. No open issue. No issue yet.
- **A second Propagate() on the same CentralBodyPropagator fires no burn and continues from the integrator state of the first run.** work/r08r09-rev/rearm: e = 0.035, one apogee raise, 4 h, 30 s outputs. Run 1 is 0 m from a fresh spacecraft. Run 2 on the same instance fires no burn and ends 5.206e6 m off (RK7(8) and Velocity-Verlet). Re-armed by reflection, RK7(8) is still 8.7e-4 m off at 1e-9, while a new instance is 0 m. The constructor initializes the integrator, and Propagate does not (CentralBodyPropagator.cs:51, PropagatorBase.cs:384-410). Add the same-instance case to #583's test list. Tracked by #583.
- **SetStandbyManeuver called twice before a run fires the first maneuver; a TryExecute preview arms the chain the same way.** work/r08r09/r08: before the run InitialManeuver targets 8378137 m and ExecutedManeuvers is 1; after it, m2 (9000 km) has not fired. verify/C/v1-tryexecute-preview (re-run 2026-11-13): after m1.TryExecute, InitialManeuver is m2 through ChainStandbyManeuver (ImpulseManeuver.cs:154) and the private Standby (Spacecraft.cs:456-469). The later SetStandbyManeuver(m1) is ignored, and only m2 fires at 396.454 m/s, ending at a = 7581.526 km instead of 8378.137 km. Add the preview case to #584's tests. Tracked by #584.
- **A maneuver built on another spacecraft's engine writes fuel and records to that spacecraft.** work/r08r09/r08: y is propagated with x's maneuver. y's tank stays at 800.00 kg and y has 0 executed maneuvers. x's tank goes to 683.15 kg and x lists 1 executed maneuver, though x never ran. Engine.Ignite burns Engine.FuelTank.Spacecraft's tank (Engine.cs:50-55). Tracked by #586.

### R09. Results of a run on the solution, one reading rule
**Gain** high · **Cost** M · **Breaking** yes · **Results** unchanged; #527: extrapolation throws · **C++** no · **Issues** #527, #575, #658, #335

**Today.** From ECLIPJ2000, `InterpolateAt` is 1.52e6 m from the ICRF read, frame unnamed. At +12 h three readers follow three rules (work/r08r09/r09). `ExecutedManeuvers` is a live set the next run clears (study/C-…/s4).
```csharp
var (r, v) = solution.InterpolateAt(end);               // frame? clamps
solution.GetStateVector(end.AddHours(12));              // throws
sc.GetGeometricStateRelativeTo(end.AddHours(12), earth); // 5.8e14 m
sc.GetTotalFuel();                      // 800 kg after 116.85 kg burnt
```
**Proposed.** One rule: inside the stored states a read is a `StateVector` with frame and observer; outside, `ArgumentOutOfRangeException`. `solution.Maneuvers` lists sealed `ExecutedManeuver` records in execution order: the definition, `Epoch`, the unclipped `ThrustWindow` (may start before the window), `ManeuverWindow`, `DeltaV` in the solution's frame, `FuelBurned`, `MassBefore`, `MassAfter`. `SpacecraftSummary` reads them.
```csharp
StateVector s = solution.GetStateVector(end);   // throws outside
foreach (var m in solution.Maneuvers)
    Console.WriteLine($"{m.Epoch} {m.DeltaV.Magnitude()} {m.FuelBurned}");
double left = sc.GetRemainingFuel();   // GetInitialFuel(): old value
```
**Breaks and migration.** `InterpolateAt` is IOA0001-obsolete, unchanged; in covariance mode `GetStateVector` also computes P: 4 times the cost (work/r08r09-rev/readcost). `GetTotalFuel` is `[Obsolete(error: true)]` naming both: repointed, it would change meaning silently. The constructors and mutators of `PropagationSolution` and `PropagationSegment` become internal (the runner keeps them, InternalsVisibleTo). `ExecutedManeuvers` becomes a new `IReadOnlyList` per run (binary break). `IDisposable` is R28's.
**Tests.** Inside, `GetStateVector` is `InterpolateAt` bit for bit: both integrators, covariance, every inertial frame, SSB. On every path `Maneuvers` equals the maneuver properties bit for bit, unchanged by a rerun; fuel against Tsiolkovsky. API test: no public mutator. #527's PR runs the suite with Validation and the runner, naming any read outside.

**Results, in detail.** None. Inside the stored states, GetStateVector returns InterpolateAt's state bit for bit (PropagationSolution.cs:357-369 and :370-388). Measured 0 over 2000 reads, with and without covariance (work/r08r09-rev/readcost). PropagationSolution.Maneuvers equals the maneuver properties bit for bit. GetInitialFuel returns GetTotalFuel's value. #527's own PR turns reads outside the stored states into ArgumentOutOfRangeException; today they extrapolate, 5.842e14 m at +12 h (work/r08r09/r09). That PR runs the full suite with Validation and the conformance report, and names any test or search that reads outside.

**Depends on.** D1, D2, R08, R28. **C++ change.** no.

**Pull requests.** PR 1: #527 alone (spacecraft reads throw outside the stored states). PR 2: readers and run records, after R08's re-arm and tested against #575's fix. It holds GetStateVector as the one reader, InterpolateAt obsolete, PropagationSolution.Maneuvers, SpacecraftSummary built on it, GetInitialFuel and GetRemainingFuel, and the internal mutators of PropagationSolution and PropagationSegment.

**Open choices.**

1. **InterpolateAt.** Recommendation: IOA0001-obsolete and EditorBrowsable(Never), unchanged until 12.0, in favour of GetStateVector: one reader, one rule. In covariance mode GetStateVector also computes P through one shortened RK7(8) step with the variational equations. Measured on a point-mass Earth: 2000 reads take 17.5 ms through InterpolateAt and 68.6 to 70.0 ms through GetStateVector (work/r08r09-rev/readcost). PR 2 measures this on a geopotential case. A state-only read can be added later without a break. Alternative: Keep InterpolateAt as the documented state-only reader, so covariance-mode readers keep its cost, and name its frame and observer in its doc.
2. **Where the per-run report lives.** Recommendation: PropagationSolution.Maneuvers, a list of sealed ExecutedManeuver records. The solution is already the result of a run. Scenario's SpacecraftSummary (ScenarioSummary.cs) is built from it (DRY), with its members' values kept bit for bit. Alternative: Only a snapshot Spacecraft.ExecutedManeuvers per run, with DeltaV and fuel staying on the definitions.
3. **DeltaV, FuelBurned, ThrustWindow and ManeuverWindow on the maneuver definitions.** Recommendation: Keep them in 11.0.0 and document them as the values of the last propagation of that maneuver. They are right when each run builds its own maneuvers: the verifier kept 233.794, 433.210 and 605.458 m/s in s4. R08 forbids sharing a maneuver between plans. Alternative: IOA0001-obsolete in favour of the records, so that the run results have one home.
4. **A thrust window that starts before the propagation window (missed by the study). In s2 it starts at 2023-12-31T23:56:13 TDB for a window from 2024-01-01T00:00, from the [t - 0.666 dt] placement (ImpulseManeuver.cs:150)..** Recommendation: The record carries Epoch (the impulse, always inside the window) and the unclipped ThrustWindow, documented as the equivalent finite burn [t - 0.666 dt, t + 0.334 dt]. Clipping would change the reported burn duration and the OPM's MAN_DURATION, which is ThrustWindow.Length (ImpulseManeuver.cs:207). The split itself is #335. ToOpm's ordering and state are #658. Alternative: Clip ThrustWindow to the window start and add a StartsBeforeWindow flag.
5. **A trajectory computed outside the library, such as the runner's straight line for Analyze (ConformanceRunner/Solvers/StraightLineEncounter.cs:107-115).** Recommendation: Make the constructors and mutators internal in 11.0.0. The runner keeps them through InternalsVisibleTo (IO.Astrodynamics.csproj:30), and Analyze keeps accepting owner-less solutions (D4). A validated public factory can be added later without a break. Alternative: Keep them public and document that a solution built by hand has no observer or frame, so GetStateVector throws InvalidOperationException (PropagationSolution.cs:360-362).

**Candidate defects found on the way.**

- **Spacecraft reads extrapolate past the stored states without an error.** work/r08r09/r09 (re-run 2026-11-13): after a 4 h RK7(8) run, GetGeometricStateRelativeTo(end + 12 h) returns |r| = 5.842e14 m with no exception. For the same epoch, solution.InterpolateAt clamps to 8.090e6 m and GetStateVector throws ArgumentOutOfRangeException. verify/B/c1: 2.657e14 m at +12 h and 4.35e7 m at -1 h. Spacecraft.cs:658-670 interpolates the arc with no bound check. Listed in Known limitations. Tracked by #527.
- **Velocity-Verlet executes an impulse located in the last step while no state carries it.** Known limitations and #575: fuel, ExecutedManeuvers and the OPM show the burn, while the final state is before it. A PropagationSolution.Maneuvers record would list a burn that the solution's states do not carry. The records PR tests against #575's fix and does not snapshot today's behaviour. Tracked by #575.

### R10. A first spacecraft: constructor, components, instrument ids
**Gain** medium · **Cost** M · **Breaking** yes · **Results** unchanged · **C++** no · **Issues** #662, #586

**Today.** The propagated constructor requires a max mass and a `Clock` (Spacecraft.cs:133, :145); the kernel one defaults them (:186-195). Tanks, engines, instruments need a model, tanks and engines a serial; no tank starts empty (FuelTank.cs:19-22). `Add*Instrument` returns void (study/C-…/s1b, s3, s5).
```csharp
var sc = new Spacecraft(-1001, "Sat", 1000.0, 10000.0, new Clock("Sat_CLK", 65536), orbit);
var tank = new FuelTank("Tank", "Model", "SN-T", 3000.0, 3000.0); sc.AddFuelTank(tank);
var engine = new Engine("Main", "Model", "SN-E", 320.0, 5.0, tank); sc.AddEngine(engine);
sc.AddCircularInstrument(-1001001, "Cam", "Imager", 0.087, Vector3.VectorZ, Vector3.VectorX, Vector3.Zero);
var cam = sc.Instruments.Single(i => i.Name == "Cam");
```
**Proposed.** The NAIF id stays required (it names the kernels); the rest is optional.
```csharp
var sc = new Spacecraft(-1001, "Sat", 1000.0, orbit);  // max mass double.MaxValue, clock Sat_CLK
var tank = sc.AddFuelTank(new FuelTank("Tank", capacity: 3000.0, initialQuantity: 3000.0));
var engine = sc.AddEngine(new Engine("Main", isp: 320.0, fuelFlow: 5.0, tank));
var cam = sc.AddCircularInstrument("Cam", 0.087, Vector3.VectorZ, Vector3.VectorX); // -1001001
```
- A default instrument id: the next free in [id·1000−999, id·1000−1]; an explicit id outside it is accepted.
- A second instrument, tank or engine of a name throws. A tank may start empty. Same defaults in `Opm.ToSpacecraft`.
- Usings: D2 N7; `Propagate(window)`: D1/D4.

**Breaks and migration.** Old constructors and `Add*Instrument` become `[Obsolete]` IOA0001 forwarders; no overload collides. `AddFuelTank`/`AddEngine` return their argument (binary break). Tanks and engines compare by reference; a same-name tank, accepted today if model or serial differ, throws.

**Tests.**
- Old vs new: bit-for-bit propagations (both integrators, an impulse), same SCLK/FK/IK bytes.
- Default vs explicit id: same FOV windows; gftfov within 1.3e-6 s (spacecraft.md:466).
- Rejected names throw; an empty tank throws `InsufficientFuelException` on a burn.
- get-started compiles (#662).

**Results, in detail.** none. The defaults equal today's explicit values: max mass double.MaxValue (as the kernel constructor, Spacecraft.cs:194) and Clock(name + "_CLK", 65536). Old and new forms give bit-for-bit propagations and identical SCLK, FK and IK bytes. Only argument checks change: a second instrument, tank or engine of the same name on one spacecraft throws, and a tank may start empty. The rejection of the spacecraft's frame id as an instrument id lands in its own defect PR, before R10.

**Depends on.** D1, D2, D4. **C++ change.** no.

**Pull requests.** PR: spacecraft construction (Spacecraft constructor, FuelTank, Engine, Add*Instrument, Opm.ToSpacecraft, with the [Obsolete] IOA0001 forwarders and the get-started fix). It lands after the defect PRs on the instrument frame id and on the unattached tank, in the D2 migration lot so each user line changes once.

**Open choices.**

1. **NAIF id when not given.** Recommendation: Keep it required. It names the SPK, CK, FK and SCLK, and the FK frame id is NaifId*1000. A per-process allocated id would depend on creation order, so the written files would differ between runs and could collide with loaded kernels (the verifier's note). Alternative: A documented constant default (for example -1000), with a duplicate check in Scenario and in the conjunction analysis.
2. **Resolution of the default clock.** Recommendation: 65536 for the new optional clock of the propagated constructor and of Opm.ToSpacecraft. SpacecraftFrame.cs:42-46 and spacecraft.md:245 document 65 536 ticks per second for the CK, and 177 of the repository's 313 `new Clock(` calls use it (132 use 256). The kernel-backed constructor keeps its 256 (Spacecraft.cs:195), bit for bit. Alternative: One constant for both constructors. 65536 would change the SCLK text written from a kernel-backed spacecraft's default clock (MODULI 256 to 65536), and no read moves. 256 would be the study's 'same SCLK as Clock(name + "_CLK", 256)'.
3. **Equality of FuelTank and Engine once model and serial are optional.** Recommendation: Reference equality, with one name per tank and per engine on a spacecraft. They are mutable entities: Quantity changes in a burn. Value equality on (Name, null, null) would make two unnamed-model tanks equal. AddFuelTank and AddEngine reject a second tank or engine of the same name; the migration says that two tanks of one name, accepted today when model or serial differ, now throw. Alternative: Keep value equality on (Name, Model, SerialNumber), with no break, and accept that two tanks of one name with no model compare equal everywhere.
4. **Strictness of the instrument id rule.** Recommendation: Default to the next free id in [id*1000 - 999, id*1000 - 1]. Accept an explicit id outside that range, since mission kernels use them. Reject only what breaks SPICE: the spacecraft's frame id NaifId*1000 (s6b, its own defect PR), a duplicate id (already rejected, Instrument.cs:291-309) and a duplicate name on the spacecraft (both FKs assign FRAME_<SC>_<INSTRUMENT>, InstrumentFrameTemplate.tf:3, and the later assignment wins). The spacecraft's own id worked in s5, so it is not rejected (verifier's correction). Alternative: Enforce the NAIF range for every id. That is simpler to state, but it breaks users of mission kernels whose ids lie outside the convention.

**Candidate defects found on the way.**

- **An instrument accepts its spacecraft's frame id: the written FK redefines the spacecraft frame and the CK can no longer be read.** Instrument.cs:102 checks only naifId < 0. Re-measured in work/r10r14-rev: AddCircularInstrument(-1001000, ...) on spacecraft -1001 is accepted. In study/C-spacecraft-maneuvers-attitudes/s6b, id -1401000 (= NaifId*1000, SpacecraftFrame.cs:72) gives an instrument FK (InstrumentFrameTemplate.tf: FRAME_<id>_NAME, NAIF_BODY_CODE) that redefines KSat_FRAME. The twin read then fails with 'defined relative to itself'. With -1401001 it works and the attitude difference is 0 (the verifier re-ran it). Fix: reject that id in its own PR, before R10. No issue yet.
- **AddEngine accepts an engine whose tank is a different, value-equal instance not on the spacecraft; its first burn throws NullReferenceException.** Measured in work/r10r14-rev/Program.cs. Spacecraft -1002 holds tank t1 ("Tank", "Model", "SN-T", 3000, 3000). An engine built on t2, a new FuelTank with the same values that was never added, passes AddEngine, because FuelTanks.Contains (Spacecraft.cs:296) uses the value equality of FuelTank.cs:47-52. t2.Spacecraft stays null. The first impulse (ApogeeHeightManeuver, RK7(8), 2 h) throws NullReferenceException at Engine.Ignite, which reads FuelTank.Spacecraft.GetTotalMass() (Engine.cs:52). Fix in its own PR: AddEngine checks the tank by reference. #586 covers a tank shared by two spacecraft, not this one. No issue yet.
- **A Clock given to two spacecraft is attached to the last one: the first spacecraft's SCLK is written with the second's id.** Re-measured in work/r10r14-rev/Program.cs. One Clock("Shared", 65536) was passed to Spacecraft -1101 (A) and then to -1102 (B). A.Clock.Spacecraft is B, and A.Clock.WriteAsync writes 'SCLK_DATA_TYPE_1102'. Clock.AttachSpacecraft overwrites without a check (Clock.cs:29-32), and WriteAsync uses Spacecraft.NaifId (Clock.cs:39). A's CK, written against SCLK -1101, then has no clock kernel. This is the pattern of #586, which covers tanks and engines, not clocks. No issue yet.
- **The spacecraft frame id NaifId*1000 overflows int without a check for |NaifId| > 2147483.** Re-measured in work/r10r14-rev. new Spacecraft(-3000000, ...) gets Frame.Id = 1294967296, a positive id, from an unchecked int product (SpacecraftFrame.cs:72). The propagated constructor checks only naifId < 0 (Spacecraft.cs:139). The FK then declares a positive frame id for a spacecraft. Instrument ids by the NAIF convention (id*1000 - n) overflow the same way. No issue yet.
- **#662's row for docs/get-started.md:112 is incomplete: after id -> naifId the block still fails on fuelTank and engine.** #662 lists only `Spacecraft(id: …)` -> `naifId` for get-started.md:112. In study/C-spacecraft-maneuvers-attitudes/s1b, that fix still fails with CS1739 'fuelTank'. get-started.md:111-117 also passes engine, and it lacks maximumOperatingMass and clock (CS7036). Add the row to #662. Tracked by #662.

### R11. Input validation: steps, window bounds, angle units
**Gain** medium · **Cost** M · **Breaking** yes · **Results** unchanged on existing signatures; options grid ends at the window end · **C++** no · **Issues** #626, #598, #563

**Today.** RK7(8): a zero step gives 0 states, −60 s 1 state; VV throws List's "capacity" (study/B-propagation/s04). A 4200 s step on 2 h ends at 02:20 (PropagatorBase.cs:389). Window(t, −1 h) ends before it starts; reversed ends are swapped (Window.cs:25-28). Incidence 90 (degrees) gives the whole window (study/D-window-searches-geometry/s4).
```csharp
sc.Propagate(window, bodies, new RK78Integrator(), false, false, TimeSpan.Zero); // 0 states
new Window(t, TimeSpan.FromHours(-1)); // Length -01:00:00
new Window(t.AddHours(6), t); // swapped
site.FindWindowsOnIlluminationConstraint(twoDays, IlluminationAngle.Incidence, RelationnalOperator.Lower,
    90.0, 0.0, Aberration.LTS, TimeSpan.FromMinutes(10), sun); // whole window
```
**Proposed.** A step ≤ 0 throws on every propagation path. The options path outputs start + i·ΔT inside the window, then the end; legacy keeps round(L/ΔT). Window throws on reversed ends or a negative length. Window.Sample(step, includeEnd = false): start + i·step (Time.Add); no library grid moves. With R15 a threshold outside its range throws: illumination [0, π]; declination, latitude [−π/2, π/2]; RA [0, 2π); geodetic longitude (−π, π].
```csharp
sc.Propagate(window, PropagationOptions.Default with { OutputStep = TimeSpan.FromSeconds(4200) }); // last state at the end
var grid = window.Sample(TimeSpan.FromSeconds(30), includeEnd: true);
site.FindIlluminationWindows(twoDays, IlluminationAngle.Incidence, RelationalOperator.LessThan, 90.0, sun,
    WindowSearchOptions.Default with { Step = TimeSpan.FromMinutes(10) }); // throws: [0, π]
```
**Breaks and migration.** Order the ends; use Constants.Deg2Rad. A negative hold throws in the maneuver constructor, not mid-run (Attitude.cs:42). Options path: SPK, CK and attitudes end at the window end. Mixed scales: D3; zero vectors: R26.
**Tests.** Bad steps throw on every entry point. Options vs legacy on one window (both integrators, Earth, Moon, SSB, maneuvers): bit for bit up to the step clipped at the end (RK78Integrator.cs:309), end vs Kepler, last SPK record vs spkezr. Sample across 2016-12-31. Angles at and past each bound, both paths.

**Results, in detail.** None on any existing signature: the legacy grid round(L/ΔT)·ΔT stays an internal setting, bit for bit. The step check only rejects calls that returned 0 states (RK7(8), 0 s), 1 state (RK7(8), −60 s) or threw List's 'capacity' exception (Velocity-Verlet). On the PropagationOptions path, a step that does not divide the window ends the outputs at the window end, not up to ΔT/2 past it: the number of outputs, the last state, the last SPK record, the CK and the attitude record at the end change, and no event past the end fires. Integrator steps up to the one clipped at the window end are the legacy run's on the same window (RK78Integrator.cs:309-310, :407; VVIntegrator.cs:81), so the outputs before that step are bit for bit; the end state is compared with Kepler at twice the worst of Linux, Windows and macOS arm64. An angle threshold outside its range throws; before, such a call returned the whole window or nothing.

**Depends on.** D1, D2, D3, D4, R06, R15. **C++ change.** no.

**Pull requests.** PR 1 (a defect, alone, first): a step <= 0 throws on every propagation path. PR 2: the Window constructor checks, the maneuver hold check and Window.Sample, together with R16. The options output grid lands in R06's PropagationOptions PR, the angle ranges in R15's search PR. #626, #598, #563 and the two defects below keep their own PRs.

**Open choices.**

1. **Last output on the options path when the step does not divide the window.** Recommendation: Output start + i·ΔT inside the window, then the window end. Nothing is read past the window (#626, #527), the user does no arithmetic, and a step longer than the window is well defined (start and end), so only a step <= 0 throws. Alternative: Keep round(L/ΔT)·ΔT on the options path too. One grid rule and the legacy results, but the documented overshoot of up to ΔT/2 past the window (PropagatorBase.cs:798-799) stays, and with it #626's frozen caches.
2. **Window constructors with reversed ends or a negative length.** Recommendation: Throw: ArgumentException for reversed ends, ArgumentOutOfRangeException for a negative length, zero length accepted. An argument-order mistake then shows up at once; today only the occultation and FOV searches reject an end before the start (OccultationFinder.cs:126-131), and only for the TimeSpan constructor's negative length, since reversed ends are swapped (Window.cs:25-28). The library builds TimeSystem.Window at 28 sites (33 'new Window(' hits, 5 of them SpiceAPI's DTO); a negative maneuver hold reaches two of them mid-propagation (Attitude.cs:42, ImpulseManeuver.cs:151), so the maneuver constructor checks the hold first (Maneuver.cs:118-124 does not). The CLI's windows (GeometryFinderCommand.cs:46, Helpers.cs:209) then report an end-first window as an error. The suite run with the throw lists any other site that relied on the swap. Alternative: Normalise: swap reversed ends, as today, and treat Window(start, negative length) the same way. Nothing breaks, but order mistakes stay silent.
3. **Angle-unit guards on coordinate and illumination thresholds.** Recommendation: Throw ArgumentOutOfRangeException with a radian hint outside each coordinate's own range, the bounds accepted, for the comparison operators only (the extremes ignore the value): illumination angles [0, π]; declination and latitude [−π/2, π/2]; right ascension [0, 2π) (CSPICE recrad, Equatorial.cs:31-33); geodetic longitude (−π, π]. Planetographic longitude stays unguarded until its two paths agree (defect below): CSPICE gives [0, 2π), the managed path (−π, π]. Distances are not checked. Land it in R15's PR. Alternative: No guard; document radians only. Nothing breaks, and the gain is low.
4. **Window with ends in two time scales.** Recommendation: Keep each end's scale, as given. D3 makes Window equality that of its ends on the TDB tick, and R15 states the scale of search results. A conversion in the constructor would change the EndDate the caller gave. Alternative: Convert the end to the start's scale in the constructor (the verifier's suggestion). One scale per window, but the end moves by the round trip of the conversion (new Window(u, u.ToTDB().AddHours(1)) at 2024-03-01 has Length 00:59:59.9999993 today, design/work/r11r16-rev/b).

**Candidate defects found on the way.**

- **A propagation step <= 0 is not validated: RK7(8) returns 0 or 1 state with no error, Velocity-Verlet throws List's 'capacity' ArgumentOutOfRangeException.** study/B-propagation/s04, re-run by the verifier and in design/work/r11r16-rev/a: deltaT 0 under RK78Integrator gives 0 states; −60 s gives 1 state; under VV, −60 s throws 'Non-negative number required. (Parameter 'capacity')'. Nothing checks deltaT in PropagatorBase.cs:88-110 or CentralBodyPropagator.cs:142-150. Tracked elsewhere: TLEPropagator's division by zero below 1 s is #598, the overshoot freezing the caches is #626, the drift of the ReadEphemeris window reads is #563. No issue yet.
- **CelestialItem.GetEphemeris(window, …) reads past its window across a leap second.** CelestialItem.cs:284 counts floor(Length/step) + 1 samples with Length = EndDate − StartDate, which counts the leap second (#508), and :288 places them by Time + TimeSpan, which does not. Measured (design/work/r11r16-rev/d): UTC window 2016-12-31T23:59:00Z to 2017-01-01T00:01:00Z, Length 00:02:01, 1 s step: 122 states, the last at 2017-01-01T00:01:01Z, 1 s past the end. Not covered by #563 (SpiceAPI window reads) or #598 (TLEPropagator). No issue yet.
- **The planetographic coordinate searches give two longitude ranges: [0, 2π) on the CSPICE path, (−π, π] on the managed path.** The managed path computes CoordinateSystem.Planetographic with ToPlanetocentric (CelestialItem.cs:1067-1079, Site.cs:395-407); the CSPICE path, taken when a kernel spacecraft takes part (CelestialItem.cs:1009-1010), passes PLANETOGRAPHIC to gfposc. Measured (design/work/r11r16-rev/e): the Moon from the Earth in ITRF93 over 10 days from 2024-03-01, Longitude Greater 4.0 rad: managed 0 windows, SpiceAPI.FindWindowsOnCoordinateConstraint 10; Greater 1.0: 10 against 11 windows, the second starting at 16:23:27 against 04:00:35. The latitude (planetocentric against planetographic) was not compared. No issue yet.

### R12. Numerical propagation of a TLE spacecraft; TLEPropagator
**Gain** high · **Cost** M · **Breaking** no · **Results** unchanged where a call runs today · **C++** no · **Issues** #457, #598, #546

**Today.** A spacecraft from a TLE or `tle.ToOsculating(t)` (TEME) throws "must be defined in inertial frame" on every path and in `astro propagate -t` (PropagatorBase.cs:948-960; study/B-propagation/s05, s08), against Spacecraft.cs:548. A TLE spacecraft reads SGP4, never its stored states (Spacecraft.cs:646-650, :781).
```csharp
var sc = new Spacecraft(-1001, "ISS", 1000, 2000, new Clock("c1", 65536), tle);
sc.Propagate(window, bodies, new RK78Integrator(), false, false, step); // ArgumentException
var sv = tle.ToOsculating(t0).ToFrame(Frame.ICRF).ToStateVector(); // workaround
new Spacecraft(-1002, "ISS", 1000, 2000, new Clock("c2", 65536), sv)
    .Propagate(window, bodies, new RK78Integrator(), false, false, step);
new TLEPropagator(window, sc, TimeSpan.FromMilliseconds(500)); // DivideByZeroException
```
**Proposed.** A TEME orbit is rotated once to ICRF at its epoch before the propagator is built (a TLE: SGP4 at the window start); it integrates in ICRF, never in TEME (#529, #535), so its solution is in ICRF. A TLE spacecraft reads its stored states after `Propagate` or `SimulateAsync`, SGP4 otherwise. `TlePropagator` stays the SGP4 sampler. `astro propagate -t` runs, its Earth set by R29's options (Helpers.cs:186-190).
```csharp
sc.Propagate(window); // RK7(8), the TLE's point-mass Earth (D4)
sc.Propagate(window, PropagationOptions.Default with { Geopotential = GeopotentialModelParameters.Egm2008(10) }); // R06
```
Docs: gap to SGP4 (ISS, RK7(8) 1e-9, no drag): point mass 53.7 km at 1 h; EGM2008 10×10 0.56 km at 1 h, 0.79 km at 6 h, 7.2 km at 24 h. B* is not used.
**Breaks and migration.** None: the starts threw; an unpropagated or SGP4-sampled TLE reads SGP4. Replacing the states is R08's, the name D2's.
**Tests.** TLE and TEME starts bit for bit equal to the workaround: both integrators, drag, SRP, maneuvers, covariance, a TEME state about the SSB. After `Propagate`, reads, SPK and the conjunction `ILocalizable` path equal the solution, #527 past it. ScenarioTests.cs:300 kept (SGP4); new SimulateAsync, CLI `-t` tests; goldens unchanged.

**Results, in detail.** None for any call that runs today: TLE and TEME starts threw on every path (re-run in design/work/r12r21-rev/r12 and vb; astro propagate -t re-run: unhandled ArgumentException at PropagatorBase.cs:958). New paths: a TLE or TEME start runs, bit for bit equal to the ToFrame(ICRF) workaround, with its solution in ICRF. A TLE spacecraft reads its stored states after Spacecraft.Propagate or Scenario.SimulateAsync (reads, SPK and the conjunction ILocalizable path, whose doc at conjunction-assessment.md:152 already says 'the stored states of a spacecraft that was already propagated'); an unpropagated or TlePropagator-sampled TLE spacecraft reads SGP4 as today (0 m, verify/B/c3). Doc figures re-measured in design/work/r12r21-rev/r12 (ISS TLE, RK7(8) 1e-9, no drag, gap to SGP4): point mass 53.663 km at 1 h, 275.692 km at 6 h, 1118.386 km at 24 h; EGM2008 10x10 0.555, 0.788, 7.217 km.

**Depends on.** D1, D2, D4, R06, R08, R29. **C++ change.** no.

**Pull requests.** PR 1: TEME and TLE starts on every propagation path (rotation to ICRF before the propagator is built) and the read rule of a TLE spacecraft (Spacecraft.cs:646-650 and :781); fixes the filed defect below. PR 2: #598, #546 and the TlePropagator velocity defect, each its own issue, beside R08's replace rule. The TLEPropagator -> TlePropagator rename lands in D2's rename PR; the CLI options in R29.

**Open choices.**

1. **What Spacecraft.Propagate does for a TLE spacecraft with no options.** Recommendation: Numerical integration from the SGP4 osculating state at the window start, rotated once to ICRF, as Spacecraft.cs:548 already says. A propagation means the force model of the options for every spacecraft; SGP4 stays what an unpropagated TLE spacecraft reads. The docs state that SGP4 and the numerical model diverge within hours (figures above) and that B* is not used. Alternative: Propagate runs SGP4 for a TLE spacecraft. It matches what some newcomers expect, but one call then has two meanings by the type of the initial orbit, and every force setting has to throw for a TLE (D1 rule 2).
2. **Where SGP4 sampling with stored states lives (the SPK of an SGP4 spacecraft).** Recommendation: Keep TlePropagator as the one SGP4 sampler, fixed by #598, #546 and the velocity defect, its states replaced per R08, reads left on exact SGP4. Not an integrator option: SGP4 is an analytical theory with no force model (the verifier calls 'Integrator = Sgp4Settings' a category error), and D1 rule 5 keeps IntegratorOptions closed to RK7(8) and Velocity-Verlet. Alternative: A nullable PropagationOptions.Model (null = Numerical, Sgp4) that stores ICRF samples at OutputStep, every force or integrator setting throwing under Sgp4; TlePropagator becomes [Obsolete] (IOA0001) until 12.0.0. One entry point, at the cost of an options record where most members cannot apply.
3. **Force model of a TLE start with no options.** Recommendation: D4's rule, as for any spacecraft: the central body with its observer's models. The TLE's Earth is a point mass (TLE.cs:176), so the default is 53.7 km from SGP4 after 1 h; the reference page shows the R06 Geopotential line (0.56 km). R06 keeps Geopotential null = the body's, so one rule holds for every start. Alternative: A TLE start takes EGM2008 10x10 by default when its observer has no model (0.56 km at 1 h, 7.2 km at 24 h). More automatic, but a second default rule that depends on the type of the initial orbit.

**Candidate defects found on the way.**

- **A TLE or TEME initial orbit is rejected by every propagation path, though Spacecraft.cs:548 and :620 say the propagator extracts the state; astro propagate -t ends in an unhandled ArgumentException.** PropagatorBase.cs:948-960 lists 7 frames without TEME. Spacecraft(TLE).Propagate and Spacecraft(tle.ToOsculating(t)).Propagate throw 'Spacecraft initial orbital parameters must be defined in inertial frame' (design/work/r12r21-rev/r12, study/B-propagation/s05, study/F-tle-omm-opm-orbital-parameters/s2-tle-states). astro propagate -t re-run on 2026-11-13: unhandled ArgumentException at PropagatorBase.cs:958 through Scenario.cs:177. ScenarioTests.cs:300 keeps its call commented. Recorded in #457 as found and not filed. The fix converts to ICRF before the propagator is built (integrating in TEME would add fictitious accelerations, #529/#535) and changes the TLE branches of Spacecraft.GetGeometricStateRelativeTo (Spacecraft.cs:646-650) and InterpolatedStatesWindow (:781), which read SGP4 before the stored states. No issue yet.
- **TLEPropagator's PropagationSolution carries zero accelerations: InterpolateAt's velocity is 4.39 m/s off SGP4 between samples.** TLEPropagator.cs:57-61 builds each AcceptedStep with Vector3.Zero accelerations; PropagationSegment.HermiteInterpolate (PropagationSegment.cs:157-159) uses them for the velocity only. ISS TLE, 60 s step, against tle.ToOsculating: velocity 4.3894 m/s at +30 s, 4.3874 m/s at +630 s; position 0.371 m and 0.370 m, which is the cubic Hermite's own error at 60 s and not caused by the zero accelerations (design/work/r12r21-rev/tp). Same file as #598/#546, own PR. No issue yet.
- **TLEPropagator counts samples in whole seconds: a sub-second step divides by zero, a non-multiple window loses its end.** TLEPropagator.cs:33; TimeSpan.FromMilliseconds(500) -> DivideByZeroException (design/work/r12r21-rev/r12). Tracked by #598.
- **TLEPropagator keeps its attitudes in a private dictionary nothing reads; the CK cannot be written.** TLEPropagator.cs:16, :40, :46; spacecraft.md:294. Tracked by #546.

### R13. Maneuver and attitude constructors
**Gain** high · **Cost** M · **Breaking** yes · **Results** unchanged if the engine is on the propagated spacecraft; bad radii throw · **C++** no · **Issues** #586, #573

**Today.** Hohmann plus nadir: 12 arguments besides targets, all inferable (study/C/s2). An attitude uses its engine only to reach the spacecraft (Maneuver.cs:105): a thrusterless satellite invents one (s3 F). `TargetApogee` is a radius: 1000e3 m passes, 5378 km underground (s7).
```csharp
var tank = new FuelTank("x", "x", "x", 1e-9, 1e-9); sc.AddFuelTank(tank);
var engine = new Engine("x", "x", "x", 1, 1, tank); sc.AddEngine(engine);
var none = new Time(DateTime.MinValue, TimeFrame.TDBFrame);
var lvlh = TriadAttitude.CreateLVLH(earth, none, TimeSpan.Zero, engine);
var raise = new ApogeeHeightManeuver(earth, none, TimeSpan.Zero, 1000e3, engine);
```
**Proposed.** D1 rule 8: target, then optional `engine`, `minEpoch`, `holdDuration`, `maneuverCenter`. #586's bind, before the detectors are built, infers a null: the only engine (0 or 2+: ArgumentException); the initial orbit's observer if a CelestialBody, else required (SSB). Attitudes take no engine; `bodyAxis` (default Front) resolves on the bound spacecraft. Radii > 0 and finite; an apogee ≥ the equatorial radius of a CelestialBody centre (no perigee check: deorbit).
```csharp
var raise = new ApogeeHeightManeuver(rGeo);
var circ = new PerigeeHeightManeuver(rGeo, holdDuration: TimeSpan.FromMinutes(10));
var lvlh = TriadAttitude.CreateLvlh();
var down = new NadirAttitude(bodyAxis: BodyAxis.Down);
```
**Breaks and migration.** Old constructors forward (IOA0001, compiled); `CreateLVLH` → `CreateLvlh` is D2's hard case rename. Null `minEpoch` = default(Time), the docs' sentinel (D3). An old attitude drops its engine (`Engine` null). `TryExecute` per R08. Forwarded: `TargetApogee`, `TargetPerigeeHeight` (→ `…Radius`), `PrimaryAttitudeTarget`, `ManeuverHoldDuration` (→ `HoldDuration`), `MinimumEpoch` (→ `MinEpoch`).
**Tests.** Each type and form (targetOrbit, Instrument), old vs new, bit for bit: both integrators, every activation path, SSB, non-ICRF. Inference = explicit: 0, 1, 2 engines; SSB, no centre. No engine = dummy-engine record. Six axes, custom triads: closed form, pxform on the CK.

**Results, in detail.** None for existing calls whose engine is on the propagated spacecraft: the [Obsolete] constructors forward bit for bit, proven old vs new over both integrators, every activation path, SSB mode, a non-ICRF start and the targetOrbit, ILocalizable and Instrument forms. Results move only where an old attitude was given an engine on no spacecraft or on another spacecraft (s3 B, s3 G). Dropping that engine binds the attitude to the propagated spacecraft, which is #586's fix, measured in #586's PR. New errors: a target radius <= 0, NaN or infinite, and an apogee radius below the equatorial radius of a CelestialBody centre. Today both are accepted (s7 ends in InsufficientFuelException). None of the 84 maneuver constructor calls in the repository's tests is affected (all are at least 6.5e6 m about the Earth or above the Moon's radius). A perigee inside the body stays legal (deorbit). The custom-axes correction of CreateLVLH/CreateProgradeWithSunTracking lands first in its own defect PR, measured against the closed form there.

**Depends on.** D1, D2, D3, R08, R03. **C++ change.** no.

**Pull requests.** PR R13, after R08's PR (#586 bind, TryExecute, SetManeuvers) and the factory-axes defect PR. It covers the constructors with optional named parameters, BodyAxis, the radius property names, the IOA0001 forwarders, and the docs (mission-design.md:149-183, attitudes.md:146). The maneuvers.md:98 wording is fixed earlier by its own docs PR.

**Open choices.**

1. **What Engine and ManeuverCenter return when they were inferred.** Recommendation: Before a propagation, the given value (null when inferred). After the bind, the resolved value, held as run state like ThrustWindow and FuelBurned and rebound by the next propagation. A caller can then read what the run used and pin it (D1 rule 10). Alternative: Engine and ManeuverCenter keep the given value, and the resolved values go on new members (ResolvedEngine, ResolvedCenter) or on the solution.
2. **Type of the body direction of an attitude.** Recommendation: Both. A BodyAxis enum {Front, Back, Right, Left, Up, Down} is the default form, resolved on the bound spacecraft's BodyFront/BodyRight/BodyUp. A Vector3 stays a literal body-frame vector for off-axis directions. TRIAD gets one BodyAxis shape and one Vector3 shape, each with IAttitudeTarget targets. Neither collides with the old TRIAD constructors, which start with Time or CelestialItem (public-api.txt:1915-1918). Alternative: The BodyAxis enum only, plus the Instrument forms for off-axis directions. This means fewer overloads, but an arbitrary body vector is then impossible.
3. **TriadAttitude.PrimaryTarget/SecondaryTarget.** Recommendation: Retype them to IAttitudeTarget, never null. An ILocalizable is wrapped in CelestialAttitudeTarget(x, Aberration.None), which uses the same formula (TriadAttitude.cs:286-290 against CelestialAttitudeTarget.GetDirection), and the forwarder keeps the centre taken from the primary target (TriadAttitude.cs:252), so the quaternion is the same bit for bit. PrimaryAttitudeTarget/SecondaryAttitudeTarget (shipped in 10.1.0) become IOA0001 forwarders. Readers of PrimaryTarget get a hard source break; they migrate with (t.PrimaryTarget as CelestialAttitudeTarget)?.Target. Alternative: Keep PrimaryTarget as ILocalizable and always fill PrimaryAttitudeTarget. Nothing breaks, but one concept keeps two properties.
4. **Names of ApogeeHeightManeuver and PerigeeHeightManeuver.** Recommendation: Keep the class names, which follow the usual name of the burn. Rename the properties to TargetApogeeRadius and TargetPerigeeRadius (the old names are forwarded; all shipped in 10.1.0). Check the values, with a message that says 'a radius from the centre of EARTH; for a height h pass R + h'. Alternative: Rename the classes ApogeeRadiusManeuver/PerigeeRadiusManeuver, or add FromHeight(h) factories. This breaks every user for little gain once the check exists.
5. **The engine passed to an [Obsolete] attitude constructor.** Recommendation: Drop it: an attitude uses no engine and acts on the bound spacecraft. A null engine is then accepted where it throws today (Maneuver.cs:123). Results are bit for bit when the engine is on the propagated spacecraft. A foreign or unattached engine (s3 G, s3 B) no longer redirects the record or falls back to the static axes, which is #586's fix. Alternative: Keep the engine and apply #586's check, so that an engine not on the propagated spacecraft throws at bind. Old code then keeps its error path, but an attitude-only satellite still needs a tank.

**Candidate defects found on the way.**

- **TriadAttitude.CreateLVLH and CreateProgradeWithSunTracking use the static body axes, not the spacecraft's instance axes.** TriadAttitude.cs:228-229 and :247-248 pass the static Spacecraft.Down/Front/Up. docs/reference/attitudes.md:146 and the .NET CLAUDE.md say every attitude reads the per-instance axes. Re-measured on 2026-11-13, main (work/r13-rev, from work/r13/Program.cs), with bodyFront +X, bodyRight -Y, bodyUp +Z and CreateLVLH: at t0+30 min, angle(BodyFront, v) = 90.000 deg and angle(BodyDown, nadir) = 0.000 deg. study/C/s3 I gives the same. Fix: resolve the axes at execution through the spacecraft, as GetBodyFront does (Maneuver.cs:105). Results change only for spacecraft with custom axes. Measure against the closed form of LVLH and with pxform on the written CK. No open issue covers it (open-issues.md). No issue yet.
- **docs/tutorials/maneuvers.md:98 says ApogeeHeightManeuver raises the apogee 'to a target altitude', but the argument is a radius from the centre.** ApogeeHeightManeuver.cs:20-24 stores apogeeRadius in TargetApogee, and Execute uses it as a radius (:37). In s7, 1000e3 m (an altitude, as the tutorial reads) is accepted as a radius 5378 km under the surface (re-run: TargetApogee=1000000, ManeuverCenter=EARTH) and ends in InsufficientFuelException. R + 1000 km gives the intended 400 x 1000 km orbit (160.8 m/s). A one-line docs PR, independent of 11.0.0. No issue yet.

### R14. Writing a spacecraft's kernels in one call
**Gain** high · **Cost** M · **Breaking** no · **Results** unchanged (the manual sequence's bytes) · **C++** no · **Issues** #545, #546, #655

**Today.** 6 writers in a fixed order, 4 async, 2 sync. The CK needs the SCLK loaded (same bytes without the FK); reading it needs the FK. The SPK and CK stay unloaded (#545). Only `CosmographiaExporter` has the sequence, in its layout, loading neither (CosmographiaExporter.cs:315-334; study/C-…/s6-kernels).
```csharp
await sc.Clock.WriteAsync(sclk); SpiceAPI.Instance.LoadKernels(sclk);
await sc.Frame.WriteAsync(fk);   SpiceAPI.Instance.LoadKernels(fk);
foreach (var i in sc.Instruments) { await i.WriteFrameAsync(F(i, "tf")); await i.WriteKernelAsync(F(i, "ti")); }
sc.WriteEphemeris(spk); sc.WriteOrientation(ck);   // CK: throws without the SCLK
SpiceAPI.Instance.LoadKernels(spk); SpiceAPI.Instance.LoadKernels(ck);   // #545
```
**Proposed.** One method writes the SCLK, FK, instrument FKs and IKs, SPK and CK into a directory and loads them; `Dispose` unloads them.
```csharp
using SpacecraftKernels k = await sc.WriteKernelsAsync(new DirectoryInfo("out"), cancellationToken);
// out/Sat.tsc, Sat.tf, Sat_Cam.tf, Sat_Cam.ti, Sat.bsp, Sat.bc, loaded
var twin = new Spacecraft(-1001, "Sat", t);   // reads them
```
- Checks before any write throw for a kernel-backed spacecraft, one never propagated or from `TLEPropagator` (#546), a centre without a NAIF name (#655), a directory inside a loaded kernel path (SpiceAPI.cs:267-283).
- A loaded target is unloaded before the rewrite and reloaded (#545 case B); it stays loaded after `Dispose`. A failure unloads what the call loaded.
- `CosmographiaExporter` calls the same sequence; paths, files and pool state unchanged.

**Breaks and migration.** None (additive); the old writers are unchanged.

**Tests.**
- Files byte for byte those of the manual sequence: impulses, attitudes, two instruments, both integrators.
- spkezr and pxform on them match the managed reads within spacecraft.md's accuracy table.
- `Dispose` unloads what the call loaded, not a prior load; a rewrite reads the new content.
- A refused call writes and loads nothing.
- Cosmographia files unchanged byte for byte; KernelReloadGuard and KernelShadowing hold.

**Results, in detail.** none. The files are byte for byte those of today's manual sequence, the existing writers are unchanged, and CosmographiaExporter's files and kernel-pool state are unchanged. The new method alone leaves its files loaded until Dispose, which unloads only what the call loaded.

**Depends on.** D1, D2. **C++ change.** no.

**Pull requests.** PR: Spacecraft.WriteKernelsAsync and SpacecraftKernels. CosmographiaExporter moves onto the same internal sequence.

**Open choices.**

1. **Load the written kernels.** Recommendation: Load them, and return SpacecraftKernels : IDisposable. Its docs and IsLoaded say that the files are loaded. Dispose unloads what the call loaded, and the files stay. A kernel the library writes is written to be read, and #545 shows that the C++ layer meant to leave it loaded. The verifier warns about the side effect on the pool, so the handle makes the side effect explicit and scoped, and the KernelShadowing rule is checked in its tests. Alternative: A D1 options record, KernelWriteOptions { Load = true }, with Load = false for a caller who only exports the files. Or never load, and leave the caller the four LoadKernels calls.
2. **Layout and file names.** Recommendation: One directory with NAIF extensions: <name>.tsc, <name>.tf, <name>_<instrument>.tf and .ti, <name>.bsp, <name>.bc. The SpacecraftKernels properties (Clock, Frame, Ephemeris, Orientation, Instruments) give each path. Alternative: Cosmographia's layout (subfolders Clocks, Frames, Instruments, Ephemeris, Orientation; .spk and .ck), which CosmographiaExporter keeps either way.

**Candidate defects found on the way.**

- **A kernel-backed spacecraft names its frame <name>_FRAME whatever the loaded FK defines for id NaifId*1000: a twin with another name, or a mission FK, cannot be read.** Re-measured in work/r10r14-rev. A twin of -1001 built with the name SatK constructs, but GetOrientation throws InvalidOperationException 'SPICE gives no transformation from SatK_FRAME to J2000', with SPICE(UNKNOWNFRAME) on stdout (study/C-spacecraft-maneuvers-attitudes/s6-kernels: 'KSatK_FRAME was not recognized'). With the original name, |dr| is 3.03e-5 m (2.9e-5 m in s6) and the attitude difference is 0. The cause is SpacecraftFrame.cs:72: base(spacecraft.Name + "_FRAME", NaifId * 1000). The same failure hits any mission FK whose spacecraft frame has another name. The fix resolves the name for NaifId*1000 through frmnam_c. No frame-name proxy exists (the externs of SpiceAPI.cs:48-143), so it needs a C++ change for a technical reason (#667), in its own PR. No issue yet.
- **A spacecraft or instrument name with a space writes an FK that SPICE cannot load.** Re-measured in work/r10r14-rev. Spacecraft -1201 named "Demo Sat" gets FK line 3 'FRAME_DEMO SAT_FRAME   = -1201000', and LoadKernels throws InvalidOperationException after SPICE(BADVARASSIGN): 'A kernel variable was not properly formed on line 3'. The name goes into a kernel variable name (FrameTemplate.tf:3; InstrumentFrameTemplate.tf:3 does the same with '<SPACECRAFT>_<INSTRUMENT>'). The constructors check only null or empty (Instrument.cs:87 for instruments). Until this is fixed, WriteKernelsAsync rejects such names in its up-front checks. No issue yet.

### R15. Window searches: on the observer, one options record per kind
**Gain** high · **Cost** M · **Breaking** yes · **Results** old calls unchanged; new: same windows, window's scale · **C++** no · **Issues** #316, #489, #496, #652, #662

**Today.** `CelestialItem` searches hang on the target, `Site`'s occultation on the observer: `dss13.FindWindowsOnCoordinateConstraint(w, moon, …)` gives the Moon below −10° (study/D/s1). A 2 h step gives 1 window for 4 eclipses (s2). Step and `adjustValue` required; FOV's observer "Not used" (Instrument.cs:160); CSPICE labels TDB (s7).
```csharp
var up = moon.FindWindowsOnCoordinateConstraint(window, site, site.Frame, CoordinateSystem.RaDec, Coordinate.Declination,
    RelationnalOperator.Greater, 10 * Constants.Deg2Rad, 0.0, Aberration.LT, TimeSpan.FromMinutes(10));
var ecl = sun.FindWindowsOnOccultationConstraint(window, spacecraft, ShapeType.Ellipsoid, earth, ShapeType.Ellipsoid,
    OccultationType.Any, Aberration.None, TimeSpan.FromHours(2)); // 1 window
```
**Proposed.** Extensions on `ILocalizable`, receiver = observer; FOV on `Instrument`, illumination on `Site`. Three sealed records repeat `Step` (occultation null: period/16), `Aberration` (None), `ConvergenceTolerance`. Null shape: ellipsoid if radii; a point front body managed (#496). Site observer: managed. Windows in the start's scale.
```csharp
var o = WindowSearchOptions.Default with { Step = TimeSpan.FromMinutes(10) };
var up = site.FindHorizontalWindows(window, moon, HorizontalCoordinate.Elevation, RelationalOperator.GreaterThan, 10 * Constants.Deg2Rad, o);
var ecl = spacecraft.FindOccultationWindows(window, sun, earth); // step > period/2 throws
var f = GeometryFinder.FindWindows(window, t => g(t), RelationalOperator.LessThan, v, o);
```
**Breaks and migration.** Old methods, `ILocalizable`'s too, `[Obsolete]`, bit for bit to 12.0.0; receiver table; `Step` in their options throws. `new GeometryFinder()` obsolete; `EvaluateCondition` internal. `AdjustValue` off AbsoluteMin/Max throws.
**Tests.** New = old, roles swapped, bit for bit (all paths, participants); horizontal vs `GetHorizontalCoordinates`; period/16 and period/2 find a LEO day's 16 eclipses, +2 min throws; bad pairs throw up front; bounds' `ToTdb()` = today's tick, bar D3's round-trip misses.

**Results, in detail.** None on existing calls: the [Obsolete] paths keep their windows and TDB labels bit for bit. New methods give the old call's windows with roles swapped, bit for bit, on today's path (a Site observer stays managed). Labels are in the scale of the search window's start. ToTDB() of each bound is today's TDB tick, except at D3's TDB-UTC round-trip misses (about 6 in 5 million ticks, one tick each, D3's own issue), counted 1972-2100 and on a leap-second day. New defaults on the new path only: Aberration.None and the occultation step period/16. Measured (work/r15-rev, LEO a = 6800 km, period 01:33:00.5, 24 h): 16 eclipses at period/16 and at period/2, edges within 0.2 us of a 1 min step; 15 at period/2 + 2 min; 14 at 50 min.

**Depends on.** D1, D2, D3, D4. **C++ change.** no.

**Pull requests.** R15-1 (after D1's infrastructure PR and D2's RelationalOperator PR): WindowSearchOptions, FieldOfViewSearchOptions, the extended OccultationSearchOptions, the observer extension methods, labels in the window's scale, the occultation step and the half-period guard (occultation and FOV), the [Obsolete] forwarders. R15-2: FindHorizontalWindows, FindNightWindows, one internal CoordinateEvaluator that checks pairs before the search, static GeometryFinder.FindWindows (double and bool overloads) beside the obsolete instance members.

**Open choices.**

1. **Which object carries the search.** Recommendation: The observer. Extension methods on ILocalizable in one static class call the internal searches directly and keep today's CSPICE/managed choice on the swapped roles (CelestialItem: IsSpacecraft || observer.IsSpacecraft; Site: a spacecraft observer). FOV goes on Instrument, whose spacecraft is the observer. ILocalizable keeps its three members [Obsolete] until 12.0.0, so ILocalizable-typed calls still compile and implementers write nothing new. Alternative: A static WindowSearch.Find(window, condition record, options) with named Observer and Target. It is harder to find from `site.` in the IDE. It can become the internal layer later.
2. **Step of threshold and FOV searches, which D4 keeps required.** Recommendation: Step stays in the three records (D1 sibling rule: same name, type TimeSpan?, default null). Where no inference rule exists, null throws ArgumentException naming Step and 'the shortest window or gap to find' at the start of the call. Once the extremes contract (own issue) proves a default, null becomes inferred without a break. Alternative: A required positional TimeSpan step on the threshold and FOV methods, with a step-less overload later. Calls are shorter, but the siblings then disagree on Step.
3. **Elevation search.** Recommendation: One Site.FindHorizontalWindows with HorizontalCoordinate { Azimuth, Elevation, Range }. It maps to Declination, 2*pi - RightAscension (relation mirrored: GreaterThan becomes LessThan, LocalMax becomes LocalMin) and Range in site.Frame, through today's target.Find(site) path. That path is managed for every site: a Site is not a CelestialItem (CelestialItem.cs:1009). Measured: LRO above 10 deg from DSS-13 with a UTC window comes back labelled Z, the managed label. Alternative: Also a FindElevationWindows shortcut for the most common search: one more member for the same thing.
4. **Kernel site observing a kernel spacecraft.** Recommendation: Keep the managed path, so the new call equals today's correct call (lro.Find(dss13)) bit for bit. Today gfposc runs only for the inverted call (site as target): its ends differ by about 1.8 s with LT (02:27:28.77 TDB against 02:26:21.42Z = 02:27:30.60 TDB, work/r15-rev). Alternative: Route it to gfposc with the site's NAIF id as observer. Kernel sites would then match CSPICE, but the windows move by the gap between the two paths. That is a stated change of results, measured against gfposc.
5. **Where the half-period guard and the window-scale labels apply.** Recommendation: The new methods only. The [Obsolete] paths forward with legacy settings (no guard, TDB labels on the CSPICE and occultation paths), bit for bit (D1 rule 9, D4). An obsolete overload whose options set Step throws ArgumentException; their Aberration is ignored there, documented, because the positional argument decides. Alternative: Guard the old paths too, listed under Breaking. An old 2 h eclipse search then throws instead of returning 1 window for 4 eclipses.
6. **ConvergenceTolerance on the CSPICE paths.** Recommendation: D1 rule 2: the property becomes TimeSpan? on the three records, where null means 1 us (SPICE_GF_CNVTOL) on every path. A value set on a search that CSPICE runs throws ArgumentException naming ConvergenceTolerance. Today the value is silently ignored there (OccultationSearchOptions.cs:20-22). Reading the property becomes nullable, a source break listed under Breaking. No C++ change. Alternative: Wire gfstol_c into the native searches, set and restored under the SPICE lock. The property then stays a non-null TimeSpan and applies on every path. It is a C++ change built on three platforms (#667 allows it).
7. **Default aberration of the new searches.** Recommendation: Aberration.None, as CelestialAttitudeTarget and OccultationSearchOptions use. Each method's XML states that, against LT, this moves the edges of a site's Moon or planet search by the light-time effect. Alternative: Aberration.LT by default. It is closer to what an observer sees, but differs from every other default of the library.

**Candidate defects found on the way.**

- **Threshold window searches (GeometryFinder: distance, coordinate, illumination) lose short windows non-monotonically in the step: no extremes-then-crossings contract.** Missed by the study, found by the verifier; own issue (D4 rule 7). A LEO (a = 6800 km) seen from a custom site, elevation > 30 deg over 24 h: 2 passes (shortest 2 min 50 s) at steps of 1, 2 and 3 min, 1 at 4 and 5 min, 2 at 5.8125 min (verify/D/step/Program.cs). GeometryFinder.cs:18-21 requires step < shortest window. Occultation finds every window when the step is at most half the time between extremes (CelestialItem.cs:958-960): all 16 eclipses up to period/2 (work/r15-rev). The fix finds more windows, so goldens are re-measured. Only then can threshold searches infer a step. No issue yet.
- **Field-of-view search on a spacecraft with no attitude silently uses the identity attitude (body axes = ICRF).** Missed by the study, found by the verifier; own issue. study/D/s3 #1: an unpropagated spacecraft with no attitude ever set returns 5 windows of the Earth 'in view' of a camera along ICRF +Y. SpacecraftFrame.cs:94-96 returns Quaternion.Identity on an empty record, and FieldOfViewSearch does not check it, while WriteOrientation throws 'has no attitude ... it has not been propagated' (SpacecraftFrame.cs:439-440). Fix: InvalidOperationException with that message on both FOV paths. No issue yet.
- **Site illumination search has no observer: Emission and Phase are measured from the site itself, and the two paths disagree.** Found while drafting, re-run in work/r15-rev (2024-03-01, 1 day, step 10 min, Aberration.None, angle < 90 deg). Site.cs:427-455: the managed path calls IlluminationEmission(date, this) and IlluminationPhase(date, source, this); both return 0 for the site as its own observer, so a custom site gets the whole day for Emission and for Phase. The CSPICE path passes the site's own id as gfilum's observer: DSS-13 gets 0 windows (Emission) and 2 windows (Phase: 00:00-07:38:42.54 and 19:27:00.30-24:00 TDB). The new FindIlluminationWindows takes a named observer, required for Emission and Phase. Related to #316. No issue yet.
- **Managed CoordinateSystem.Planetographic computes planetocentric coordinates (CSPICE's LATITUDINAL), unlike the gfposc path.** Found while drafting, re-run in work/r15-rev. CelestialItem.cs:1067-1078 and Site.cs:395-406 evaluate stateVector.ToPlanetocentric() under Planetographic and offer Radius. The CSPICE path passes PLANETOGRAPHIC to gfposc: planetographic latitude, longitude positive west for prograde bodies other than the Earth, Moon and Sun, and ALTITUDE, not RADIUS (gfposc header, to be verified by S. Guillet). Sub-Moon latitude > 20 deg in IAU_EARTH, March 2024, step 6 h: the managed Planetographic window starts 2024-03-14T07:04:00.71 TDB, the Geodetic one (the Earth's planetographic latitude) 07:03:24.11, 36.6 s apart; the ends are 05:37:28.32 and 05:38:08.09. No issue yet.
- **A site built from coordinates, seen from a non-Earth observer in its own frame, throws SPICE(UNKNOWNFRAME) even with Aberration.None.** study/D/s1 A and I, re-run by the verifier: a custom site searched in site.Frame from the Moon throws SPICE(UNKNOWNFRAME) MYSITE_TOPO; from the Earth it works (s1 J). Site.GetEphemeris (Site.cs:207-213) hands the managed SiteFrame to spkezr through RelativeTo, the code path of #489, and the same kind of failure as #558 (a managed frame's name reaching spkezr). Own issue, linked to #489 and #558. FindHorizontalWindows from a custom site with a non-Earth target depends on it. No issue yet.

### R16. Window lists: one closed-interval algebra
**Gain** medium · **Cost** S · **Breaking** yes · **Results** unchanged: Hull is Merge bit for bit, searches return the same windows · **C++** no · **Issues** #668

**Today.** "In view and above the horizon" needs a double loop: a nadir camera sees a site while it is 65 to 82° below its horizon (study/D-window-searches-geometry/s3). `a.Intersect(b)` binds LINQ's equality: [0, 2 h] and [1 h, 3 h] give 0 windows (design/work/r16b). 21 public members (3 of ILocalizable) return IEnumerable<Window> over an array or list (GeometryFinder.cs:637, SpiceAPI.cs:464). Merge gives the hull, as ImpulseManeuver.cs:151 wants.
```csharp
var both = new List<Window>();
foreach (var a in inFov) foreach (var b in above) if (a.Intersects(b)) both.Add(a.GetIntersection(b));
var wrong = inFov.Intersect(above); // compiles: LINQ equality, 0 windows
```
**Proposed.** A static class WindowList extends IEnumerable<Window> with names LINQ lacks: IntersectWindows, UnionWindows, ExceptWindows, ComplementWithin(window), TotalDuration. A result is sorted, disjoint and closed, as CSPICE's wnintd, wnunid, wndifd, wncomd; zero-length windows kept. Each result end is an input end (scale and tick kept); order is Time's (D3). TotalDuration sums EndDate − StartDate (#508). Single windows: Contains(Time), Hull, TryMerge, TryGetIntersection. Searches return IReadOnlyList<Window>.
```csharp
IReadOnlyList<Window> inFov = camera.FindFieldOfViewWindows(window, site, FieldOfViewSearchOptions.Default with { Step = TimeSpan.FromSeconds(30) });
var o = WindowSearchOptions.Default with { Step = TimeSpan.FromMinutes(1) };
var both = inFov.IntersectWindows(site.FindHorizontalWindows(window, sc, HorizontalCoordinate.Elevation, RelationalOperator.GreaterThan, 0.0, o));
var gaps = both.ComplementWithin(window);
TimeSpan total = both.TotalDuration();
```
**Breaks and migration.** IReadOnlyList breaks compiled dependents (rebuild) and ILocalizable implementers (R15). In source, a `var` result later assigned a LINQ query fails (CS0266, compiled). Merge and Intersects(Time) forward to Hull and Contains (IOA0001) until 12.0.0.
**Tests.** Against wn* lists from a spiceypy generator under tools/, as tools/geometry_reference: random lists (overlapping, touching, zero-length, empty, unsorted), membership at every end and midpoint. Ends EqualsExact an input end, UTC and TDB mixed. Hull vs Merge bit for bit. Each search's list equals today's array.

**Results, in detail.** none: the methods are new, Hull returns what Merge returns bit for bit, and every search returns the same windows as a list, element by element (EqualsExact).

**Depends on.** D2, D3, R11, R15. **C++ change.** no.

**Pull requests.** PR 2 of R11 (Window members and the WindowList extensions); the change of the search return type to IReadOnlyList<Window> lands with R15's search PR.

**Open choices.**

1. **Form of the list algebra.** Recommendation: Extension methods on IEnumerable<Window> in a static class WindowList, with names LINQ does not use (IntersectWindows, UnionWindows, ExceptWindows, ComplementWithin, TotalDuration). They bind the same way on a list, an array or the result of .Where(). LINQ's Intersect on two window lists gives 0 windows where the time intersection is 1 h, and Union gives 2 (measured, design/work/r16b and r11r16-rev/b). Alternative: A WindowSet type, sorted and disjoint by construction, returned by every search, with instance methods. The type holds the invariant, but a variable typed IEnumerable<Window> or a .Where() result still binds LINQ's Intersect unless the names differ anyway, and it adds a concept.
2. **Return type of the 21 searches.** Recommendation: IReadOnlyList<Window>. The results are already materialised (arrays at GeometryFinder.cs:637, OccultationFinder.cs:177-179 and SpiceAPI.cs:464, a List for NotEqual at SpiceAPI.cs:507-527), and Count and indexing become visible. Listed under Breaking: binary for compiled dependents, source for ILocalizable implementers and for a `var` result later assigned a LINQ query (CS0266, compiled in design/work/r11r16-rev/c). Alternative: Keep IEnumerable<Window>, since the extensions work on it: no break, but Count stays hidden and callers keep calling ToList().
3. **Zero-length windows in results.** Recommendation: Keep them, as CSPICE's wn* routines do: Equal searches return zero-length crossings, and touching closed windows intersect in one instant (today GetIntersection of [0, 1 h] and [1 h, 2 h] gives [1 h, 1 h], measured). Alternative: Drop them from list results. That is simpler to read, but it loses the results of Equal searches.

### R17. Conjunction analysis: settings kept by the encounter, radius, results
**Gain** high · **Cost** M · **Breaking** yes · **Results** unchanged on kept calls; defect PRs move the Pc · **C++** no · **Issues** #640, #621

**Today.** A 3 m secondary counts 5 m; avoidance drops the encounter's settings; a 30 h lead gives 0 options, no reason (work/r17-r18/before17, study/E-conjunction-cdm/s3).
```csharp
var profile = new ProtectedSpacecraftProfile(sc);
var opts = new ConjunctionAnalysisOptions { StaleCovarianceThreshold = TimeSpan.FromDays(1) };
var enc = ConjunctionAssessment.AnalyzeAll(profile, secondary, window, opts)[^1]; // HBR 13 m
var av = ConjunctionAssessment.EvaluateAvoidance(enc); // stale
var far = ConjunctionAssessment.EvaluateAvoidance(enc, new AvoidanceSearchOptions { LeadTimes = new[] { TimeSpan.FromHours(30) } });
Console.WriteLine($"{far.Count} {enc}"); // 0 IO.Astrodynamics.SSA.EncounterCase
```
**Proposed.** Calls take the `Spacecraft`; one `ConjunctionAnalysisOptions`, in a new `ScreeningOptions.Analysis` and in `AvoidanceSearchOptions.Analysis` (null = the encounter's). Radius per D1 rule 6. `EncounterCase` gets `ProtectedSpacecraft`, `Tca`, `MissDistanceMeters`, `RelativeSpeedMetersPerSecond`, `ProbabilityOfCollision`, `ToString`.
```csharp
var enc = ConjunctionAssessment.AnalyzeAll(sc, secondary, window,
    ConjunctionAnalysisOptions.Default with { StaleCovarianceThreshold = TimeSpan.FromDays(1) })[^1];
Console.WriteLine(enc); // TCA … TDB, miss … m, Pc …, HBR 11 m, None
var study = ConjunctionAssessment.EvaluateAvoidance(enc, // inherits enc.AnalysisOptions
    AvoidanceSearchOptions.Default with { LeadTimes = [TimeSpan.FromHours(30)] });
foreach (var s in study.Skipped) Console.WriteLine(s); // 30 h: before the window
```
**Breaks and migration.** Profile, `ManeuverConstraints`, `ProtectedAsset`, mirrored subsets: [Obsolete] forwarders to 12.0.0; `Analysis` beside a mirrored value throws. `EvaluateAvoidance` returns `AvoidanceStudy`: append `.Options`. Invalid values throw in init (D1).
**Tests.** Old and `Spacecraft` overloads bit for bit on the SSA and CDM tests: both families, VV and RK7(8), TLE and kernel secondaries, CDM bytes. A theory per property. Each inherited setting, skip reason, shortcut; `ToString` with a null Pc.

**Results, in detail.** None on the calls that are kept. These are bit for bit equal to the state the defect PRs leave over the SSA, avoidance and CDM tests: the new Spacecraft overloads with the same settings, the [Obsolete] profile forwarders (the ManeuverConstraints filter, MANEUVERABLE from MaxDeltaV > 0), and ScreeningOptions.Analysis's default (5 min sample, covariance held in RTN, today's ranking). Results move only in the separate PRs, each stated and measured. (1) The secondary HBR defect (D4 rule 6): the Pc and combined radius of every secondary Spacecraft with HBR > 0 under the default option move. The 64 calls of the test helper CreateSpacecraft (ConjunctionAssessmentTests.cs:2259-2267) go from 13 m to 16 m, and the CDM exports move with them. Both are re-derived from tools/collision_probability (Pc2D, mpmath), not snapshotted. An explicit option that differs from a secondary HBR > 0 now throws: 3 test sites (ConjunctionAssessmentTests.cs:443, :491 and RectilinearPassOptions :517-522, each with an 8 m secondary) give the secondary hardBodyRadius 0 and stay bit for bit. The conformance runner sets 0 on the option and on every secondary (ConjunctionSolver.cs:69, :77, :177; StraightLineEncounter.cs:65, :75) and does not move. (2) The avoidance-settings defect: only the options of encounters analyzed with non-default settings move. Expected on the s3 case: post-maneuver HBR 13 m to 8 m and StaleCovarianceUsed cleared; that PR measures it. (3) #621's remainder: on the sampled path the midpoint fallback becomes the window end of smaller distance. Analyze, AnalyzeAll (now empty) and Screen's entries without an interior minimum move. (4) The self-secondary defect: Analyze with the protected spacecraft as secondary throws instead of returning a midpoint.

**Depends on.** D1, D2, D4, R19. **C++ change.** no.

**Pull requests.** PR 1: #640 alone (both AvoidanceSearchOptions steps validated). PR 2: the secondary HBR defect (D4 rule 6, double? on the three options types, finite and >= 0). PR 3: the avoidance inherits the encounter's analysis settings (defect). The resolved options are kept internally, and AnalysisSampleStep, MaximumEventSearchStep and LowRelativeSpeedThresholdMetersPerSecond of AvoidanceSearchOptions become nullable, null = the encounter's. PR 4: Analyze/AnalyzeAll reject the protected spacecraft as its own secondary (defect). #621's remainder in its own PR, with the NoCloseApproachInWindow flag on Analyze. PR 5 (this section): options records with init validation and containment (ScreeningOptions.Analysis, AvoidanceSearchOptions.Analysis), the Spacecraft overloads and the [Obsolete] profile forwarders, EncounterCase.AnalysisOptions, ProtectedSpacecraft and the trajectory source of each participant, AvoidanceStudy (Options, Skipped) with Directions and DeltaVRtn, the result shortcuts and ToString.

**Open choices.**

1. **Avoidance limits (ManeuverConstraints) on the new path.** Recommendation: Make them [Obsolete] with ProtectedSpacecraftProfile. The grid in AvoidanceSearchOptions (LeadTimes and DeltaVMagnitudesMetersPerSecond, validated non-empty, positive and finite) is the constraint. Burns outside the window or the coverage are listed in Skipped. The legacy path keeps its filter and lists each filtered value in Skipped. MANEUVERABLE of object 1 follows the rule already used for object 2 (CdmExporter.cs:344-350): a Spacecraft is YES, and CdmExportOptions.ProtectedParticipantMetadata can override it (:422). The default profile (MaxDeltaV 5 m/s) also gives YES, so the output is bit for bit. Alternative: Move MinLeadTime, MaxLeadTime and MaxDeltaVMetersPerSecond into AvoidanceSearchOptions (the verifier's option 2), with grid values outside the limits listed in Skipped. That keeps the operator's limits, but two fields of the same record then constrain each other.
2. **The value that holds the covariance in RTN inside the analysis.** Recommendation: Keep CovariancePropagationOptions, not flattened (D1; Mode nullable, so null resolves to Covariance inside the analysis). Add CovariancePropagationOptions.None, shared with R06 and D4. CovariancePropagation becomes a non-nullable initializer (D1 rule 2: a constant default). A null in init throws ArgumentNullException that names None, because today's null means 'held in RTN' and must not silently become 'inferred'. Compiled binaries that set null then throw at run time, so this goes under Breaking. Migration covers 12 code sites (10 tests, 1 benchmark, 1 smoke test) and the tutorial block at covariance-propagation.md:229. Alternative: Keep null = held in RTN as a documented exception to D1 rule 2: no break, but null then means something other than 'inferred' only here.
3. **Return type of EvaluateAvoidance.** Recommendation: AvoidanceStudy, a sealed class with Options (IReadOnlyList<AvoidanceOption>) and Skipped (lead time, reason). It is not a collection, because FDG asks a type that implements IEnumerable to end in Collection (CA1710). It is a source and binary break: the migration appends .Options, the same for every caller. Alternative: AvoidanceStudy : IReadOnlyList<AvoidanceOption> plus Skipped. Count, the indexer and foreach stay source-compatible, but the type name breaks FDG's Collection suffix. Or keep IReadOnlyList and add EvaluateAvoidanceStudy, with no break but two methods for one operation.
4. **Reading the four main numbers.** Recommendation: Add Tca, MissDistanceMeters, RelativeSpeedMetersPerSecond and ProbabilityOfCollision on EncounterCase. Add a ToString on EncounterCase, EncounterState and CollisionRisk that prints 'Pc n/a' for a null Pc. ProtectedCovarianceAge and SecondaryCovarianceAge (EncounterState.cs:98, :104) become [Obsolete] (IOA0001). The public constructors of the result types stay until R19's ToEncounter exists. Alternative: ToString only, with one accessor per value; the nested paths stay the only way in.
5. **How the encounter reports each participant's trajectory (missed by the study: a two-body extrapolation backward from the initial epoch gives flags None).** Recommendation: Add EncounterState.ProtectedTrajectory and SecondaryTrajectory, of one enum named for both uses (e.g. TrajectorySource { Propagated, TwoBody, Sgp4, Kernel }, numeric values 0 and 1 kept). AvoidanceTrajectory (Propagated, TwoBody only) is renamed to it in D2's migration as a hard type rename, so AvoidanceOption.Trajectory and the encounter share one type. Alternative: A TwoBodyExtrapolation quality flag only. It is cheaper, but it says nothing about SGP4 or kernel participants.
6. **The covariance setting in ScreeningOptions.Analysis.** Recommendation: Screen honours Analysis as given, its covariance setting included. The default Analysis keeps CovariancePropagationOptions.None and the 5 min step, so today's ranking is bit for bit. A caller who passes the analysis's own options gets the analysis's Pc, at its cost. That is one mechanism with no hidden override. Alternative: Screen always holds the covariance in RTN (the documented rule, conjunction-assessment.md:53) and throws when Analysis asks for a propagated one. The cost stays bounded, but a valid record is rejected by one operation only.

**Candidate defects found on the way.**

- **The secondary's own HardBodyRadius is ignored: Pc 33 % high, NaN and negative radii accepted.** ConjunctionAssessment.cs:2404 adds options.SecondaryHardBodyRadiusMeters (5.0) to the protected radius and never reads Spacecraft.HardBodyRadius (Spacecraft.cs:105, default 0). Measured: a 3 m secondary gives 13 m (work/r17-r18/before17). Re-run of study s4: 15 m gives Pc 3.6145e-4, 13 m gives 2.7147e-4 (ratio 1.331). The verifier measured -5 m giving 3 m combined, and NaN giving Pc null with flags None. The docs say the option is for a secondary 'that does not expose one' (ConjunctionAnalysisOptions.cs:64-67, conjunction-assessment.md:183, :251). CdmExporter.cs:76-79 derives the CDM secondary radius as combined minus protected, so the CDM carries 5 m. Fix per D4: double?, the 5-row precedence, finite and >= 0 on the three options types. Under that rule an explicit option beside a secondary HBR > 0 throws, which hits ConjunctionAssessmentTests.cs:443, :491 and :517-522; those sites give their secondary hardBodyRadius 0. No open issue. No issue yet.
- **EvaluateAvoidance resets the encounter's analysis settings to defaults.** ConjunctionAssessment.cs:514-521 and :669-679 rebuild ConjunctionAnalysisOptions from AvoidanceSearchOptions' subset: SampleStep, MaximumEventSearchStep, SecondaryHardBodyRadiusMeters and LowRelativeSpeedThreshold. EncounterCase keeps no options (EncounterCase.cs:60-90). Measured: an encounter analyzed with StaleCovarianceThreshold 1 day has flags None, while its avoidance option is flagged StaleCovarianceUsed (work/r17-r18/before17). In study s3, HBR 0 gives an encounter of 8 m and options of 13 m. The fix keeps the resolved options internally. The four mirrored properties become nullable, null = the encounter's (the radius already double? after the HBR PR). The avoidance still sets ComputeNonlinearityIndex off (:678), clears ReadRangeRateOverWindow, and sets CovariancePropagation on the two-body fallback (:520) as today. No open issue. No issue yet.
- **EvaluateAvoidance does not validate its steps: OutOfMemoryException.** The verifier measured MaximumEventSearchStep -5 s ending in OOM after 12.5 s, and AnalysisSampleStep -60 s in OOM after 14.6 s (study s3). #640 reports 0 s; this extends it to negative steps and to AnalysisSampleStep. EvaluateAvoidance (ConjunctionAssessment.cs:330) calls AnalyzeInternal directly (:525) and never ValidateAnalysisOptions (:2888). It ships first and alone. Tracked by #640.
- **The sampled path returns the window midpoint when no close approach is found.** Study s3: the Moon as secondary gives the midpoint with a miss of 410 394 091 m. The cause is SelectBestTcaEpoch's midpoint fallback (ConjunctionAssessment.cs:792-818), documented at :175-178. It is fixed by #621's remaining PR: the end of smaller distance, AnalyzeAll empty, and a NoCloseApproachInWindow flag on Analyze only. A zero-length window stays accepted. Both paths already return its epoch, and the dense rule is tested (ConjunctionAssessmentTests.cs:655, code :971). Tracked by #621.
- **Analyze and AnalyzeAll accept the protected spacecraft as its own secondary.** Screen filters out the protected NaifId (ConjunctionAssessment.cs:140), but Analyze and AnalyzeAll do not. Measured: Analyze(profile, sc, 10 min window) gives TCA 12:05:00 TDB (the midpoint), miss 0 m, Pc 0.815 (work/r17-r18/before17). Fix: ArgumentException naming secondaryObject on the same NaifId rule as Screen. No open issue. No issue yet.

### R18. Propagated participants: two overloads kept, the search made visible
**Gain** medium · **Cost** S · **Breaking** yes · **Results** unchanged where accepted · **C++** no · **Issues** #621, #628

**Today.** On an RK7(8) pair the two searches agree (0.2 µs, <0.1 mm) but differ in cost (`AnalyzeAll`, 3 h: 22 ms, 7 ms), label (window scale, TDB) and window ends (#621). A wrong trajectory is accepted: miss 0 m, Pc 0.68 (work/r17-r18/before18, study/E-conjunction-cdm/s2).
```csharp
var t1 = sc.Propagate(window, none, new RK78Integrator(), false, false, step, null);
var t2 = secondary.Propagate(window, none, new RK78Integrator(), false, false, step, null);
var profile = new ProtectedSpacecraftProfile(sc);
var sampled = ConjunctionAssessment.Analyze(profile, secondary, window);
var dense = ConjunctionAssessment.Analyze(profile, t1, secondary, t2, window);
var wrong = ConjunctionAssessment.Analyze(profile, t2, secondary, t2, window);
```
**Proposed.** Both families stay bit for bit (D4 rule 6). No inference from `LastPropagation` (RK7(8) only, PropagatorBase.cs:609; it would move `Screen` and the labels). `EncounterCase.Search` (`Sampled`, `Polynomial`) says which ran. `ReadRangeRateOverWindow = true` throws where the sampled search runs. Another spacecraft's trajectory throws (defect PR).
```csharp
var t1 = sc.Propagate(window);
var t2 = secondary.Propagate(window);
var enc = ConjunctionAssessment.Analyze(sc, t1, secondary, t2, window);
Console.WriteLine(enc.Search); // Polynomial
ConjunctionAssessment.Analyze(sc, t2, secondary, t2, window); // throws: t2 is S's
var o = ConjunctionAnalysisOptions.Default with { ReadRangeRateOverWindow = true };
ConjunctionAssessment.Analyze(sc, secondary, window, o); // throws: sampled
```
**Breaks and migration.** `ReadRangeRateOverWindow = true`, ignored today, throws on the `ILocalizable` overloads, `ScreeningOptions.Analysis`, an explicit avoidance `Analysis` and trajectories of different observer or frame: remove it. Inherited, it is cleared as today; it stays `bool`. The reference tabulates both searches.
**Tests.** TCA accuracy, #621 and #628 tests bit for bit; `Search` on each path; `true` throws on each sampled path, `false` bit for bit. A foreign solution throws; an owner-less one passes; the runner report holds.

**Results, in detail.** None for any call that is still accepted. Both overload families stay bit for bit (D4 rule 6), shown by ConjunctionTcaAccuracyTests, the #621/#628 tests and the conformance report. What changes is calls that now throw. First, ReadRangeRateOverWindow = true where the sampled search runs, which today is silently ignored (documented at ConjunctionAnalysisOptions.cs:57-59). Second, in the defect PR, a trajectory owned by another spacecraft, which today gives a meaningless result (miss 0 m, Pc 0.676 measured). Inferring the polynomial search on the ILocalizable overloads, with one TDB TCA label, would move Screen's results and labels. That is filed and measured on its own, not done here.

**Depends on.** R17, D1, D4. **C++ change.** no.

**Pull requests.** First, the defect PR that rejects a trajectory owned by another spacecraft (D4 rule 7). PropagatorBase sets an internal owner for every integrator, compared by reference; owner-less solutions are accepted. Then, inside R17's PR 5: EncounterCase.Search, the throw of ReadRangeRateOverWindow = true on the sampled search, and the reference table of the two searches.

**Open choices.**

1. **Fate of the PropagationSolution overloads in 11.0.0.** Recommendation: Keep both families bit for bit (D4 rule 6), add the ownership check and EncounterCase.Search, and file the inference. LastPropagation is kept after RK7(8) only (PropagatorBase.cs:609, Spacecraft.cs:824-832), so inference would not reach Velocity-Verlet pairs; study s2 is such a pair. Inference would also move Screen and the TCA labels. The avoidance deliberately reads the participants through the sampled path (AvoidanceSearchOptions.cs:38-44). Alternative: The study's proposal: remove the trajectory overloads and infer the polynomial search from each LastPropagation. This needs every integrator's solution kept (about 0.55 MB per LEO day in RK7(8), Spacecraft.cs:826-829; not measured for Velocity-Verlet) and a stated change of Screen's ranking, cost and labels.
2. **ReadRangeRateOverWindow where the polynomial search cannot run.** Recommendation: Keep it bool (default false) and throw ArgumentException naming options when it is true at the start of a call that runs the sampled search. That covers the ILocalizable overloads, Screen's Analysis, an explicit avoidance Analysis, and trajectories that do not share observer and frame (CanUseDensePolynomialSearch, ConjunctionAssessment.cs:913-921). An inherited avoidance value is cleared internally, as today. With a false default, 'set' is detectable as true, so D1 rule 2's intent holds without a type change. Under bool?, D1 rule 10 would resolve a dense encounter's value to false, and reusing those resolved options on Screen would throw. Alternative: bool? per the letter of D1 rule 2 (null = off, any non-null value throws where the sampled search runs). That is a binary break of the property, and it brings the trap of reusing resolved options described above.
3. **Showing which search ran.** Recommendation: EncounterCase.Search, an enum CloseApproachSearch { Sampled, Polynomial }. It explains the cost, the label and the window-end rule of each result, and it costs nothing. Alternative: Nothing on the result; the reference table only.

**Candidate defects found on the way.**

- **Analyze and AnalyzeAll accept a trajectory that belongs to another spacecraft.** No overload compares a PropagationSolution with its spacecraft (ConjunctionAssessment.cs:221-245, :297-320). Measured: Analyze(profile, t2, secondary, t2, window) gives TCA 12:00:00 TDB, miss 0.0000 m, Pc 0.676 on an RK7(8) pair (work/r17-r18/before18). The re-run of study s2 (Velocity-Verlet pair) gives miss 0.000 m, Pc 0.221. Fix per D4 rule 7: an internal owner set by PropagatorBase, compared by reference, and an ArgumentException naming the parameter. Owner-less solutions (the runner's StraightLineEncounter, `new PropagationSolution()` at :113) are accepted. No open issue. No issue yet.
- **The TCA is labelled in the window's scale on the sampled path and in TDB on the polynomial path; the polynomial search is not inferred for RK7(8) pairs.** Re-run of study s2 on one UTC window (Velocity-Verlet pair): the sampled path gives 13:59:56.5197669Z and the trajectory path 14:01:05.7070377 TDB, about 3 ms apart. On an RK7(8) pair (work/r17-r18/before18) the sampled path labels a UTC window 2024-01-01T11:59:00.2482427Z. The two searches agree within 0.2 µs and 0.1 mm, and AnalyzeAll takes 22 ms sampled against 7 ms polynomial over 3 h (this run). This is D4 rule 7's filed item 'dense inference together with the TCA label', related to #621. The study missed item 2: a UTC window over the 'same' span as a TDB propagation throws, because 3 UTC hours are 3.6 µs longer in TDB (measured). That throw is correct (#527, Spacecraft.cs:812-821), and its message gives both windows, so it is out of scope. No issue yet.

### R19. CDM: ingest, catalogue objects, validation
**Gain** high · **Cost** M · **Breaking** no · **Results** unchanged (additions); CDM text moves in #615, #618 · **C++** no · **Issues** #342, #614, #615, #618

**Today.** `Cdm` has no conversion (`Opm` has), the Pc is internal (ConjunctionAssessment.cs:2651). study/E-conjunction-cdm/s4 guesses EME2000 as ICRF, wraps each object in a dummy spacecraft (Pc 1.6062e-4); runner: StraightLineEncounter.cs (122 lines). TLE: s2.
```csharp
var tca = new Time(cdm.RelativeMetadataData.TcaUtc, TimeFrame.UTCFrame);
var d = cdm.Segments[1].Data;
var sv = new StateVector(d.StateVector.PositionMeters, d.StateVector.VelocityMetersPerSecond, earth, tca, Frame.ICRF);
var p = sv.RotateCovarianceFromRtn(d.CovarianceMatrix.StateCovarianceRtn);
var o2 = new Spacecraft(-2, "o2", 100, 1000, new Clock("c2", 256), new StateVector(sv.Position, sv.Velocity, earth, tca, Frame.ICRF, p));
// object 1 alike, then Analyze
```
**Proposed.**
```csharp
StateVector s1 = cdm.ToStateVector(CdmObjectRole.Object1); // s2 alike
EncounterCase enc = cdm.ToEncounter(combinedHardBodyRadiusMeters: 10.0);
double? pc = CollisionProbability.Foster2D(s1, s2, 10.0);
var debris = TrackedObject.FromTle(tle, hardBodyRadiusMeters: 1.0);
```
- `ToStateVector`: EME2000, GCRF to `Frame.ICRF` (Opm.cs:322-324), ITRF to ITRF93; P from RTN, null if absent.
- `ToEncounter`: the runner's straight-line encounter; 0 covariances: `CovarianceUnavailable`; 1: `FrisbeeMax`. The file's Pc stays.
- `CollisionProbability`: two epochs or frames throw.
- `TrackedObject` (sealed `CelestialItem`) from a TLE, TLE OMM or state: HBR (D4 rule), NORAD, COSPAR, `ObjectType`, read by the exporter. Object 1's MANEUVERABLE: R17.
- #614, #615, #618 with #342: own PRs in 11.0.0; the reader takes both roots (CdmReader.cs:97-101). `Tca` (Time) beside `TcaUtc`; R22's Kind rule.

**Breaks and migration.** None; #615, #618 change the written text (Changed).

**Tests.** `cdm` 001-006 (EME2000) by `ToStateVector` bit for bit with CdmSolver; GCRF as EME2000, ITRF against `sxform`; `ToEncounter` bit for bit with StraightLineEncounter; Pc on `collision_probability` (mpmath bound); 0-2 covariances; `FromTle` (NORAD 339999) bit for bit with the wrapper; a `TrackedObject` export; runner unchanged.

**Results, in detail.** None on existing paths: Cdm.ToStateVector, Cdm.ToEncounter, CollisionProbability and TrackedObject are additions. ToStateVector is shown bit for bit against CdmSolver on cdm 001-006, which are all EME2000. GCRF is checked against EME2000, and ITRF against SPICE sxform. ToEncounter is shown bit for bit against StraightLineEncounter, FromTle against the Spacecraft wrapper, and the Pc primitive against the collision_probability cases within CLAUDE.md's mpmath bound. The written CDM text changes only in the separate PRs of #615 (root namespace) and of #618 with #342 (FOSTER-1992). Each of those PRs validates its output against SANA's NDM/XML 4.0.0 schema. The cdm 007 golden holds no method name. No number moves.

**Depends on.** D1, D2, D4, R17, R22. **C++ change.** no.

**Pull requests.** PR 1: Cdm.ToStateVector, Cdm.ToEncounter and the public CollisionProbability. The conformance runner drops StraightLineEncounter in the same PR, before R17 makes the result constructors internal. PR 2: TrackedObject, with the exporter reading its identity. #614, #615, and #618 together with #342 (as #618 asks) each land in their own PR in 11.0.0.

**Open choices.**

1. **Hard-body radius of Cdm.ToEncounter.** Recommendation: Require a combined radius argument (combinedHardBodyRadiusMeters). A CDM carries no HBR, and AREA_PC (CdmModels.cs:141) is optional and is an area. Alternative: Infer the radius from AREA_PC as sqrt(A/pi) per object, sum the two, and throw when AREA_PC is absent.
2. **Shape of the public Pc primitive.** Recommendation: A static CollisionProbability class (namespace Ssa) with one method per method, as the verifier asks. Foster2D(s1, s2, hbr) is CARA's Pc2D with both covariances. FrisbeeMax(s1, s2, hbr) is Frisbee's maximum with one covariance: it is not Foster's method, so it is not named Foster2DMaximum. Each returns null when the covariance it needs is absent. Both throw ArgumentException on states at two epochs or in two frames. Alternative: One Compute(s1, s2, hbr) that picks the method from the covariances present and returns the Pc together with the method used.
3. **Lightweight catalogue participant.** Recommendation: TrackedObject, a sealed CelestialItem (negative NAIF id, so IsSpacecraft, CelestialItem.cs:79) built FromTle, FromOmm or from a state. FromOmm needs a TLE-compatible OMM, because mean elements give no state. It carries an HBR, the NORAD and COSPAR ids and an ObjectType (default Unknown). It has no clock, engine or maneuver. Alternative: A Spacecraft.FromTle factory with a default mass and clock. It keeps the Spacecraft inference of OBJECT_TYPE Payload and MANEUVERABLE Yes (CdmExporter.cs:344-350, :390-397).
4. **Default NAIF id of TrackedObject.FromTle.** Recommendation: An optional naifId whose default is -100000 - NORAD id (mkspk's convention, confirmed by S. Guillet, 2026-11-13). It covers Alpha-5 up to 339999 (-439999). Document the range: it overlaps MonteCarloConfiguration.BaseNaifId (-100000, propagators.md:149), and Screen skips any candidate with the protected asset's NAIF id (ConjunctionAssessment.cs:140), so a colliding id drops an object silently. Alternative: A required naifId parameter with no default.
5. **CDM dates.** Recommendation: Add Time accessors (Tca and the other dates as Time) beside the DateTime *Utc init properties. Hand-built CDMs and the reader's records keep their DateTime initializers. The Kind handling of the writer follows R22's rule (#617). Alternative: Retype the eight *Utc properties to Time, as the verifier suggested. SSA then has one epoch type, but every hand-built CDM initializer breaks and each name loses its Utc suffix. (A leap-second date, #616, fits neither DateTime nor today's UTC Time constructors, so it does not decide this choice.)
6. **RTN axes of an ITRF state in ToStateVector.** Recommendation: Build R, T and N from the inertial state at TCA, with N along the orbital angular momentum (CCSDS 508.0-B-1 RTN definition, confirmed by S. Guillet, 2026-11-13). Then turn the covariance and test it against SPICE sxform. Alternative: Build RTN from the ITRF state as given, which gives a rotating-frame T and N.

**Candidate defects found on the way.**

- **CDM export writes MANEUVERABLE YES for any Spacecraft secondary, even one without an engine, next to INTRACK_THRUST false.** CdmExporter.cs:344-350: object 2 gets CdmManeuverable.Yes whenever the source is a Spacecraft. CdmExporter.cs:371 writes IntrackThrust = Engines.Count > 0 in the same metadata block. verify/E/v1: a debris TLE wrapped in a Spacecraft with no engine is exported with MANEUVERABLE Yes and OBJECT_TYPE Payload (:390-397). OBJECT_TYPE is an inference that TrackedObject replaces. MANEUVERABLE contradicts the same block. No issue yet.
- **A TLE, kernel or single-covariance secondary can never be exported to a CDM (FOSTER-2D-MAX-PC is never written).** verify/E/v1: ToCdm throws 'Secondary object covariance is required to export a standards-compliant CDM' for a TLE secondary. The TrackedObject export test therefore uses a state with a covariance. #618 asks to decide its method value in the same PR as this one. Tracked by #342.
- **Cdm.ValidateSchema checks nothing on the unqualified CCSDS 508.0 forms.** study/E-conjunction-cdm/s1, re-run in work/r19r22-rev/r19: a written CDM gives ValidateSchema IsValid True with 0 issues. It ships in 11.0.0 in its own PR. Tracked by #614.
- **CdmWriter puts the cdm and ndm roots in the NDM/XML namespace, which SANA's schema rejects.** Re-run in work/r19r22-rev/r19: the written root is <ndm xmlns="urn:ccsds:schema:ndmxml">. The reader matches local names (CdmReader.cs:97-101), so it reads both forms during the transition. It ships in 11.0.0 in its own PR. Tracked by #615.
- **CDM export writes COLLISION_PROBABILITY_METHOD FOSTER-2D, which SANA does not register.** Re-run in work/r19r22-rev/r19: CollisionProbabilityMethod = FOSTER-2D. SANA's cdm_cpm and CARA write FOSTER-1992. #618 asks for one PR with #342. Tracked by #618.

### R20. Return types and coordinates: StateVector where one is built, geodetic in one call
**Gain** high · **Cost** M · **Breaking** yes · **Results** unchanged, bit for bit · **C++** no · **Issues** #662

**Today.** GetEphemeris, ReadEphemeris, ToFrame and RelativeTo return OrbitalParameters, built as a StateVector: `.ToStateVector()` follows at 52 library, 231 test and 11 doc sites. Swapped, RadiusFromPlanetocentricLatitude gives 0.00349 m and Equatorial(ra, dec) (0.853, 0.492, 0.174) for (0.853, 0.150, 0.500) (study/A-…/s05-ephemeris, study/G-…/s2-frames, s3-math).
```csharp
var sv = moon.GetEphemeris(epoch, earth, Frame.ICRF, Aberration.None).ToStateVector();
var p = sv.ToFrame(Frame.ECLIPTIC_J2000).ToStateVector().Position;
var pd = sv.ToPlanetocentric(Aberration.None).ToPlanetodetic(earth.Flattening, earth.EquatorialRadius);
var eq = new Equatorial(dec, ra, epoch);
```
**Proposed.** ILocalizable, IDataProvider (R04) and ReadEphemeris return StateVector; CelestialItem and Site add (epoch, observer) and (window, observer, step), ICRF and geometric. ToFrame and RelativeTo, made virtual, are overridden in StateVector, covariant: a TLE keeps its same-frame identity. AtEpoch is unchanged.
```csharp
StateVector sv = moon.GetEphemeris(epoch, earth);
Vector3 p = sv.ToFrame(Frame.ECLIPJ2000).Position;
Planetodetic pd = sv.ToPlanetodetic();
Vector3 xyz = pd.ToCartesianCoordinates(earth);
var eq = Equatorial.FromRightAscensionDeclination(ra, dec, epoch);
var site = new Planetodetic(longitude: lon, latitude: lat, altitude: 1e3);
```
**Breaks and migration.** Recompile. Source: GetEphemeris implementers, overrides; `List<OrbitalParameters> l = ….ToList()`. IOA0001 to 12.0: ToPlanetocentric(Aberration) (unused, CelestialItem.cs:623), (f, R) forms, Equatorial(double, double, …), AtmosphericContext.FromPlanetodetic(alt, lat, lon, t). Coordinates keep init, Deconstruct; `Longitude:` breaks. A non-body observer throws InvalidOperationException, not InvalidCastException (OrbitalParameters.cs:1023).

**Tests.** New overloads equal (ICRF, None) bit for bit: body, propagated and kernel spacecraft, Site, Star. Suite, conformance report unchanged. sv.ToPlanetodetic() equals the two hops bit for bit (poles, axis, equator), recgeo; ToCartesianCoordinates: georec. Docs compile.

**Results, in detail.** none

**Depends on.** D2, R04. **C++ change.** no.

**Pull requests.** PR R20-a: StateVector returns (ILocalizable, IDataProvider with R04, SpiceApi.ReadEphemeris), the default overloads on CelestialItem and Site, and virtual ToFrame/RelativeTo with covariant StateVector overrides. PR R20-b: one-call geodetic conversions taking the CelestialBody, Equatorial.FromRightAscensionDeclination, AtmosphericContext.FromPlanetodetic(Planetodetic, Time), camelCase coordinate constructors, IOA0001 forwarders. D2 N7 replaces the finding's 'document the usings' choice.

**Open choices.**

1. **Where the StateVector return type lands.** Recommendation: Change ILocalizable.GetEphemeris (both overloads), IDataProvider.GetEphemeris (R04 already breaks it), SpiceApi.ReadEphemeris and CelestialItem's virtual, so calls through ILocalizable get a StateVector too. Every implementation already returns one (CelestialItem.cs:301-322, Spacecraft.cs:678, SpiceDataProvider.cs:32, MemoryDataProvider.cs:82), and the library chains .ToStateVector() after GetEphemeris at 31 sites. Alternative: The verifier's narrower form. The concrete classes return StateVector, and ILocalizable and IDataProvider stay unchanged through explicit interface implementations. No implementer breaks, but callers through the interface keep the conversion.
2. **Ellipsoid arguments of the coordinate conversions.** Recommendation: Add overloads taking the CelestialBody: ToPlanetodetic(body), ToPlanetocentric(body), RadiusFromPlanetocentricLatitude(body), ToCartesianCoordinates(body). The last forwards to the existing CelestialBody.RadiusFromPlanetocentricLatitude(latitude). The (flattening, radius) and (radius, flattening) forms become IOA0001 forwarders until 12.0: a swapped RadiusFromPlanetocentricLatitude compiles and returns 0.00349 m with no error. A swapped ToPlanetodetic throws ArgumentOutOfRangeException instead. Alternative: Keep a double form for custom ellipsoids under one order everywhere, CSPICE georec's (equatorialRadius, flattening), and obsolete only the forms in the other order.
3. **Defaults of frame and aberration.** Recommendation: Add overloads GetEphemeris(epoch, observer) and (window, observer, step) on CelestialItem and Site, with ICRF and Aberration.None, the documented defaults of propagation and of attitude targets. Optional parameters on a virtual or interface member would bind the default into the caller (FDG). Alternative: No default, as today: every read names the frame and the aberration.

**Candidate defects found on the way.**

- **ToEquatorial and Equatorial(StateVector) ignore the state's frame: the 'RightAscension' of an ITRF93 state is an Earth-fixed longitude.** OrbitalParameters.cs:970-974 and Coordinates/Equatorial.cs:28-37 take atan2/asin of the position in whatever frame the state is in, and Equatorial has no Frame. For one LEO state at 2024-06-21 12:00 TDB, RA is 8.366 deg in ICRF and 278.797 deg after ToFrame(ITRF93), the same through ToEquatorial() and new Equatorial(sv) (design/work/r20r23-rev2). The verifier measured 0 against 20.525 deg (verify/G/own). The docs present the result as RA/Dec (coordinate-conversions.md:85, :125). Fix: rotate to ICRF first. Results move for every non-ICRF state; measure them against CSPICE recrad. The study missed it, and no issue is open. No issue yet.

### R21. The TLE type: parsing, fields, mean vs osculating
**Gain** high · **Cost** M · **Breaking** yes · **Results** unchanged · **C++** no · **Issues** #463, #604, #606, #607

**Today.** A 2-line set needs a name; a CR, swapped lines, a checksum throw 3 types, after a kernel read (study/F-…/s9-bad-tle, TLE.cs:176). 4 fields are read only in ToOmm (TLE.cs:944-976). `MeanMotion()` is mean or osculating by static type: 0.14 % (s2-tle-states).
```csharp
var again = TLE.Create(tle.ToMeanKeplerianElements(), tle.Name, tle.NoradCatalogId,
    tle.Line1.Substring(9, 8).Trim(), int.Parse(tle.Line2.Substring(63, 5)),
    bstar: tle.BallisticCoefficient, nDot: tle.FirstDerivationMeanMotion,
    nDDot: tle.SecondDerivativeMeanMotion, elementSetNumber: int.Parse(tle.Line1.Substring(64, 4)));
double n1 = tle.MeanMotion(), n2 = ((OrbitalParameters)tle).MeanMotion(); // mean, osculating
```
**Proposed.** `Tle.Parse`, `TryParse`, `IParsable<Tle>` (2 or 3 lines, CR/LF, trailing blanks), `LoadAll(path)`, `ParseAll(TextReader)`. Text is checked before SPICE: `TleFormatException : FormatException` (line, column) or false. No name: the catalogue field. Added: `CosparId` ("98067A", null if blank), `ElementSetNumber`, `RevolutionNumberAtEpoch`, `Classification`, `MeanMeanMotion` (rad/s, the former `MeanMotion()`). Inherited members: SGP4 osculating at Epoch, documented.
```csharp
var all = Tle.LoadAll("active.txt");
var again = Tle.Create(tle.ToMeanKeplerianElements(), tle.Name, tle.NoradCatalogId, tle.CosparId,
    tle.RevolutionNumberAtEpoch, tle.Classification, tle.BStar, tle.MeanMotionDot,
    tle.MeanMotionDDot, tle.ElementSetNumber);
double n = tle.MeanMeanMotion; // mean, like MeanInclination
```
**Breaks and migration.** `Tle.MeanMotion()`: [Obsolete(error: true)] to 12.0.0; ToOmm reads MeanMeanMotion (the base gives 15.4709 rev/day, not 15.4931). The constructor throws ArgumentException on any bad line, checksum included (R03 defers here); a blank name is legal. Names: D2, R27.
**Tests.** Parse (2, 3 lines, CRLF) equals the constructor; LoadAll order; each s9 case: line, column, TryParse false, kernel-free child; Create from the properties rebuilds each Sgp4Reference set; ToOmm byte for byte; Alpha-5, blank COSPAR (#604); CS0619 on `tle.MeanMotion()`.

**Results, in detail.** none. No numeric output moves. ToOmm reads the new MeanMeanMotion, the former TLE.MeanMotion() bit for bit (equal to the parsed n, design/work/r12r21-rev/r21), so its XML stays byte for byte. Bound to the osculating base method, it would write MEAN_MOTION 15.470915975 instead of 15.49309423 rev/day (re-measured). Create from the hand-parsed fields gives the same two lines on the ISS set. Only exception types change: the constructor throws ArgumentException on any malformed line, where today a checksum or catalogue mismatch throws InvalidOperationException (TLETests.cs:186, :388, :397 change). A blank name becomes legal (TLETests.cs:210 changes). Parse throws TleFormatException.

**Depends on.** D2, D3, R03, R22, R27. **C++ change.** no.

**Pull requests.** PR 1: text validation before any SPICE read, Parse/TryParse/IParsable/LoadAll/ParseAll, TleFormatException, the constructor's exception types, the five new properties. PR 2: [Obsolete(error: true)] on Tle.MeanMotion(), ToOmm switched to MeanMeanMotion, docs/reference/tle.md states the osculating contract. Type and field renames land in D2/R27's rename PR.

**Open choices.**

1. **Exception of malformed text: Parse and the constructor.** Recommendation: Parse: TleFormatException : FormatException with the line and column, as int.Parse and D3's Time.Parse do; TryParse never throws for bad text. The constructor: ArgumentException naming line1 or line2 (FDG for bad arguments), with the TleFormatException as InnerException. One validator serves both. Alternative: The constructor throws TleFormatException too, as Guid(string) does. One type everywhere, but against FDG's ArgumentException rule for arguments.
2. **Name of a set read without a name line (Space-Track 2-line form).** Recommendation: The catalogue field of line 1 as text ("25544", Alpha-5 as written). ToOmm's OBJECT_NAME and the native SGP4 call, which takes the name (ConvertTleToStateVector), keep working. The constructor accepts a null or blank name with the same rule. Alternative: Name = null; ToOmm and the native call then need their own fallback, and FDG discourages null strings on a property.
3. **Fate of TLE.MeanMotion() (mean, a parameterless overload beside the osculating OrbitalParameters.MeanMotion(double)).** Recommendation: [Obsolete(error: true)] in 11.0.0 with the message 'MeanMeanMotion for the mean motion of the TLE; ((OrbitalParameters)tle).MeanMotion() for the osculating one', removed in 12.0.0. A bare removal binds every call to the osculating base, 0.14 % off, silently: the trap the verifier found in ToOmm (TLE.cs:952). D3 and D4 treat their traps the same way. Alternative: Remove it in 11.0.0 with a release note; calls then bind to the osculating method without a diagnostic.
4. **Should Tle keep deriving from OrbitalParameters.** Recommendation: Keep, with the documented contract 'the inherited members are the SGP4 osculating state at Epoch' and no member whose result depends on the static type. Spacecraft, Scenario and the conjunction overloads take a TLE as OrbitalParameters today. Alternative: Composition: Tle is not an OrbitalParameters and Spacecraft takes it through an overload. Cleaner types, but it breaks every call that passes a TLE as an orbit, for little gain once MeanMotion() is gone.
5. **A malformed set inside a catalogue read by LoadAll/ParseAll.** Recommendation: Throw TleFormatException at the first bad set, with its line in the file; no partial list. Alternative: Skip it and return the errors beside the list. Useful for dirty feeds, but a silent count change is easy to miss. It can be added later as an option without a break.

**Candidate defects found on the way.**

- **AtEpoch and ToKeplerianElements(Time) relabel mean elements (tle.ToMeanKeplerianElements()) as osculating, bypassing the mean-element guard.** study/F-tle-omm-opm-orbital-parameters/s2-tle-states (re-run by the verifier): mk.AtEpoch(t).ElementsType == Osculating; #463's table: AtEpoch(t0), AtEpoch(t0 + 1 h) and ToKeplerianElements(t0 + 1 h) are labelled Osculating, and ToStateVector() returns a state. Tracked by #463.
- **A blank international designator cannot be written by TLE.Create nor round-tripped through an OMM.** TLE.cs:341-345 (cosparId required, 6 to 8 characters); the new CosparId property returns null for a blank field and needs #604's fix for the Create round trip. Tracked by #604.
- **Omm.ToTle and TLE.Create default the element set number differently (999 against 9999).** Omm.cs:292 (?? 999) against TLE.cs:34 (MAX_ELEMENT_SET_NUMBER = 9999, Create's default); exposing ElementSetNumber removes the need to rely on either default in a round trip. Tracked by #606.
- **TLE.Create checks neither the frame (TEME) nor the centre (Earth) of its mean elements.** TLE.cs:326 (ElementsType only), :366-378 (Keplerian conversion in their own frame, n from their own centre). Tracked by #607.

### R22. CCSDS OMM/OPM: metadata honoured, one shape, dates
**Gain** high · **Cost** M · **Breaking** yes · **Results** OPM centre from CENTER_NAME; mislabelled OMM: `ToTle` throws; REF_FRAME text · **C++** no · **Issues** #339, #607, #617

**Today.** A lunar OPM without observer is an Earth orbit, a 956 km, e 0.988 (study/F-…/s4-opm; Opm.cs:224-229). A MOON/ICRF/DSST OMM gives an Earth TEME TLE (s10; Omm.cs:84, :244, :255). A 2-object NDM loads one (OmmReader.cs:116-122; OPM, CDM too). Verbs: study/J s5.
```csharp
var sv = opm.ToStateVector();               // CENTER_NAME MOON: about the Earth
var omm = Omm.LoadFromFile("catalog.xml");  // 2 objects: first only
string x1 = omm.ToXml(), x2 = opm.ToXmlString(wrapInNdm: false, indent: true);
opm.SaveToFile("p.xml", true, false, true);
```
**Proposed.**
```csharp
var sv = Opm.Load("lunar.xml").ToStateVector(); // observer from CENTER_NAME
var all = Omm.LoadAll("catalog.xml"); // Load throws on 2
var mean = all[1].ToMeanElements(); // SI
opm.Save("p.xml", new CcsdsWriteOptions { WrapInNdmContainer = false });
```
- CENTER_NAME by the names written (MOON, SOLAR SYSTEM BARYCENTER) and synonyms; unknown or contradicted: throws. `Opm.ToSpacecraft` alike; `Omm.ToSpacecraft`: TLE OMMs.
- `IsTleCompatible`: SGP4, TEME, EARTH; else `ToTle` names the keyword.
- D1 verbs, all three: `Load`, `Parse`, `LoadAll`, `TryLoad`, `Save`, `ToXml`, `Validate`, `IsValid`. The options hold each reader, writer, validator switch (all true today); those classes go `[Obsolete]`. `useMeanMotion` constructor: its factories; `CreateMinimal`: enums.
- Models keep `DateTime`, TIME_SYSTEM (UT1 loads); `GetEpoch()` throws there; `CreateForTle(Time)`. One Kind rule in 3 writers (#617): Unspecified is the message's scale, Local throws (D3).
- `*Meters`, `BisectionToleranceSeconds` stay (D1); unsigned: R06, R10, R27.

**Breaks and migration.** The above, in defect PRs; Local throws where the CDM writer converts it, OMM/OPM write it as is. Old verbs `[Obsolete]` (IOA0001) to 12.0, a table: 152 test and runner lines, 56 doc lines.

**Tests.** OPM about Moon, SSB bit for bit; OPM/OMM tests, 17 `ToTle` calls unchanged; each throw; same XML by new verbs; `ToMeanElements` = `FromOMM`, bit for bit; `cdm` 001-006 via `Cdm.Load`; Kind under TZ=Europe/Paris.

**Results, in detail.** Intended changes, each first in its own defect PR. (1) An OPM whose CENTER_NAME is not EARTH, read without an observer, is now about that centre. In study/F s4-opm, re-run, a lunar state reads today as an Earth orbit (a 956 km, e 0.988); after the fix, the Moon and SSB round trips come back bit for bit. (2) An OMM that is not SGP4/TEME/EARTH gets IsTleCompatible false: ToTle throws, and Omm.ToString loses its ' (TLE)' marker (Omm.cs:187). (3) Load of a multi-message NDM throws. (4) One Kind rule in the three writers (#617). Unspecified is the message's scale. Local throws, where today the CDM writer converts it and the OMM and OPM writers write its clock reading. (5) #339 writes REF_FRAME ICRF instead of J2000 in OPM text. Kept unchanged: the 17 ToTle test calls, iss.omm and H2ADEB.omm (both SGP4/TEME/EARTH), the runner's OMMs (TleConversionSolver.cs:59), XML byte-identical through the new verbs, and ToMeanElements bit for bit with FromOMM.

**Depends on.** D1, D2, D3, R02, R06, R10, R21, R27. **C++ change.** no.

**Pull requests.** Defect PRs first: OPM CENTER_NAME; OMM TLE metadata; multi-message NDM; #617 (one Kind rule for the CDM, OMM and OPM writers); #339. Then PR A: CcsdsReadOptions/CcsdsWriteOptions and one verb set on Omm, Opm and Cdm. The old verbs and the Reader/Writer/Validator classes become [Obsolete] (IOA0001) with [EditorBrowsable(Never)], and are removed or made internal in 12.0.0. Then PR B: LoadAll, ToMeanElements, Omm.ToSpacecraft, GetEpoch and CreateForTle(Time).

**Open choices.**

1. **How CENTER_NAME maps to a body.** Recommendation: A managed table of the names the writer emits and their NAIF synonyms, case-insensitive. The writer emits SPICE names, measured in work/r19r22-rev/names: MOON, MARS, SUN, SOLAR SYSTEM BARYCENTER, EARTH BARYCENTER. Synonyms include SSB and EARTH-MOON BARYCENTER. The table needs R02's pure-data constants, so the lookup reads no kernel and takes no type initializer (#623). The body is then built as today (CelestialBody(id, ICRF, epoch), which reads kernels). An unknown name without an observer throws ArgumentException naming it. Alternative: bodn2c_c through a new native proxy. It also knows kernel-defined names (NAIF_BODY_NAME), but it is a C++ change under the SPICE lock.
2. **Omm/Opm/Cdm.Load on an NDM holding several messages.** Recommendation: Throw, with a message naming LoadAll. As the verifier found, Load callers never see a validation warning, so option (c) of the finding stays silent. Alternative: Keep the first message and expose the count of dropped messages on the loaded object.
3. **Default validation of Cdm.Load and Cdm.Save.** Recommendation: CcsdsReadOptions.Default and CcsdsWriteOptions.Default validate for all three messages, as Omm and Opm do today: every switch defaults to true in all three types (Omm.cs:362, Opm.cs:522, Cdm.cs:36), so one Default keeps them. The obsolete Cdm.Read*/Write* forward with validation off, bit for bit. Test that cdm 001-006 load through Cdm.Load. Alternative: Nullable ValidateSchema/ValidateContent, where null means today's per-type default (OMM and OPM validate, the CDM does not).
4. **Time type of the CCSDS models.** Recommendation: Keep DateTime with TIME_SYSTEM on the raw models. A UT1 OMM loads today with default validation, with a warning only (OmmValidator.cs:311-316; measured in work/r19r22-rev/ut1), and so would TCB, MET or SCLK messages. Add GetEpoch() returning Time, which throws for a scale Time lacks, and Time overloads of the factories (CreateForTle). Alternative: Retype the DateTime properties to Time. Messages in UT1, TCB, TCG, MET or SCLK could then no longer load.
5. **MEAN_ELEMENT_THEORY values that count as TLE-compatible.** Recommendation: SGP4 only, the one value MeanElementTheory parses (MeanElementTheory.cs:22). SGP4-XP is excluded: the library runs classic SGP4 only. Alternative: Also accept SGP/SGP4, the ODM's example value (CCSDS 502.0-B-3, confirmed by S. Guillet, 2026-11-13).
6. **KeplerianElements.FromOMM.** Recommendation: Make it [Obsolete] (IOA0001), forwarding to the internal path that Omm.ToMeanElements uses (which keeps the cached mean motion), and remove it in 12.0. It is the only SI-side API that takes degrees and rev/day. Alternative: Keep it, documented as taking CCSDS units.
7. **GM used by Omm.ToMeanElements to turn n into a.** Recommendation: The kernel GM of the observer, as FromOMM uses today: bit for bit, and stated in the XML doc. Alternative: WGS-72's GM, the constants of SGP4, for an SGP4 theory.

**Candidate defects found on the way.**

- **Opm.ToStateVector and ToSpacecraft without an observer ignore CENTER_NAME: a lunar OPM is read as an Earth orbit.** study/F-tle-omm-opm-orbital-parameters/s4-opm, re-run in work/r19r22-rev/r22: CENTER_NAME MOON is read back with observer EARTH, |r| 1900 km, a 956 km, e 0.988. Opm.cs:224-229 builds new CelestialBody(399) and never reads Metadata.CenterName (documented at Opm.cs:193, but silent). Opm.ToSpacecraft (Opm.cs:280-286) inherits it. This is the reading-side twin of #607. No issue yet.
- **Omm.IsTleCompatible and ToTle ignore CENTER_NAME, REF_FRAME and MEAN_ELEMENT_THEORY: a MOON/ICRF/DSST OMM becomes an Earth TEME TLE.** study/F s10-omm-metadata: IsTleCompatible is True, because Omm.cs:84 checks only Data.HasTleParameters. ToTle hard-codes the Earth (Omm.cs:244) and TEME (Omm.cs:255). OmmValidator warns only for DSST, and LoadFromFile callers never see the warning. SGP4-XP must not count as compatible either. No issue yet.
- **A multi-message NDM loads its first message silently (OMM, OPM and CDM readers).** study/F s3-omm: a 2-object catalog.xml loads ISS (ZARYA) with no error (re-run in work/r19r22-rev/r22). FirstOrDefault at OmmReader.cs:116-122, OpmReader.cs:115-120 and CdmReader.cs:97-101. No issue yet.
- **The OMM and OPM writers write a DateTime of Kind Local as its clock reading: Omm.CreateForTle with 12:00 local in Paris is written 24001.50000000, one hour late.** study/F s3 under TZ=Europe/Paris (re-run by the verifier). OmmWriter.cs:435-439 and OpmWriter.cs:429-433 format the value with no Kind handling. #617 covers the CDM writer and asks for one rule for the three writers in the same PR; this is that rule's OMM/OPM side. Tracked by #617.
- **OPM export writes REF_FRAME = J2000 for Frame.ICRF, which the library's own OpmValidator reports as non-standard.** Opm.cs:112-113 (the remark) and :133 (writes stateVector.Frame.Name). Re-run in work/r19r22-rev/r22: Validate().IsStrictlyValid False, "Reference frame 'J2000' is not a standard CCSDS frame." The fix in 11.0.0 writes ICRF, in its own PR; the study missed it. It changes the written text. Tracked by #339.

### R23. Orbital elements: one meaning per constructor, errors naming the argument, value equality
**Gain** medium · **Cost** M · **Breaking** yes · **Results** values bit for bit; ==, hashes, exceptions change (#475) · **C++** no · **Issues** #475, #476, #481, #663, #512, #662

**Today.** trueAnomaly and period are used unchecked beside a and M: 6177.2 km off, yet equal (study/F-…/s7). perigeeRadius: 1 m makes an ellipse's PerigeeRadius() 1 m (OrbitalParameters.cs:749). Equal copies hash apart, mean elements throw in ==, i = 51.6 gives '[-PI,PI]' without ParamName (F s5, J s3).
```csharp
double m = OrbitalParameters.TrueAnomalyToMeanAnomaly(nu, e, shape.EccentricAnomaly(nu));
var k = new KeplerianElements(a, e, i, raan, aop, m, earth, epoch, Frame.ICRF, trueAnomaly: nu);
var k2 = new KeplerianElements(a, e, i, raan, aop, m, earth, epoch, Frame.ICRF);
var set = new HashSet<KeplerianElements> { k, k2 }; // Count 2, k == k2
```
**Proposed.** Six elements named as the methods; elementsType stays. FromTrueAnomaly is the runner's code (OrbitalElementsSolver.cs:131-140); Parabolic takes q, e in #459's band (1 by default). Caches go internal (5 library callers). Guards: ArgumentOutOfRangeException with ParamName, Deg2Rad named except in FromOMM (degrees). Equality: #475's plus q and covariance; Time by D3.
```csharp
var k = KeplerianElements.FromTrueAnomaly(a, e, i, raan, aop, nu, earth, epoch, Frame.ICRF);
var q = KeplerianElements.Parabolic(perigeeRadius, i, raan, aop, m, earth, epoch, Frame.ICRF);
var k2 = new KeplerianElements(a, e, i, ascendingNode: raan, argumentOfPeriapsis: aop,
    meanAnomaly: m, observer: earth, epoch: epoch, frame: Frame.ICRF);
var set = new HashSet<KeplerianElements> { k, k2 }; // Count 1
```
**Breaks and migration.** Recompile. trueAnomaly: → FromTrueAnomaly, period: goes, perigeeRadius: → Parabolic (16 test sites); rigthAscendingNode and FromOMM's raan become ascendingNode (hard, D2 N5). A set and its state, mean and osculating twins differ.

**Tests.** #475's matrix: no throw, equal hashes over 20 000 pairs, no returned state mutated. Parabolas of q 6800, 9000 km unequal. Each guard's type and ParamName. FromTrueAnomaly is the runner's bit for bit, TrueAnomaly() = ν. Barker cases, internal callers bit for bit. Docs compile.

**Results, in detail.** No state or element value moves: bit for bit on the existing suite and the conformance report. What changes: the booleans of ==/Equals and the hash codes, as #475 tabulates. A set and its own state, mean and osculating twins, states with different covariances, and parabolas with different q become unequal, and equal copies share a hash. Exception types also change (ArgumentOutOfRangeException with ParamName). Measured by #475's matrix over 20 000 pairs.

**Depends on.** D2, D3. **C++ change.** no.

**Pull requests.** #476, #481 and #663 land first, each in its own PR. PR R23-a: the public constructor with six elements, FromTrueAnomaly, Parabolic, the internal constructor keeping the caches for the 5 library callers, and the guards. PR R23-b: equality per #475, with q and the covariance; Time equality comes from D3.

**Open choices.**

1. **Constructor parameter names.** Recommendation: Use the names of the methods every OrbitalParameters shares: semiMajorAxis, eccentricity, inclination, ascendingNode, argumentOfPeriapsis, meanAnomaly (D2 N3: full English, one term per concept). FromOMM, FromTrueAnomaly and Parabolic use the same names. D2's table records the pairing with the RAAN and AOP properties. Alternative: Use the property names a, e, i, raan, aop, m, which the docs guessed. This is a literal reading of D2 N3's 'a parameter is named as the member it sets', and it conflicts with its full-English rule.
2. **Name of the true-anomaly factory.** Recommendation: FromTrueAnomaly, beside FromOMM on the same type. Alternative: CreateWithTrueAnomaly, the name of the same concept in OpmKeplerianElements (OpmKeplerianElements.cs:136).
3. **perigeeRadius of EquinoctialElements.** Recommendation: Apply the same rule in R23-a, because it is the same unchecked cache. With P = 7000 km and perigeeRadius 1 m, PerigeeRadius() returns 1 m, and Equals is True against the set without it (measured, work/r20r23-rev2). Accept it only when P is not finite, the parabola it serves (EquinoctialElements.cs:285-299), and throw ArgumentException otherwise. Internal callers (OrbitalParameters.cs:717) use the internal constructor. Test call sites: EquinoctialReference.cs:184. Alternative: Leave EquinoctialElements unchanged in 11.0.0 and file its cache in its own issue.
4. **Negative inclination.** Recommendation: Keep the accepted range [-pi, pi] here, and change only the exception and the message. Rejecting i < 0 changes accepted input, so it goes to its own PR after a search of callers (verifier's note). Today i = -0.5 is stored as given, and ToStateVector().Inclination() returns 0.5 (measured). Alternative: Reject i outside [0, pi] in 11.0.0, with i -> -i, Omega -> Omega + pi, omega -> omega + pi in the migration note.
5. **Covariance in StateVector equality.** Recommendation: Compare it (Matrix is a record struct with value equality, Matrix.cs:6, :344). Equality then covers every stored value, and two states with different covariances give different Pc. Today they compare equal (StateVector.cs:536-541, measured). Alternative: Ignore it, as today: equality of position, velocity, observer, epoch and frame only.
6. **Physical comparison across representations.** Recommendation: No new method in 11.0.0: compare ToStateVector() explicitly, as #475 proposes. A tolerance method needs its own design (frames, epochs, observers). Alternative: Add IsCloseTo(other, positionTolerance, velocityTolerance). The study proposed both IsCloseTo and IsSameStateAs.

**Candidate defects found on the way.**

- **Two parabolas with different perigee radii compare equal (KeplerianElements and EquinoctialElements).** KeplerianElements.Equals (KeplerianElements.cs:247-252) compares A, which is +Infinity for a parabola, and ignores the perigee radius. Measured: e = 1, a = +Infinity, q = 6800 km against q = 9000 km gives Equals True and == True, with states 2200 km apart. EquinoctialElements with P = +Infinity and q = 6800 km against 9000 km also gives Equals True, with states 2857 km apart (design/work/r20r23-rev2). This is not in #475's text. Add it there so that the #475 PR covers it. Tracked by #475.
- **CombinedManeuver caches the current orbit's perigee radius on the target orbit.** CombinedManeuver.cs:56-57 builds the target orbit with a = (TargetPerigeeHeight + apogee)/2 and perigeeRadius: stateVector.PerigeeRadius(), the current orbit's perigee. The target's PerigeeRadius() would return that wrong value (OrbitalParameters.cs:749). Today only ToStateVector() reads the target, and it ignores the cache on an ellipse (OrbitalParameters.cs:584-589), so no result moves. Drop the argument in its own PR, and show it bit for bit. No issue yet.

### R24. Time: a required scale, one equality key, one grammar, nearest-tick arithmetic
**Gain** high · **Cost** L · **Breaking** yes · **Results** library unchanged; AddDays/Hours/Minutes and CreateFromJD to the nearest tick (≤ 1 tick, measured), cross-scale ==, TT prints TT · **C++** no · **Issues** #512, #637, #660

**Today.** An untagged calendar is TDB (69.18 s from UTC), a day of year UTC, day 400 accepted; one instant in two scales is unequal with CompareTo 0; AddHours(NaN) is a no-op (study/G-time-frames-math-units/s1-time, s5, s7). `PI2` is π/2 (study/J-fdg-cross-cutting/s6-before-compiles).
```csharp
var t = new Time(2024, 6, 21, 12, 0, 0); // TDB
var d = Time.Create(2024, 400, 0.0); // 2025-02-03 UTC
bool hit = new HashSet<Time> { t.ToUTC() }.Contains(t); // false
var n = t.AddHours(double.NaN); // t
var x = new Time("2024-172T12:00:00 UTC"); // FormatException
double q = Constants.PI2; DateTime j = Time.J2000; // π/2; a DateTime
```
**Proposed.** D3 with D2's names, plus: (1) `Constants.TwoPi`, `HalfPi`; `_2PI`, `PI2` IOA0001 forwarders (same doubles); g0, JULIAN_* and the rest follow D2's table. (2) `Time.J2000` stays a DateTime, IOA0001-obsolete: retyped, `Time.J2000.AddDays(1)` would compile and print otherwise (measured). (3) The CLI parser (Helpers.cs:93-142) keeps bare seconds, now with any scale tag, and passes the rest to `Time.TryParse(s, TimeScale.TDB)`.
```csharp
var t = new Time(2024, 6, 21, 12, 0, 0, TimeScale.UTC);
var d = Time.FromDayOfYear(2024, 173, 43200.0, TimeScale.UTC);
bool hit = new HashSet<Time> { t.ToTdb() }.Contains(t); // true: the TDB tick
if (Time.TryParse("2024-172T12:00:00 UTC", out var p)) { } // or "JD 2460000.5 TDB"
double q = Constants.HalfPi; Time j = Time.J2000Tdb;
```
**Breaks and migration.** D3's (CS0619 at frameless constructors and Time(string), cross-scale ==, Kind checks, CLI flags). Day-of-year checks land first, in their own PR. CLI JD inputs move ≤ 1 tick.
**Tests.** D3's list. The forwarders equal today's values. CLI: every form `EpochInputTests` accepts today, bit for bit, JD at the exact nearest tick; verify/H's '757339200 TT', '2460310.5 JD TT', '2024-001T00:00:00Z' against `Time.Create`, `CreateFromJD`, `FromDayOfYear`. Full suite with Validation and long-validation.yml: no golden moves.

**Results, in detail.** No library result moves. No library code outside Time calls Time.AddDays, AddHours, AddMinutes or CreateFromJD, and the library's own DateTime.AddDays calls add whole days (Time.cs:338, TLE.cs:250, :455, CcsdsEpochParser.cs:76). What users see change: those four methods round to the nearest tick. That is at most 1 tick from today: measured over 1.2e7 random arguments against an exact BigInteger nearest-tick reference (work/r24r26-rev2), and the moved test values are listed as in #634. Also: == and hashing across scales follow D3's TDB-tick key; Time.ToString writes TT where it wrote TDT; the CLI's JD inputs (Helpers.cs:110, :114) move by at most 1 tick. Proof: the full suite with Validation and long-validation.yml with no golden recaptured, and the conformance runner bit for bit.

**Depends on.** D3, D2, D1. **C++ change.** no.

**Pull requests.** The single D3 time PR, sequenced with D2's naming table. The Constants and Time.J2000 forwarders land in D2's rename PR for the root namespace and TimeSystem. The CLI parser lands in D3's CLI time-converter part. Before it, the NaN and day-of-year defects each get their own PR. #660's runner readers move to Time.Parse in that issue's PR.

**Open choices.**

1. **Fate of Time.J2000 (a DateTime with Kind Unspecified, Time.cs:12).** Recommendation: Make it an IOA0001 [Obsolete] [EditorBrowsable(Never)] field with the same value. Its message names Time.J2000Tdb and Time.J2000Tdb.DateTime. Remove it in 12.0.0. Internal uses move to an internal field: Time.cs:13, :14, :306, :318, :379, :394 and PropagationTimeArgument.cs:53. Outside the library, ConformanceRunner/Solvers/TimeConversionSolver.cs, two test files (DynamicalTimeReference.cs, SecondsToTicksTests.cs) and time-and-windows.md:11 migrate. A retype in place would be silent for code that uses var: `Time.J2000.AddDays(1)` compiles on both types, and prints '01/02/2000 12:00:00' as a DateTime but a TDB string as a Time (measured, work/r24-before). Alternative: Retype Time.J2000 to the TDB Time in 11.0.0 and drop J2000Tdb, as the finding's after-code does. The name is shorter, but call sites that use `var`, ToString or AddDays change meaning without a compile error.
2. **Names of 2π and π/2.** Recommendation: Constants.TwoPi and Constants.HalfPi, as D2 N3 decided. _2PI and PI2 become IOA0001 const forwarders until 12.0.0. TwoPi is the same double as Math.Tau, because 2·π is exact in binary (measured: _2PI == Math.Tau is True). The change is therefore bit for bit. #471 (fl(2π) used as 2π) is unchanged and stays open. Alternative: Remove both constants and point users to Math.Tau and Math.PI / 2, as the finding's after-code does. That follows the BCL but contradicts D2 N3, and Constants.PI would then be left alone.
3. **What the obsolete Time.Create(year, doy, sec) forwards. Today year 0 reads as 2000, and day 0, day 400 or 90000 s roll into the neighbouring day or year (Time.cs:335-339, measured)..** Recommendation: The defect PR (see defects) makes Create validate first: year >= 1, a day inside the year, seconds in [0, 86400). It lands before D3 and is listed under Breaking. D3's obsolete Create then forwards to FromDayOfYear(year, doy, sec, TimeScale.UTC) bit for bit, as D1 rule 9 requires. Valid inputs never move. Alternative: Keep the old leniency in the obsolete Create until 12.0.0, so every input stays bit for bit, and validate only in FromDayOfYear.
4. **Tagged seconds in the CLI ('757339200 TT' fails today with a FormatException; the help documents only '<UTC or TDB>', Commands/Parameters/EpochParameters.cs:9).** Recommendation: The CLI keeps its own bare-seconds form, as D3 says ('the CLI's bare seconds stay theirs'), and now accepts any scale tag. Untagged stays TDB, as at Helpers.cs:133. Every other form goes to Time.TryParse(s, TimeScale.TDB). UTC is no longer detected by Contains("z"). Alternative: Bare seconds stay UTC or TDB only. A seconds string with another tag is rejected, with a message that lists the accepted forms.

**Candidate defects found on the way.**

- **Time.AddDays/AddHours/AddMinutes(NaN) return the epoch unchanged and CreateFromJD(NaN) returns J2000, silently, while AddSeconds(NaN) throws.** Measured in work/r24r26-rev: t.AddHours(double.NaN) prints t unchanged, and Time.CreateFromJD(double.NaN, UTCFrame) prints 2000-01-01T12:00:00.0000000Z. AddDays(NaN) behaves the same (verifier, G1). The infinities already throw ArgumentOutOfRangeException (measured, work/r24r26-rev2), so only NaN is silent. Sources: Time.cs:122-135 (DateTime.AddDays/AddHours/AddMinutes) and Time.cs:391-400 (J2000.AddDays). #637 tracks the truncation of these methods but not the NaN no-op. Fix through ToNearestTicks (Time.cs:182-203), which throws on a non-finite value. That fix is either its own PR or a stated extension of #637, before D3. No issue yet.
- **Time.Create(year, doy, sec) validates nothing: year 0 reads as 2000, and a day or seconds outside the day roll into the neighbouring day or year.** Measured in work/r24r26-rev2. Create(2024, 400, 0) gives 2025-02-03T00:00:00Z, Create(2024, 0, 0) gives 2023-12-31, Create(2024, -5, 0) gives 2023-12-26, Create(2024, 1, 90000) gives 2024-01-02T01:00:00Z, and Create(0, 1, 0) gives 2000-01-01 (verifier G5). Source: Time.cs:335-339, `year == 0 ? 2000 : year` and baseDate.AddDays(doy - 1) with no range check. No library code calls it; the tests call it 15 times with valid inputs. Listed under Breaking. No issue yet.

### R25. Frames by name and frame errors
**Gain** medium · **Cost** S · **Breaking** yes · **Results** unchanged where SPICE gives a transform; an unknown name throws `ArgumentException`, not `InvalidOperationException` · **C++** yes (R03's PR 1) · **Issues** #656, #558, #454

**Today.** A typo reads as missing coverage: `TransformFrameProxy` sees `sxform`'s SPICE(UNKNOWNFRAME) (`failed_c` stays set) but drops it (Proxy.cpp:625-629), and SpiceAPI.cs:1132-1136 guesses "does not cover this epoch". `MemoryDataProvider` throws `ArgumentException` (MemoryDataProvider.cs:34). CIRS by name throws (#656); no `Frame.ITRF93` (G/s2).
```csharp
sv.ToFrame(Frame.CIRS);              // works
sv.ToFrame(new Frame("CIRS"));       // InvalidOperationException (#656)
sv.ToFrame(new Frame("ITRF"));       // "... does not cover this epoch or is not loaded"
```
**Proposed.** After #656 and R03's PR 1.
- SPICE(UNKNOWNFRAME): `ArgumentException` naming the frame and "not defined by the loaded kernels", the `SpiceException` inner. Checked at the transform: the pool can change after `new Frame(name)`.
- Other frame errors (FRAMEDATANOTFOUND, coverage): `SpiceException`, naming frame and epoch, so the propagation's orientation cache still reads them as unavailable (PropagationFrameOrientationCache.cs:77, :124).
- `Frame.ITRF93`, the SPICE name (D2 N2).
- `new Frame("ICRF")` and `"EME2000"` stay other names (frames.md:93); the message names `Frame.ICRF`.
```csharp
var cirs2 = sv.ToFrame(new Frame("CIRS"));     // Frame.CIRS's rotation (#656)
var itrf = sv.ToFrame(Frame.ITRF93);
sv.ToFrame(new Frame("ITRF"));
// ArgumentException: 'ITRF' is not a frame of the loaded kernels (SPICE(UNKNOWNFRAME))
```
**Breaks and migration.** `catch (InvalidOperationException)` misses an unknown frame: catch `ArgumentException` (Changed). Coverage errors keep their type.
**Tests.** An unknown name on each path to SPICE frames (`ToFrame` both ways, `GetEphemeris` with and without LT, `TransformFrame` by instance and name, a propagation's orientation cache, both providers): `ArgumentException`, nothing on stdout. An epoch past the Earth PCK: `SpiceException`. `Frame.ITRF93 == new Frame("ITRF93") == earth.Frame`, transforms bit for bit equal. Frame tests, conformance frame cases unchanged.

**Results, in detail.** None where SPICE gives a transform: the same sxform call, bit for bit, checked by the existing frame tests and the conformance frame cases. An unknown frame name changes from InvalidOperationException to ArgumentException. A coverage error stays an InvalidOperationException (SpiceException), so the propagation's orientation cache treats it as before. CIRS/TIRS by name is #656's change, measured in #656. No golden moves.

**Depends on.** R03. **C++ change.** yes.

**Pull requests.** #656 first, in its own PR (a defect: CIRS/TIRS by name in both data providers, the reads and TransformFrame). The rest of #558 stays in #558: the coordinate search and ReadOrientation in CIRS/TIRS, and TEME on gfposc. The ephemeris reads of #558 are already routed (SpiceAPI.cs:196-201). TransformFrameProxy's bool + lastError lands in R03's PR 1, built by build-native. Then one small .NET PR: the SPICE(UNKNOWNFRAME) mapping to ArgumentException and Frame.ITRF93.

**Open choices.**

1. **Exception for a frame name the loaded kernels do not define.** Recommendation: ArgumentException naming the frame and 'not defined by the loaded kernels', with R03's SpiceException (ShortMessage SPICE(UNKNOWNFRAME)) as the inner exception. ParamName is set when the frame is an argument of the public call. MemoryDataProvider already throws ArgumentException for an unknown frame (MemoryDataProvider.cs:34), so both providers agree. A coverage failure on a known frame stays SpiceException : InvalidOperationException, naming the frame and the epoch. The name is checked at the transform, not in new Frame(name), because the kernel pool can change after construction (verifier). Alternative: Throw SpiceException alone for every SPICE frame error. That is one type with R03, but a typo looks like a state error, and the two data providers then disagree.
2. **The names ICRF and EME2000.** Recommendation: Keep both as other names, as frames.md:93 documents. The ArgumentException message for these two names points to Frame.ICRF. Routing 'ICRF' to J2000 without equality gives a frame that converts but is not Frame.ICRF: no identity shortcut, and it does not pass the inertial check as Frame.ICRF. Aliasing it in equality too means the hash must follow aliases, and the documented rule changes. A strict rule can be relaxed later without a break. Both names throw today (design/work/r03-r25-rev/r25b), so no result moves either way. Alternative: Alias ICRF and EME2000 to J2000 in routing and in equality (Equals and GetHashCode on the SPICE name), and change frames.md:93. EME2000 then drops the 23 mas bias, as the CCSDS readers already do (frames.md:142-145).
3. **The data provider of the static Frame.ITRF93.** Recommendation: Follow R04's rule for captured providers. Until then, document that Frame.ITRF93, like Frame.ECLIPTIC_J2000, reads the provider active when Frame is first used (the constructor captures it, Frame.cs:156). Test it under MemoryDataProvider. Alternative: No static. Document earth.Frame and new Frame("ITRF93"), which read the provider active at their construction.

### R26. Math value types: an immutable Matrix and the usual operators
**Gain** high · **Cost** M · **Breaking** yes · **Results** unchanged (library arithmetic bit for bit) · **C++** no · **Issues** #662

**Today.** `Matrix` wraps a shared `double[,]` (Matrix.cs:8, :28-33) with a public `Set` (:77): copies alias, a `StateVector` keeps the caller's matrix (StateVector.cs:96), `default(Matrix).Get` throws NullReferenceException. `Vector3` has no `-v`, `double * v` or `Matrix * v`; vector `*` is the dot product, component-wise in System.Numerics (study/G-time-frames-math-units/s3-math, s4-units).
```csharp
var cov = new Matrix(6, 6);
for (int i = 0; i < 6; i++) cov.Set(i, i, 100.0);
var sv = new StateVector(r, v, earth, epoch, Frame.ICRF, cov);
cov.Set(0, 0, -1.0); // sv's [0,0] is -1 too
sv.Covariance.Value.Set(1, 1, -5.0); // and its [1,1] is -5
double[] a = m * new[] { r.X, r.Y, r.Z }; // no Matrix * Vector3
var o = r.Inverse(); double dot = r * v;
```
**Proposed.** `Matrix(double[,])` copies its array and `Set` gets `[Obsolete(error: true)]` (IOA0002). The library fills fresh matrices through an internal setter and owning constructor. New: a get-only indexer, `With`, `Create(rows, columns, f)`, `Diagonal`, `Identity(n)`. Bad indexes throw ArgumentOutOfRangeException; `default(Matrix)` is 0 × 0 for every member. `Vector3` gains `-v`, `double * v`, `Matrix * v` (3 × 3 only) and `Dot`; vector `*` an IOA0001 forwarder of `Dot`.
```csharp
var cov = Matrix.Diagonal(100, 100, 100, 1, 1, 1);
var sv = new StateVector(r, v, earth, epoch, Frame.ICRF, cov); // fixed
var cov2 = cov.With(0, 0, 50.0);
Vector3 a = m * r; var o = -r; double dot = r.Dot(v);
```
**Breaks and migration.** CS0619 at `Set` (226 code lines in 30 files, 25 doc lines): fill a `double[,]`, then `new Matrix(a)`, `Create` or `With`. Listed: a later write to `a` no longer reaches it; IndexOutOfRangeException becomes ArgumentOutOfRangeException; 10.1.0 binaries keep `Set` until 12.0.0. Vector `*` warns; math.md:28, :40 change.
**Tests.** No caller action changes a stored matrix (StateVector, encounter, CDM, Monte Carlo); every member on `default(Matrix)`. New members bit for bit against `Set`, `Multiply(double[])`, `Inverse()`, `*`. Full suite with Validation and conformance report: bit for bit.

**Results, in detail.** None for the library. It fills its matrices through an internal setter, which replaces the 63 Set lines and is used only on matrices not yet returned (ConjunctionAssessment.cs:628 already copies before it writes, :685-697), and through an internal owning constructor. The owning constructor replaces the 6 Matrix(double[,]) sites: Matrix.cs:161, Jacobian.cs:31, :46, CovarianceMatrix.cs:200, :272 and SiteFrame.cs:56. None of them writes its array after construction (checked). So the arithmetic and the allocations are unchanged. Vector `*` forwards to Dot, the same expression (v.X*w.X + v.Y*w.Y + v.Z*w.Z, Vector3.cs:88), bit for bit. Matrix * Vector3 is the arithmetic of Multiply(double[]). User-visible changes, listed under Breaking: an array written after new Matrix(array) no longer reaches the matrix, and an index out of range throws ArgumentOutOfRangeException instead of IndexOutOfRangeException. Proof: the full suite with Validation and the conformance report, bit for bit, with no golden recaptured.

**Depends on.** D1, D2. **C++ change.** no.

**Pull requests.** One PR, 'Math value types: immutable Matrix, Vector3 operators', after D2's IOA0001 infrastructure PR. The two defects below go first, each in its own PR.

**Open choices.**

1. **Level of the [Obsolete] on Matrix.Set.** Recommendation: error: true (CS0619), as D3 and D4 treat traps. Set writes through any getter into the matrix an object stores: StateVector.Covariance, EncounterState.CombinedCovarianceRtn, CollisionRisk.ProjectedCovariance, CdmModels.StateCovarianceRtn and EpochStatistics.SampleCovariance. Measured: sv.Covariance.Value.Set(1, 1, -5) changes sv. An error costs the same migration as a warning, which 12.0.0 forces anyway, and the migration is mechanical. It covers 226 code lines in 30 files: 206 in 25 files of IO.Astrodynamics.Tests, 12 in SmokeTests, 5 in Performance and 3 in the conformance runner. It also covers 25 doc lines in 3 tutorials. Binaries built against 10.1.0 keep calling Set until 12.0.0. Alternative: Warning level, per D1 rule 9, plus a copy of the covariance in the StateVector constructor. That closes only the caller's-matrix path in 11.0.0. Writes through a getter stay possible until 12.0.0.
2. **Vector3 operator *(Vector3, Vector3), which is the dot product.** Recommendation: Add Dot(in Vector3) and make the operator an IOA0001 warning-level forwarder to Dot, removed in 12.0.0. FDG says to overload an operator only when its result is obvious, and System.Numerics.Vector3 * Vector3 is component-wise. An [Obsolete] on a user-defined operator warns at each `a * b` (compiled, work/r26-op). The library's own uses switch to Dot, the same expression, bit for bit. math.md:40 ('There is no .Dot() method') changes. Alternative: Keep * as the documented dot product and only add Dot. Users see no warning, but the operator keeps a meaning that differs from the BCL's.
3. **Vector3.Inverse() (the negation).** Recommendation: Keep it, documented as the negation, beside the new unary minus. StateVector.Inverse() means the same thing (state-vector.md:89), so the term is consistent within the library. It is not a trap, and an obsolete marker would add churn for no safety gain. Alternative: Make it an IOA0001 forwarder to unary minus, so that only Matrix.Inverse() carries the word 'inverse' (D2 N3, one term per concept). StateVector.Inverse() would then need the same treatment.
4. **Matrix.ToString (the record's 'Matrix { Rows = 3, Columns = 3 }', measured).** Recommendation: Print the rows with invariant culture and round-trip doubles. No library code, test or CLI output reads Matrix.ToString (grep), so nothing moves. Alternative: Keep the record's ToString and leave formatting to callers through the indexer.

**Candidate defects found on the way.**

- **Matrix.Equals treats NaN as equal to any value, and the hash codes then differ.** Matrix.cs:360 rejects a pair only when |a − b| > Double.Epsilon, which is false for NaN. Measured in work/r24r26-rev: a zero 2×2 matrix and the same matrix with NaN at [0,0] give == True, while GetHashCode differs. That breaks the Equals/GetHashCode contract and transitivity, and a NaN covariance compares equal to a valid one. Vector3, a record struct, compares with double.Equals, where NaN equals only NaN. No open issue tracks this. No issue yet.
- **Normalize() of a zero Vector3 or a zero Quaternion returns zero silently, so a degenerate axis or attitude becomes a zero vector.** Vector3.cs:31-40 and Quaternion.cs:43-52 return `this` when the magnitude is 0. Measured (work/r24r26-rev, verify/G/own): Vector3.VectorX.Rotate(new Quaternion(0,0,0,0).Normalize()) returns (0,0,0) with no error. The SpiceAPI path rejects a non-unit quaternion (#454), but the math types do not. This is an item the study missed. The fix (throw or return NaN) can move results, so it needs its own issue, with an audit of the 65 internal Normalize() calls. Vector3.Angle returns 0 for a zero vector by convention (Vector3.cs:52-56). No issue yet.

### R27. Naming rule and its mapping table
**Gain** high · **Cost** L · **Breaking** yes · **Results** unchanged · **C++** no · **Issues** #668, #662

**Today.** ṅ/2 has four names, ω five. s4: CS1739 on `rightAscendingNode` and `dragCoefficient` (study/J-fdg-cross-cutting/s4-overloads). CA1707 ×176, CA1725 ×14, CA1721 ×4.
```csharp
var ke = new KeplerianElements(7e6, 0.001, 0.9, rigthAscendingNode: 1.0, 0.5, 0.0, earth, epoch, Frame.ICRF);
var sc = new Spacecraft(-1001, "Sat", 500.0, 1000.0, clock, ke, dragCoeff: 2.2, solarRadiationCoeff: 1.3);
TLE.Create(mean, "ISS", 25544, "98067A", 1, bstar: tle.BallisticCoefficient, nDot: tle.FirstDerivationMeanMotion);
double raan = ke.RAAN, T = earth.SideralRotationPeriod(epoch).TotalSeconds, q = Constants.PI2;
```
**Proposed.** D2's N1–N8, and:
- A parameter is named as the member it sets (as `startDate`). Any other `Time` parameter is `epoch` or ends in `Epoch`; `other` stays.
- TLE fields: OMM names (`MeanMotionDot`, `MeanMotionDDot`, `BStar`).
- `KeplerianElements.A`…`M` forward to the methods.
- CA1721: `RightAscensionSigmaAt`, `DeclinationSigmaAt`, `OrbitalDirectionTarget.OrbitalDirection`; `Frame.StateOrientationsToICRF` (protected, unused) goes.
- `SiderealRotationPeriod`, `SunSynchronousOrbit`, `SolarRadiationCoefficient`, `Tsiolkovsky`, `CreateFromGaussMethod`, `TwoPi`, `HalfPi`, `StandardGravity`, `JulianDateJ2000`, `SecondsPerDay`.
```csharp
var ke = new KeplerianElements(7e6, 0.001, 0.9, ascendingNode: 1.0, 0.5, 0.0, earth, epoch, Frame.ICRF);
var sc = new Spacecraft(-1001, "Sat", 500.0, ke, maxMass: 1000.0, dragCoefficient: 2.2, solarRadiationCoefficient: 1.3);
Tle.Create(mean, "ISS", 25544, "98067A", 1, bStar: tle.BStar, meanMotionDot: tle.MeanMotionDot);
double raan = ke.AscendingNode(), T = earth.SiderealRotationPeriod(epoch).TotalSeconds, q = Constants.HalfPi;
```
**Breaks and migration.** 10.1.0 members renamed by more than case: IOA0001 forwarders (N5). Hard, by sed: parameters, types (`Tle` with its members, `Tsiolkovsky`), case-only names, protected members. File keys stay (N6): Cosmographia.Model goes internal (R04).

**Tests.** NamingRuleTests: deny-list, parameter and `epoch` rules. Forwarders bit for bit. Migrated suite with Validation, conformance report and written files unchanged.

**Results, in detail.** none. Renames and IOA0001 forwarders only. The full suite with Validation, migrated by the sed script, the conformance report (report_meta ignored) and the files the library writes (CCSDS, kernels, Cosmographia JSON) stay bit for bit. Each forwarder is bit for bit its target. No dynamics change, so long-validation.yml is not needed.

**Depends on.** D2, D1, D3, R02, R04, R05, R10, R15, R21, R23, R24, R26. **C++ change.** no.

**Pull requests.** Inside D2's landing order: the infrastructure PR (PublicApiAnalyzers, NamingRuleTests baseline with the deny-list), then one rename PR per area with its docs: (a) orbital elements and the TLE glossary, with R21 and R23; (b) bodies and spacecraft parameters, with R10; (c) Constants, Time and Frame statics, with R24. Then D2's final PR turns the analyzers into errors and generates the tables.

**Open choices.**

1. **EquinoctialElements has two accessors per element: properties P, F, G, H, K, L0 and methods EquinoctialEx(), EquinoctialEy(), Hx(), Hy(), Lv() (EquinoctialElements.cs:75-99). CA1711 flags 'Ex'..** Recommendation: Keep the properties P, F, G, H, K, L0, the symbols of the modified equinoctial elements. Make the five methods IOA0001 forwarders: the library calls none of them; they appear on 5 test lines (EquinoctialElementsTests.cs:73-77) and 6 doc lines (orbit-creation.md:96, keplerian-elements.md:61, DEVELOPER_GUIDE.md:555, 564-566). Alternative: Keep the methods and make the single-letter properties obsolete, as KeplerianElements does with A...M. That loses the standard symbols, and Ex/Ey/Hx/Hy/Lv are not full words either.
2. **Scenario.Spacecrafts: a non-English plural, but the English plural is the type name.** Recommendation: Keep Spacecrafts and add it to the cspell dictionary. A collection property named Spacecraft, next to Sites and Stars, would read as one object. Alternative: Rename it to Spacecraft (a 10.1.0 member renamed by more than case, so with a forwarder). It compiles inside Scenario (checked), but the property and its element type share the name.
3. **Name of LambertSolver.Solve's misspelled parameter targetOjectOrbitalParameters (LambertSolver.cs:25).** Recommendation: targetObjectOrbitalParameters: the spelling fix only. It is what lambert.md:59 and #662 already use. Alternative: targetOrbitalParameters, paired with initialOrbitalParameters. It is shorter, but a second change for the doc line and the named callers.
4. **Single-letter and offset constants: Constants.G, C and Kelvin = 273.15 (Constants.cs:8, :23, :25).** Recommendation: Rename them GravitationalConstant, SpeedOfLight and ZeroCelsiusInKelvin, each with an IOA0001 const forwarder (N3: full words; Kelvin is an offset, not a unit). Same values, so results stay bit for bit; compiled callers keep the inlined value. Alternative: Keep G and C as the CODATA symbols and rename only Kelvin. That leaves two one-letter names next to StefanBoltzmann and SolarMeanRadiativeLuminosity.

**Candidate defects found on the way.**

- **SideralRotationPeriod and TrueSolarDay truncate to the tick below through TimeSpan.FromSeconds.** CelestialBody.cs:255 and :407, both listed in #637. The rename to SiderealRotationPeriod must not change the value; the rounding fix moves results and belongs to its own PR. Tracked by #637.
- **Constants._2PI = fl(2π) is used as 2π when angles are brought into [0, 2π): 2.45e-16 rad per turn.** Constants.cs:13. TwoPi keeps the value bit for bit, so the rename fixes nothing here. Tracked by #471.
- **Constants.Kelvin is 273.15 where the NASA Glenn curve fits of the standard atmospheres take 273.1.** Constants.cs:25. The rename (open choice) keeps the value. Tracked by #438.

### R28. Cancellation by token, no async over sync, no empty IDisposable
**Gain** medium · **Cost** M · **Breaking** yes · **Results** unchanged unless cancelled · **C++** no · **Issues** #583, #584

**Today.** One token, BatchOptions.CancellationToken, read before each task (BatchPropagator.cs:85): a one-task 200-day batch cancelled at 100 ms threw after about 3.2 s (work/r04-r28/r28). Spacecraft and Star PropagateAsync are bare Task.Run; a token gives CS1501 (study/J-fdg-cross-cutting/s5). SimulateAsync awaits bodies one by one (Scenario.cs:206-216). PropagatorBase.Dispose is empty (:962).
```csharp
var task = sc.PropagateAsync(window, bodies, false, false, step); // no token
cts.Cancel();  // ignored
using (var p = new CentralBodyPropagator(window, sc2, new RK78Integrator(), bodies, false, false, step))
    p.Propagate();  // empty Dispose
```
**Proposed.** CPU-bound work is synchronous, `CancellationToken cancellationToken = default` last (D1): every Propagate, MonteCarlo, Batch, and a new Scenario.Simulate, sequential. VVIntegrator and RK78Integrator read it per accepted step (internal hook); another IIntegrator between segments. A batch gives one linked token to Parallel.ForEach and its tasks. After a cancel the spacecraft is partial; Spacecraft.Propagate resets it (Spacecraft.cs:552), a CentralBodyPropagator does not (#583). Propagators drop IDisposable.
```csharp
var solution = spacecraft.Propagate(window, options, cts.Token);
var summary = scenario.Simulate(options, cts.Token);
var batch = BatchPropagator.Propagate(tasks, cancellationToken: cts.Token);
```
**Breaks and migration.** IOA0001 until 12.0.0: Star, MonteCarlo, Batch PropagateAsync, 2 SimulateAsync; error level (D4): Spacecraft.PropagateAsync, SimulateAsync(drag, srp, step). BatchOptions token and Progress forward (D1), so running tasks stop too (batch-propagation.md:126). `using` on a propagator: CS1674 at 32 lines (5 library, 20 tests, 7 docs), a binary break.

**Tests.** Cancel after step 1, both integrators, a user IIntegrator, covariance, maneuvers, SSB: thrown within a step; then Spacecraft.Propagate equals an uncancelled run bit for bit. Batch: one OperationCanceledException, no AggregateException. Simulate equals SimulateAsync bit for bit; goldens unchanged under a live token.

**Results, in detail.** None for any uncancelled call: the check reads a token flag, and every golden stays bit for bit (tested under a live token). There is one behaviour change for a cancelled batch, through the new parameter and through the obsolete BatchOptions.CancellationToken alike: running tasks stop at their next accepted step. The spacecraft they were propagating are then partial. Today they run to their end (batch-propagation.md:126-127). Re-measured: a task cancelled at 100 ms threw OperationCanceledException at 3166 ms, its solution discarded.

**Depends on.** D1, D4. **C++ change.** no.

**Pull requests.** One PR with D1's options entry points: the token parameters, the internal integrator hook, the linked batch token, Scenario.Simulate, the obsolete async forwarders and the IDisposable removal.

**Open choices.**

1. **Scenario entry point.** Recommendation: A synchronous Scenario.Simulate(options = null, cancellationToken = default), sequential as today. D1's after code uses `await scenario.SimulateAsync(options)`, but SimulateAsync awaits each star's and each spacecraft's Task.Run in turn (Scenario.cs:206-216). It is async over sync, which the FDG advise against. Simulate also matches Spacecraft.Propagate, BatchPropagator.Propagate and MonteCarloPropagator.Propagate. The run stays sequential because a geopotential body is not thread-safe. Alternative: Keep SimulateAsync(options, cancellationToken) as the options entry point, with Task.Run inside. A UI caller saves one line, but every user then has two ways to do one thing.
2. **Where the per-step cancellation check lives.** Recommendation: An internal hook on the sealed VVIntegrator and RK78Integrator, set by PropagatorBase, read at each accepted step. Any other IIntegrator is checked between segments (the loop of PropagatorBase.cs:441-454). The public interface IIntegrator does not change. Alternative: Add a CancellationToken to IIntegrator.IntegrateSegment. Every user implementation then breaks (the verifier's note 1).
3. **IDisposable on propagators.** Recommendation: Remove it from IPropagator (IPropagator.cs:9), PropagatorBase and TLEPropagator. PropagatorBase.Dispose is empty (:962-964), TLEPropagator.Dispose only nulls two fields (:69-73), and CA1063 fires 4 times (study analyzers). `using` then gives CS1674 at 32 lines (5 in the library, 20 in tests, 7 in docs, by grep), each a mechanical deletion. It is a binary break: a compiled caller must be rebuilt. PropagationEphemerisCache, the other public IDisposable of the propagator, becomes internal in R04. Alternative: Keep a documented no-op Dispose. No user code breaks, but the API keeps a false resource contract, and no [Obsolete] can reach a `using` statement.
4. **State of the spacecraft after a cancel.** Recommendation: Document it: the spacecraft is partial (fuel, ExecutedManeuvers, standby chain, stored states). The next Spacecraft.Propagate resets it (ResetPropagation, Spacecraft.cs:552, :603, :629). A directly built CentralBodyPropagator or builder does not reset it: that is #583, not this section. Alternative: Roll back on cancel. This needs a snapshot of the fuel, the records, the chain and the stored states, and #583/#584 show that reset is already fragile.
5. **Where IProgress<BatchProgress> goes in BatchPropagator.Propagate.** Recommendation: An overload Propagate(tasks, IProgress<BatchProgress> progress, BatchOptions options = null, CancellationToken cancellationToken = default). It keeps D1's rule of options last before the token, and the common call needs no progress argument. Alternative: The BCL order (…, options, progress, cancellationToken). The reflection test of D1 then needs an exception for IProgress.

### R29. The CLI: one grammar, one parser, the library's defaults
**Gain** high · **Cost** L · **Breaking** yes · **Results** `propagate` moves (`-n`, Cd 2.2, D4), other numbers unchanged · **C++** yes (bodn2c_c) · **Issues** #652, #656, #657

**Today.** A bad value gives a stack trace; `-1000` is an unknown option; a step `60` is 60 days; `-t -u -l -d` runs (TimeConverterCommand.cs:32); `-n 2` and `-n 50` give one SPK (Helpers.cs:195-198); no `-s/-k/-q/-t` in `propagate` is a NullReferenceException (study/H-cli).
```csharp
astro ephemeris kernels 301 399 $T $T 1:00:00 ICRF LT+S // exit 1
astro ephemeris kernels -1000 399 $T $T 1:00:00 // exit 129
```
**Proposed.** Positionals: the objects (id or NAIF name); options alike everywhere: `-K` (repeatable; file, metakernel or folder; added to `ASTRO_KERNELS`), `--begin --end --step --frame --aberration --value --format`. One parser: enums by name or `[Description]`, help generated; a step needs a colon or a unit; SI/rad, or `km`/`deg`; epochs as D3 (bare seconds, else `Time.TryParse(s, TimeScale.TDB)`). Flags become enum options (`--from`, `--as`, D3's `--to/--format` and aliases; two of a group: exit 2). A TLE has no centre, epoch, frame. Errors: one line, exit 2 (input) or 3 (kernels) from the CLI's checks, else 1, `--verbose` the trace. `propagate` builds `PropagationOptions` (D1, D4), Cd 2.2, prints its NAIF id. Output `text` (ICRF `J2000`, epochs in the input's scale), `csv`, `json`.
```csharp
astro ephemeris MOON EARTH -K kernels --begin $T --end $U --step 12h --aberration LT+S
astro ephemeris -1000 EARTH -K kernels -K out ... --format csv
astro propagate Sat ... --integrator vv --step 1s --cd 0.3 --atmosphere standard // today
```
**Breaks and migration.** Order, flags, bare step, D3's lenient dates, exit codes, label scale: a table per command. `-K` needs R01 (SpiceAPI.cs:266 drops a sibling folder). Known limitations: no conjunction or CDM command; CSPICE searches only (GeometryFinderCommand.cs:52-205).
**Tests.** Via Cocona (none today): today's numbers bit for bit, strings as listed; every enum name and description parses; bad input: its code, one line; the `// today` line rewrites today's SPK byte for byte; defaults equal the library run bit for bit, Kepler/GMAT per D4; csv/json read back bit for bit.

**Results, in detail.** astro propagate only. (1) -n is applied: runs with a degree other than 10 move. Today -n 2 and -n 50 write byte-identical SPKs; re-checked on a 30 min LEO with cmp. (2) Cd defaults to 2.2 instead of 0.3 (PropagateCommand.cs:40, against Spacecraft.cs:134): drag x7.3 on runs with -a. (3) D4's defaults: RK7(8) at 1e-9 with a 60 s output step instead of Velocity-Verlet at 1 s (PropagateCommand.cs:62), and for the Earth NRLMSISE-00 nominal instead of EarthStandardAtmosphere (Helpers.cs:161); Mars keeps MarsStandardAtmosphere (Helpers.cs:165). D4 overrides the verifier's B-cli-propagate note, which kept VV at 1 s. How it is measured: each new run equals the library run with the same PropagationOptions bit for bit, and Kepler (a LEO day ends 1.36 m off under RK7(8), D4) or GMAT within D4's thresholds; CLI.Tests expectations are re-derived, not snapshotted. Today's SPK is rewritten byte for byte with --integrator vv --step 1s --cd 0.3 --atmosphere standard at the default degree 10. Output strings: 'j2000' becomes 'J2000' (#657). Orientation and window epochs are printed in the input's scale (UTC for a Z input) instead of TDB. That label can sit one tick off the TDB instant it shows. No other number moves: unsuffixed SI/rad values, colon-form steps and the CLI's documented epoch forms keep their meaning. D3's lenient DateTime forms are rejected.

**Depends on.** D1, D2, D3, D4, R01, R03. **C++ change.** yes.

**Pull requests.** Defect PRs first, each alone: -n ignored, --from-tle propagation, sub-point with a site id, the CelestialItem.ToString GM line, the kernel path length check, #652, #657; #656 in the library. Then PR A: the grammar, one value parser, errors and exit codes, -K/ASTRO_KERNELS (after R01's path matching), generated help (one description per find-windows command; the --version subcommand and the stdout banner go with R03), --format text|csv|json, D3's time-converter. Then PR B: propagate on PropagationOptions, landing with D4's defaults PR. The bodn2c_c proxy lands with the library's name lookup (SpiceApi.TryGetNaifId), P/Invoke and native in step, built by build-native on the three platforms, with a C++ test and a .NET test.

**Open choices.**

1. **Body names as well as NAIF ids.** Recommendation: Add SpiceApi.TryGetNaifId(string, out int) over a new bodn2c_c proxy, called under the shared lock. Proxy.cpp:529 has only bodc2n_c, and the public API has no name lookup. The proxy knows DSN stations, spacecraft and every name a kernel defines, and the library gains it too. This is a C++ change for an ergonomic reason, as #667 allows. Agree the signature in the library area. Alternative: Ids only, documented in the help. No C++ change; MOON or EARTH stays rejected.
2. **Default time scale of printed epochs.** Recommendation: The scale of the input epoch: a Z input gets UTC labels, and TDB seconds keep TDB, as ephemeris does today. The labels are for display. Document that a re-fed UTC label can sit one tick off the TDB instant. --time-scale TDB gives today's orientation and window strings bit for bit. Alternative: Always TDB, the library's label scale. Orientation and windows do not change, but ephemeris keeps echoing UTC, so the inconsistency stays.
3. **How a user keeps today's drag results of propagate.** Recommendation: --atmosphere nrlmsise00|standard for an Earth centre, default nrlmsise00 (D4). Mars keeps its model, and the option given for another centre is exit 2. With --integrator vv --step 1s --cd 0.3 --atmosphere standard at degree 10, today's SPK is rewritten byte for byte. That run is also the bit-for-bit test. Alternative: No option: the Earth always takes NRLMSISE-00, and today's drag runs cannot be reproduced from the CLI (stated in the release notes).
4. **Machine-readable formats.** Recommendation: text, csv and json. csv serves the tabular commands (ephemeris, orientation, windows) and json the record commands (celestial-body-info, sub-point). Values use round-trip digits and unit-suffixed field names, with one writer per format. Alternative: text and json only: one structured format fewer to test.
5. **Compatibility with the old positional forms.** Recommendation: No shim, beyond the -t -u -l -j -e -d aliases of time-converter that D3 keeps. Two grammars per command cannot stay consistent, 11.0.0 is a major release, and the CLI prints 'experimental' on every run. The release notes carry an old-to-new table for every command, propagate's positionals included. Alternative: Accept the old forms for one release, with a warning on stderr.

**Candidate defects found on the way.**

- **astro propagate ignores --earth-geopotential-degree: every run uses degree 10.** PropagateCommand.cs:35 declares earthGeopotentialDegree and never reads it. Helpers.cs:195-198 calls ConvertToOrbitalParameters without it, so the default 10 applies (:145, :160). The SPKs of -n 2 and -n 50 are byte-identical (study/H-cli outn2 and outn50; re-checked on a 30 min LEO, cmp). PropagateTests.PropagateWithPerturbations passes degree 20 but asserts only 'Propagation completed'. The help '(Max. 100' is unclosed. Fix it alone. Results move for every -n other than 10, measured against the library run with GeopotentialModelParameters of that degree. No issue yet.
- **astro propagate --from-tle always throws 'must be defined in inertial frame'.** The TLE state is in TEME, which PropagatorBase.cs:948-958 rejects. No CLI test sets FromTLE. Verifier correction: ToOsculating().ToFrame(ICRF) alone is not enough. A TLE builds its own Earth with no geopotential and no atmosphere, so -n and drag would be dropped again. The state must be rebuilt on the CLI's Earth. Test against the library propagation from that state. No issue yet.
- **astro sub-point throws NullReferenceException for a site id that ephemeris accepts.** `sub-point kernels 399013 399 ...` throws NullReferenceException: EphemerisCommand.cs:93 casts CreateLocalizable(...) as CelestialItem and dereferences it with '!' at :98. `ephemeris` accepts the same id (verify/H). It should give a one-line error that names the accepted object kinds. No issue yet.
- **CelestialItem.ToString prints GM with a wrong unit and 7 significant digits.** celestial-body-info 399 prints 'GM (m^3.s^2): 3.986004E+014' (CelestialItem.cs:1095, format 'E'). The unit is m^3/s^2, and the value 3.986004354e14 loses its last digits. 'Mass (kg) : 5.972168E+024' (:1094) does the same. This is the library's ToString, so it gets its own issue. No issue yet.
- **propagate writes kernel paths longer than 255 characters that SPICE cannot load, and the message points to stdout.** The Cosmographia layout adds about 70 characters (Mission<n>_Scenario<n>/Spacecrafts/<n>/Ephemeris/<n>.spk). The managed SpkWriter wrote a 297-character path without complaint, and furnsh then fails with SPICE(FILENAMETOOLONG) (verify/H). The .NET message says 'You can have more details on standard output' (SpiceAPI.cs:291). PropagateTests works around it with a short output directory. Check the length before writing; the stdout part goes with R03. No issue yet.
- **#652 coordinate-constraint help lists values that do not exist, and RA/DEC and RIGHT ASCENSION are rejected.** GeometryFinderCommand.cs:25/27 list LATITUDINAL, SPHERICAL, CYLINDRICAL and COLATITUDE. Lines 48-49 parse member names only. Reproduced by the study and the verifier. Tracked by #652.
- **#657 the ephemeris command labels ICRF states 'j2000' or 'J2000' depending on the observer.** EphemerisCommand.cs:38-41, OrientationCommand.cs:27-30 and GeometryFinderCommand.cs:41-44 map ICRF to 'j2000'. FrameConverterCommand.cs:30/35 and a site observer print 'J2000'. Reproduced: 'Frame : j2000' for 301 from 399. Tracked by #657.
- **#656 CIRS and TIRS built by name are not routed to the managed frames.** `frame-converter kernels ICRF CIRS` throws 'SPICE gives no transformation from CIRS to J2000'. The fix is in the library (new Frame("CIRS") routed to Frame.CIRS); the CLI adds no frame router of its own (verifier note). Tracked by #656.

## C. Resolutions of the completeness review

A final reviewer read every section against the others. Each contradiction below is resolved as stated; the resolution prevails. The textual fixes of C1 (the TimeScale casing in D3), C3 (D4 rule 2), C6 (the search examples of R11 and R16), C15 (IOA0002 in R07 and R26), C17 (the R27 example) and C2 (the entry points of R06) are already applied in sections A and B; the others are applied by the implementation PRs.

**C1** (D2, D3, R24, R29, R15, R25). Time scale casing. D3 rule 1 writes enum TimeScale { Tdb = 0, TT, Tai, Utc, Gps }, its messages use 'TimeScale.Tdb', 'TimeScale.Utc', its CLI sub-choice uses Time.TryParse(s, TimeScale.Tdb), and its Local message uses t.ToUTC(). D2 N2 makes the members symbols (TimeScale.UTC, TT, TDB) but does not list TAI and GPS. D2 N1 makes coined methods ToUtc/ToTdb. R24 and R29 already write TimeScale.UTC and TimeScale.TDB. R15's results text uses ToTDB(). R25 still writes Frame.ECLIPTIC_J2000, which D2 renames to Frame.ECLIPJ2000.

*Resolution.* Write enum TimeScale { TDB = 0, TT, TAI, UTC, GPS } and add TAI and GPS to N2's value list. Rewrite D3's messages to name TimeScale.TDB and TimeScale.UTC, and the Local message to t.ToUtc().DateTime.ToLocalTime(). Write ToTdb() in R15 and Frame.ECLIPJ2000 in R25. NamingRuleTests then checks the whole list.

**C2** (D1, R06, R28, R07). Async entry points. D1's after-code awaits scenario.SimulateAsync(options), and R06 puts the PropagationOptions record on 'Propagate(Async)' and 'SimulateAsync'. R28 makes CPU-bound work synchronous: it adds Scenario.Simulate(options, cancellationToken) and obsoletes Spacecraft.PropagateAsync (error level), Star/MonteCarlo/Batch PropagateAsync and both SimulateAsync. R07's error message for PropagateAsync points to Task.Run of Propagate, which agrees with R28 and not with R06.

*Resolution.* The options entry points are Spacecraft.Propagate, CentralBodyPropagator, Scenario.Simulate, PropagationTask, MonteCarloConfiguration.Propagation and BatchPropagator.Propagate, each with `options = null, CancellationToken cancellationToken = default`. No new async overload takes PropagationOptions. Correct D1's after-code and R06's list.

**C3** (D1, D4, R07). Integrator factories. D1's integrator sub-choice removes Func<Integrator> from Scenario, PropagationTask and MonteCarlo. D4 rule 2 instead drops '= null' from PropagationTask.IntegratorFactory and makes MonteCarloConfiguration.IntegratorFactory required. R07 takes a third path: an error-level 6-parameter PropagationTask constructor, and MonteCarlo's DeltaT, CelestialBodiesFactory and IntegratorFactory lose `required` and become IOA0001-obsolete. D4's form would force options-path users to set an obsolete member.

*Resolution.* Adopt R07 and rewrite D4 rule 2 and its sub-choice to match. Func members are obsolete forwarders, never required, and the options path has no factory.

**C4** (D1, D4, R06, R17). Two shapes for one covariance setting. PropagationOptions.Covariance is CovariancePropagationOptions? with null = Auto (D4, R06). ConjunctionAnalysisOptions.CovariancePropagation is a non-null initializer whose null throws ArgumentNullException (R17). Name, type, default and check all differ, against D1 rule 7. Two inference rules also overlap. D1 resolves Mode = null from ProcessNoise and ConsiderParameters only (Covariance when either is set, else StateTransitionOnly). D4's Auto resolves from the integrator and P0 (P0 at the window start, PSD, RK7(8)). A record with Mode null and a P0 present has no stated result. Separately, ScreeningOptions.Analysis defaults to None at a 5 min step, so ScreeningOptions.Default.Analysis != ConjunctionAnalysisOptions.Default, and no section says so.

*Resolution.* One property, Covariance (CovariancePropagationOptions?), on PropagationOptions and on ConjunctionAnalysisOptions. Null = Auto, and R06 gives one resolution table for Covariance null and for Mode null with P0, ProcessNoise and ConsiderParameters as inputs. ConjunctionAnalysisOptions.CovariancePropagation becomes an IOA0001 forwarder that maps null to CovariancePropagationOptions.None. Today's 'null = held in RTN' then keeps its meaning without a runtime throw in compiled binaries. State in ScreeningOptions' XML that its Analysis default differs from ConjunctionAnalysisOptions.Default, and why.

**C5** (D1, D4, R17). Names of the hard-body-radius fallback. Today there are three properties with two names: ConjunctionAnalysisOptions.SecondaryHardBodyRadiusMeters (ConjunctionAnalysisOptions.cs:67), AvoidanceSearchOptions.SecondaryHardBodyRadiusMeters (AvoidanceSearchOptions.cs:49) and ScreeningOptions.DefaultSecondaryHardBodyRadiusMeters (ScreeningOptions.cs:36). D1 cites the Default... name as the model of a fallback. D4 makes SecondaryHardBodyRadiusMeters double? 'on all three options types'. R17 then obsoletes the mirrored subsets once Screening and Avoidance contain an Analysis. The defect PR would change the semantics of two properties that R17 deprecates in the same release.

*Resolution.* ConjunctionAnalysisOptions.SecondaryHardBodyRadiusMeters (double?, null = per D1 rule 6) is the one fallback, reached through ScreeningOptions.Analysis and AvoidanceSearchOptions.Analysis. The other two become IOA0001 forwarders in R17 PR 5. D4's defect PR changes semantics only, not names. Correct D1's example name.

**C6** (R15, R16, R11). The search examples contradict R15. R16 calls site.FindElevationWindows(...), a shortcut R15 rejects ('one more member for the same thing'). R16 also calls camera.FindFieldOfViewWindows(window, site), and R11 calls site.FindIlluminationWindows(..., 90.0, sun), both without options. R15 keeps Step required for FOV and threshold searches (null throws ArgumentException naming Step), so R11's '// throws' would be the Step error, not the angle-range error it is meant to show.

*Resolution.* In R16, write site.FindHorizontalWindows(window, sc, HorizontalCoordinate.Elevation, RelationalOperator.GreaterThan, 0.0, o) and camera.FindFieldOfViewWindows(window, site, FieldOfViewSearchOptions.Default with { Step = ... }). In R11, pass an options record with Step, so the throw shown is the [0, pi] range check. R05's docs job catches the rest.

**C7** (D1, R15). R15 lets an obsolete positional search overload ignore the Aberration of its options record ('ignored there, documented'). That breaks D1 rule 2, 'set where it cannot apply, it throws'. Aberration is a non-nullable initializer (default None), so 'set' cannot be detected.

*Resolution.* Choose one. Either the obsolete positional overloads take no options record, or Aberration becomes Aberration? (null = None) on the three search records, so the obsolete overload throws when it is set. The siblings stay identical either way (D1 rule 7).

**C8** (D1, R18). R18 keeps ReadRangeRateOverWindow a bool that throws when true where the sampled search runs. D1 rule 2 says a setting that applies to some calls only is nullable. As written, the reflection test of D1 would reject R18's choice.

*Resolution.* Amend D1 rule 2: a bool whose non-default value applies to some calls only may stay bool, and true throws where it cannot apply. Encode the rule in the reflection test (allow-list: ReadRangeRateOverWindow).

**C9** (R02, R04, R25). Which data provider the statics use. R02 tags each cached catalogue body with the provider instance, 'R04's scoped provider' included, so EarthBody rebuilds inside a UseDataProvider scope. R04 says the static catalogues and the static frames always use the process provider, never the scoped one. R25 keeps Frame.ITRF93, which, like every static Frame, captures the provider at first use and never changes it (Frame.cs:156, :180, :191). After SetDataProvider, EarthBody follows the new provider and Frame.ITRF93 does not.

*Resolution.* One rule. The catalogue cache is tagged with the process provider and the kernel generation only, and a scope does not reach it (R04's text). For the static frames, either read Configuration.Instance.DataProvider at each transform instead of capturing it (results unchanged while the provider is not swapped), or state in R02 and R25 that static frames freeze their provider while catalogue bodies follow SetDataProvider. Recommend the first.

**C10** (R04, R25). MemoryDataProvider exception types. R04's defect PR turns 'missing data' into InvalidOperationException, as SPICE does (the missed item cites MemoryDataProvider.cs:64, :69, 'a missing state or frame'). R25 relies on MemoryDataProvider.cs:34 keeping ArgumentException for an unknown frame, so that both providers agree with SPICE(UNKNOWNFRAME) mapped to ArgumentException.

*Resolution.* Write the IDataProvider exception contract once, in R04, and R25 uses it. An unknown name (frame or body) gives ArgumentException. A known item without data at the epoch gives InvalidOperationException (SpiceException on SPICE). R04's defect PR changes only :64 and :69 and the coverage check after them. :34 stays ArgumentException.

**C11** (R06, R11, D1, D4). R06 PR 2 says 'results unchanged', and D1's tests require each options path to equal its obsolete path bit for bit. R11 puts the new options output grid (start + i*dT, then the window end) in that PR. When the step does not divide the window, the grid moves the last output, the last SPK record, the CK and the attitude record at the end.

*Resolution.* Move R11's options grid into D4's defaults PR, where moved figures are stated and long-validation.yml runs. R06 PR 2 keeps the legacy grid on both paths and stays bit for bit.

**C12** (R07, R29, R28). The CLI in D4's defaults PR. R07 moves PropagateCommand.cs:62 to new VVIntegrator(step), which keeps VV at 1 s. R29 PR B, 'landing with D4's defaults PR', moves it to RK7(8) on PropagationOptions. Neither section calls R28's Scenario.Simulate. The SimulateAsync(drag, srp, step) member it calls becomes an error in that PR.

*Resolution.* One change in D4's defaults PR: PropagateCommand builds PropagationOptions and calls Scenario.Simulate (R28, landed earlier), with --integrator vv --step for VV. If R29 PR B cannot land there, R07's interim new VVIntegrator(TimeSpan.FromSeconds(1)) is stated as such.

**C13** (R24, R29, D3). Three owners for the CLI epoch parser (Helpers.cs:93-142). R24 says it lands in D3's CLI time-converter part, R29 PR A lists 'D3's time-converter', and D3 rule 7 owns time-converter.

*Resolution.* The parser and time-converter land in D3's time PR (R24), the one place where Time.TryParse appears. R29 PR A reuses it and adds nothing to epochs.

**C14** (R21, R22, D1). Two verb sets for text formats. Tle gets Parse, TryParse, IParsable, LoadAll(path) and ParseAll(TextReader). Omm/Opm/Cdm get Load, Parse, LoadAll, TryLoad, Save, ToXml, Validate and IsValid, with no TryParse and no ParseAll. D1's Try* rule defines the (path, out, out) and (path, options, out, out) pair only for paths.

*Resolution.* One verb set for Tle and the CCSDS messages: Load, LoadAll, Parse, ParseAll, TryLoad, TryParse. Parse and ParseAll take a string or a TextReader, the Try* forms follow D1's pair, and CCSDS takes CcsdsReadOptions. NamingRuleTests checks the set.

**C15** (D1, D3, D4, R09, R21, R26). D1 rule 9 makes every replaced member a warning-level [Obsolete] under one DiagnosticId. D3, D4, R09 (GetTotalFuel), R21 (Tle.MeanMotion()) and R26 (Matrix.Set) add error-level obsoletes, 'with D1's DiagnosticId'. Rule 9 lists no such class, so the release-note table and NamingRuleTests ('each IOA0001 forwarder is bit for bit its target') cannot tell a rename from a trap. Tle.MeanMotion() is the case where the error member is not the same as its replacement.

*Resolution.* Amend D1 rule 9 with a second class: error-level traps under their own id (IOA0002), each with a message naming the fix, listed under Breaking. IOA0001 stays for warning forwarders only, and the forwarder test applies to IOA0001.

**C16** (R17, D1). Churn inside R17. PR 3 makes AvoidanceSearchOptions.AnalysisSampleStep, MaximumEventSearchStep and LowRelativeSpeedThresholdMetersPerSecond nullable (null = the encounter's), a public change. PR 5 then contains a ConjunctionAnalysisOptions in AvoidanceSearchOptions.Analysis and obsoletes those mirrored values. Two mechanisms for one inheritance ship in successive PRs of one release.

*Resolution.* PR 3 makes the avoidance inherit the encounter's resolved options internally, with no public shape change. PR 5 adds Analysis (null = the encounter's) and obsoletes the mirrored values as they are.

**C17** (R27, R10). R27's after-code builds new Spacecraft(-1001, "Sat", 500.0, 1000.0, clock, ke, dragCoefficient: 2.2, ...). That is the max-mass-and-clock constructor R10 makes IOA0001-obsolete, so the example renames parameters on a member the user is told to leave.

*Resolution.* Write new Spacecraft(-1001, "Sat", 500.0, ke, maxMass: 1000.0, dragCoefficient: 2.2, solarRadiationCoefficient: 1.3) on R10's constructor. Rename the parameters of both constructors in the same PR.

**C18** (R06, D4). ThirdBodies = null means 'none, the central body only' on Spacecraft.Propagate (D4 rule 4) and 'the scenario's AddCelestialItem bodies' on the scenario run (R06). One PropagationOptions value reused across the two entry points therefore gives two force models.

*Resolution.* Keep R06's input-dependent inference (D1 rule 2 allows it), but put the per-entry-point table in the ThirdBodies XML doc and expose the resolved list on PropagationSolution.Options (D1 rule 10). The bit-for-bit test covers the same options on both entry points.

## D. Plan of short PRs

| PR | Content | Sections | Why here |
|---|---|---|---|
| P01 Infrastructure | PublicApiAnalyzers with PublicAPI.Shipped.txt seeded from main (no #nullable header), AnalysisModeNaming=All as warnings, NamingRuleTests with a baseline, the IOA0001 and IOA0002 ids, and D1's reflection test with an allow-list baseline. | D1, D2, R27 | Every later PR records its surface diff, forwarders and naming against this baseline. It changes no API. |
| P02 Docs job | R05 PR 1: the docs-snippets job (each page one program, #line, HTML markers), a shrink-only baseline of failing pages, #662's fixes and the 30 hidden lines. | R05 | Each API PR after it must fix and prove its own doc lines. It lands before the first rename. |
| P03 Defects, each alone | #640 first. Then, in any order, each with its own issue and measurement: the GetCelestialBodyInfo defects (GM without RADII, unknown id), RK78 NaN tolerances, step <= 0 throws (R11 PR 1), single-use GeopotentialModelParameters, the secondary HBR, dropped drag, GetAtmosphere vs AtmosphericModel, central-body twin on the constructor path, #583, #584, #586, #527, the foreign trajectory in Analyze, the self-secondary, #656, MemoryDataProvider missing-state type, the instrument frame id, the unattached tank, the factory axes, Time.Create validation and AddHours(NaN), the FOV empty-attitude throw, OPM CENTER_NAME, OMM TLE metadata, the multi-message NDM, #617, #339, and the CLI defects (-n, sub-point id, kernel path length, #652, #657). | R03, R06, R08, R09, R10, R11, R13, R15, R17, R18, R22, R24, R25, R29, D4 | CLAUDE.md requires a defect to get its own PR. Each moves results that must be measured before any API change lands on top. D4 orders them before the defaults PR. |
| P04 Native error contract | R03 PR 1: bool + lastError with SPICE's short and long messages on every proxy .NET calls, zeroed DTOs, caller-sized search cells (#653), SpiceException : InvalidOperationException, the 6 unused exports removed. build-native on three platforms. Then R03 PR 2: the library's exception types. | R03 | R01, R25, R29 and R02's error paths name SpiceException and its ShortMessage. P03's GetCelestialBodyInfo fixes must land first, or the 8 bodies with a GM and no RADII throw. |
| P05 Kernel registry | R01-a: string overloads, ReloadKernels, registry-based UnloadKernels, ordinal folder order, throws on missing paths, the internal load scope, and the ktotal_c/kdata_c proxy. C++ in step. | R01 | R02's generation counter, R14's scoped load and R29's -K all need the registry. It needs P04's LoadKernelsProxy message. |
| P06 Frames by name | R25: SPICE(UNKNOWNFRAME) mapped to ArgumentException, Frame.ITRF93, with R04's provider exception table written here. | R25, R04 | It is the cheapest section, small, and it depends only on P04 and #656. |
| P07 Time | D3/R24 in one PR with D2's TimeSystem renames: TimeScale (TDB, TT, TAI, UTC, GPS), required-scale constructors, CS0619 traps, TDB-tick equality, Parse/TryParse, IFormattable, nearest-tick Add*, the CLI time-converter and epoch parser, and the Constants forwarders. | D3, R24, D2 | R11, R15, R16, R21, R22, R23 and R29 build on Time.Parse and Time equality. The 374 rewritten test lines (255 calendar, 119 Time(string)) should move once, before other PRs touch the same files. long-validation.yml runs here. |
| P08 Catalogues | R02 with D2's 411-row catalogue block: data-only ids, *Body properties over the generation cache tagged with the process provider, NaifObject : INaifObject without Frame, and the #623 rule rewritten. | R02, D2 | It needs P05's generation counter. R21's lazy Earth and R22's CENTER_NAME table need the data-only ids. |
| P09 Public surface | R04 PR 1 and PR 2 (DTO internal, CelestialBodyInfo, the IDataProvider contract, the getter, UseDataProvider, internal helpers, readonly event structs) and R20-a (StateVector returns on ILocalizable, IDataProvider, ReadEphemeris). | R04, R20 | Both break IDataProvider. One PR makes custom providers change once. |
| P10 Math and equality | R26 (immutable Matrix, exact element equality, Vector3 operators), then R23-a/b (constructors, guards, equality per #475). | R26, R23 | R23's covariance equality needs R26's Matrix equality. Its Time equality needs P07. |
| P11 Propagation options | R06 PR 2: GeopotentialModelParameters as a value, PropagationOptions with one covariance resolution table, the IntegratorOptions records, one force builder, and the IOA0001 forwarders. Legacy grid kept, bit for bit. | R06, D1 | Every later options PR (R07, R08, R12, R28) builds on it. It needs the P03 defects so the forwarders reproduce fixed behaviour. |
| P12 Cancellation | R28: token parameters, the internal integrator hook, Scenario.Simulate, the obsolete async forwarders, and IDisposable removed. | R28 | It needs P11's entry points and #583 (P03). R07's error messages point to Scenario.Simulate. |
| P13 Runs and maneuvers | R08 PR 5 with R13: SetManeuvers, ComputeDeltaV, TryExecute sealed, the maneuver and attitude constructors with optional named parameters, BodyAxis, the radius names, and TLEPropagator replacing its states. Then R09 PR 2 (GetStateVector as the one reader, solution.Maneuvers, the fuel accessors). | R08, R13, R09 | It needs #583, #584, #586 and #527 (P03) and P11's options. |
| P14 Spacecraft construction | R10: the optional max mass and clock, AddX returning its argument, default instrument ids, the same-name checks. | R10 | R13's engine inference and R22's Opm.ToSpacecraft defaults read the same constructors. It lands in D2's lot. |
| P15 Defaults | D4's defaults PR: R07 (RK7(8) default, VVIntegratorOptions, the error-level traps, 75+ call sites), R06 PR 3 (central body always, NRLMSISE-00 Earth, covariance Auto), R11's options grid, and R29 PR B (CLI propagate on PropagationOptions via Scenario.Simulate). long-validation.yml runs, and every moved figure is stated. | D4, R07, R06, R11, R29 | All forwarders exist and are bit for bit, so every moved number is attributable to the defaults alone. |
| P16 Searches | R15-1 (three search records, observer extensions, labels in the window's scale, the inferred occultation step, the half-period guard) with R16's IReadOnlyList return and R11's angle ranges. Then R15-2 (FindHorizontalWindows, CoordinateEvaluator, static FindWindows) and R11 PR 2 with the rest of R16 (Window checks, WindowList, Sample). | R15, R16, R11 | It needs P07's Time equality and P08's RelationalOperator names. It is independent of the propagation chain. |
| P17 TLE | R12 PR 1 (TLE and TEME starts, read rule, CLI -t with today's options) and R21 PR 1 and 2 (Parse and the verb set shared with CCSDS, lazy Earth, the MeanMotion() trap). | R12, R21 | It needs P11, P13 (the replace rule, #527) and P08 (lazy Earth). |
| P18 Conjunction and CDM | R19 PR 1 (Cdm.ToStateVector, ToEncounter, CollisionProbability; the runner drops StraightLineEncounter), R17 PR 5 with R18 (Analysis containment, Spacecraft overloads, EncounterCase shortcuts and Search, one Covariance property), then R19 PR 2 (TrackedObject). | R19, R17, R18 | It needs the HBR, foreign-trajectory and self-secondary defects (P03), P11's covariance table and P17's Tle. |
| P19 CCSDS verbs | R22 PR A (CcsdsReadOptions, CcsdsWriteOptions, one verb set shared with Tle, old verbs obsolete) and PR B (LoadAll, ToMeanElements, Omm.ToSpacecraft, GetEpoch). | R22 | It needs the CCSDS defects (P03), P04's CcsdsFormatException, P08's name table and P18's Cdm surface. |
| P20 Kernel export | R14: Spacecraft.WriteKernelsAsync and SpacecraftKernels, with CosmographiaExporter on the same internal sequence. | R14 | It needs P05's scope and registry, P12's token and #546. |
| P21 CLI grammar | R29 PR A: one grammar and one value parser, exit codes, -K/ASTRO_KERNELS, generated help, --format text/csv/json, and the bodn2c_c proxy (SpiceApi.TryGetNaifId, C++ in step). | R29 | It needs P05 (-K), P07 (epochs) and P15 (propagate), and it is the last user-facing behaviour change. |
| P22 Renames and close-out | R27's rename PRs per area (orbital elements and TLE glossary, bodies and spacecraft parameters, Constants and Frame statics), each running the sed script on the tests and docs. Then D2's final PR (analyzers as errors, baseline emptied, tables generated), R03 PR 3 (errprt NONE, CLI banner and stderr), and R05 PRs 2-4 (DEVELOPER_GUIDE, GenerateDocumentationFile after #556, cli.md). | R27, D2, R03, R05 | Renames come last, so each user line changes once. errprt NONE needs every proxy message from P04 to P21. |

## E. Dependencies the sections did not state

- Every API PR depends on D2's infrastructure PR (PublicAPI files, IOA0001) and on R05 PR 1 (the docs-snippets job with its shrink-only baseline), so that each one proves its doc lines. Only R27 lists R05. Add R05 to R06, R07, R10, R13, R15, R17, R20, R21, R22, R24, R26 and R29.
- R14 depends on R01. 'Dispose unloads what the call loaded, not a prior load' is R01's internal scope, 'a directory inside a loaded kernel path' needs R01's component-wise path registry (today's substring match, SpiceAPI.cs:267-283), and both sections rewrite CosmographiaExporter.cs:316-334. R14 also depends on R28 (its cancellationToken parameter), on R03 (the #655 check) and on #546 (R12 PR 2) for the TLEPropagator refusal.
- R21 depends on R02. A valid Tle still builds new CelestialBody(399, Frame.ECLIPTIC_J2000, epoch) in its base call (TLE.cs:176-177) before any check. Without kernels, Parse, NoradCatalogId and ToOmm throw and CSPICE prints banners (verifier, verify/F/v1-nokernels). R21's 'text checked before SPICE' and 'kernel-free child' cover bad text only. A valid set read without kernels needs a lazy Earth over R02's cache. No section covers this missed finding.
- R12 depends on R09 (#527). A propagated TLE spacecraft reads its stored states and throws outside them, while an unpropagated one reads SGP4 everywhere. R12 also needs a source flag on the stored states, so that 'TlePropagator-sampled reads SGP4' (R12) and 'TLEPropagator replaces the stored states' (R08 PR 5) can both hold. Which rule wins after TlePropagator and then Spacecraft.Propagate is not stated.
- R12 and R29 form a cycle. R12 depends on R29 for the CLI's Earth options, and R29's '--from-tle propagation' defect PR is R12 PR 1. Break it: R12 PR 1 makes `astro propagate -t` run with today's CLI options, and R29 PR B later moves it onto PropagationOptions.
- R22 depends on R03 PR 2 (CcsdsFormatException is what Load and TryLoad throw and catch) and on R19 PR 1. Both change Cdm.cs's public surface. Land R19 PR 1 with R22's verb names, or after R22 PR A.
- R19 depends on R21: TrackedObject.FromTle takes the renamed Tle and its NoradCatalogId up to 339999.
- R23-b depends on R26. StateVector equality compares the covariance through Matrix.Equals, which tests |a - b| > Double.Epsilon (Matrix.cs:360). A NaN entry then equals any value, while GetHashCode (Matrix.cs:370-381) differs, so the Equals/GetHashCode contract fails. R26 should compare elements with double.Equals, in its own defect PR, before R23-b relies on it.
- R29 depends on R24/D3 (the epoch parser), R28 (Scenario.Simulate), R12 (-t), R06 and R07 (PropagationOptions and the defaults). Its depends_on lists none of them.
- R07's error-level obsolete on Scenario.SimulateAsync(drag, srp, step) needs R28's Scenario.Simulate to exist first. Otherwise its message would point to a SimulateAsync options overload that R28 obsoletes.
- R28's documented cancel state ('a CentralBodyPropagator does not reset it, #583') follows R08 PR 1, which moves the reset into PropagatorBase.Propagate. R28 must land after R08 PR 1 and say that every path resets.
- R11 PR 2's hold check goes into Maneuver constructors that R13 replaces. Put the check in R13's new constructors as well, or land R11 PR 2 after R13.
- R15 must return IReadOnlyList<Window> (R16's decision) in its own PR. R15 does not list R16, and R16 says its return type 'lands with R15's search PR'.
- R18's owner check (PropagatorBase sets an internal owner) and R09 PR 2 (PropagationSolution constructors internal) touch the same type. Land the owner check first, so that R09 keeps owner-less solutions for the runner through IVT.
- Uncovered (R11 defers to R26, which says nothing): Vector3.Normalize() and Quaternion.Normalize() return zero unchanged (Vector3.cs:31-40, Quaternion.cs:43-52). Either R26 adds the rule (throw, or NaN) with its moved results stated, or it gets its own issue.
- Uncovered (R15's verifier): an FOV search on a managed spacecraft with an empty attitude record uses the identity (SpacecraftFrame.cs:94-96) and returns 5 windows. WriteOrientation throws there (SpacecraftFrame.cs:439-440). This needs its own defect PR before R15-1.
- Uncovered (R17's verifier): a UTC window over the same span as a TDB propagation throws in Spacecraft.CheckInterpolatedStatesCover (Spacecraft.cs:818), because one UTC hour is 1.2 us longer in TDB. It depends on D3 and R11's mixed-scale windows. It needs its own issue.
- Uncovered (R20's verifier): ToEquatorial and new Equatorial(StateVector) ignore the state's frame (OrbitalParameters.cs:970-974, Equatorial.cs:28-37). An ITRF93 state gives RA 20.525 deg against 0 deg in ICRF. R20 should rotate to ICRF first, or file it, with the moved results stated.
- Uncovered (R03's verifier): a site cannot be the observer of SpiceApi.ReadEphemeris (SpiceAPI.cs:846), while the coordinate search accepts it. Assign it to R02 (#651, Site ids) or to its own issue.

## F. Claims to measure again in the implementation PRs

- R17 says 'the 64 calls of CreateSpacecraft' (ConjunctionAssessmentTests.cs:2259-2267) and D4 says '67 calls'. grep finds 67 occurrences of 'CreateSpacecraft(' in ConjunctionAssessmentTests.cs, the definition included, and the helper name also appears in 8 other test files. Recount the calls whose Pc moves, and state one figure in the HBR defect PR.
- R23 relies on 'Matrix is a record struct with value equality' for covariance equality. Matrix.Equals tests Abs(a - b) > Double.Epsilon (Matrix.cs:360), so a NaN entry equals any value, and GetHashCode then differs (Matrix.cs:370-381). #475's matrix over 20 000 pairs does not include NaN covariances, so the claim needs that case.
- R24 says 'no library result moves' while Time.ToString writes TT instead of TDT, and R27 says the written files stay bit for bit. Neither shows a scan of the writers (CCSDS COMMENT lines, Cosmographia JSON, kernel comments, CLI output) for a TT Time formatted through ToString. Add the grep, or a byte comparison on a TT input, to the PR.
- R11's results_change says 'None on any existing signature', but its PR 2 makes the existing Window constructors throw on reversed ends or a negative length (today they swap, Window.cs:25-28). Calls that run today then throw, and the CLI reports end-first windows (GeometryFinderCommand.cs:46, Helpers.cs:209) as errors. State it in results_change, not only under Breaks.
- R06 and D4 state covariance Auto as 'trajectory unchanged (0.000 m) at 1.8 times the cost'. That is one measurement of one case. Every new-path user who gives P0 pays the cost, and in SSB mode the RK7(8) default already runs at 118 to 372 ms per hour. Measure Auto in SSB mode and with a geopotential before the defaults PR.
- R12's 'ScenarioTests.cs:300 kept (SGP4)': the SimulateAsync call there is commented out (ScenarioTests.cs:299). The test reads an unpropagated TLE spacecraft, so it proves nothing about 'stored states after SimulateAsync'. R12's new SimulateAsync and Simulate tests are required, not optional.
- R10 reports 177 of 313 'new Clock(' calls at 65536 and 132 at 256. That accounts for 309. Name the other 4, since the default resolution is chosen from these counts.
- R19's NAIF id default (-100000 - NORAD, mkspk), R19's RTN axes of an ITRF state (CCSDS 508.0-B-1) and R22's SGP/SGP4 value (CCSDS 502.0-B-3) carried 'to be verified by S. Guillet' (all three confirmed by S. Guillet, 2026-11-13). CLAUDE.md forbids a marker at the release, so each needs a confirmation date before the 11.0.0 tag, or the choice that depends on it falls back to its alternative.
- R15's 'ToTDB() of each bound is today's TDB tick, except at about 6 in 5 million ticks' is quoted from D3's measurement of TT/TDB round trips. R15's labels go ET to UTC to TDB on the CSPICE path, which includes the leap-second table. The count should be measured on that chain, 1972-2100 and on a leap-second day, as R15's own test list says, before it is published.

## G. Candidate defects without an issue

Found by the study and its verifiers, each outside the scope of an API change: per CLAUDE.md each gets its own issue and its own PR. Evidence in section B under the recommendation named.

| # | Defect | Section |
|---|---|---|
| 1 | LoadKernels and UnloadKernels match paths by substring: a folder whose name extends a loaded one is never loaded, and loading or unloading the shorter one unloads it | R01 |
| 2 | UnloadKernels is a silent no-op on a deleted or missing path while SPICE keeps the data; unloading a file of a loaded folder leaves the folder listed | R01 |
| 3 | The CLI wraps every kernels argument in DirectoryInfo: a meta-kernel or a single kernel file is ignored without an error | R01 |
| 4 | A predefined body keeps every spacecraft built about it: Satellites is an unsynchronised HashSet on a process-wide instance | R02 |
| 5 | A TLE cannot be built without SPICE kernels: the constructor builds the Earth before any argument check | R02 |
| 6 | A body with a GM but no RADII loses its GM: Pallas, Nix, Hydra, Kerberos, Styx, Didymos and Dimorphos read GM 0 and a garbage frame name | R03 |
| 7 | An unknown NAIF id or a barycentre returns uninitialised native memory: a stale or garbage name, a garbage frame and GM 0 | R03 |
| 8 | An impulse whose engine was never added to the spacecraft throws NullReferenceException mid-propagation; AddEngine accepts a tank that is equal by value but was never added | R03 |
| 9 | A ground station cannot be the observer of SpiceAPI.ReadEphemeris: 'Celestial celestialItem frame can't be defined' | R03 |
| 10 | The illumination method is ignored on the managed path, and a misspelt method gives 0 windows on CSPICE | R03 |
| 11 | MemoryDataProvider throws ArgumentException or KeyNotFoundException for data it lacks, so a body that builds on SPICE cannot be built from memory | R04 |
| 12 | Requested atmospheric drag is dropped silently when no body in the list has an atmospheric model | R05 |
| 13 | Release notes cite a CLI option that does not exist, and the README misspells a command | R05 |
| 14 | GeopotentialModelParameters is single-use: a second CelestialBody built from the same instance throws ObjectDisposedException (missed by the study; a prerequisite of R06's geopotential part, per D1) | R06 |
| 15 | The CentralBodyPropagator constructors and Spacecraft.Propagate leave out an unlisted central body and ignore a listed twin's geopotential, while the builder adds the central body | R06 |
| 16 | CelestialBody.GetAtmosphere returns NRLMSISE-00 for the Earth whatever model the body was built with, while the drag force reads the configured model | R06 |
| 17 | The force-building constructors add their forces to the caller's Integrator: a reused instance integrates the forces of every run | R08 |
| 18 | An instrument accepts its spacecraft's frame id: the written FK redefines the spacecraft frame and the CK can no longer be read | R10 |
| 19 | AddEngine accepts an engine whose tank is a different, value-equal instance not on the spacecraft; its first burn throws NullReferenceException | R10 |
| 20 | A Clock given to two spacecraft is attached to the last one: the first spacecraft's SCLK is written with the second's id | R10 |
| 21 | The spacecraft frame id NaifId*1000 overflows int without a check for /NaifId/ > 2147483 | R10 |
| 22 | A propagation step <= 0 is not validated: RK7(8) returns 0 or 1 state with no error, Velocity-Verlet throws List's 'capacity' ArgumentOutOfRangeException | R11 |
| 23 | CelestialItem.GetEphemeris(window, …) reads past its window across a leap second | R11 |
| 24 | The planetographic coordinate searches give two longitude ranges: [0, 2π) on the CSPICE path, (−π, π] on the managed path | R11 |
| 25 | A TLE or TEME initial orbit is rejected by every propagation path, though Spacecraft.cs:548 and :620 say the propagator extracts the state; astro propagate -t ends in an unhandled ArgumentException | R12 |
| 26 | TLEPropagator's PropagationSolution carries zero accelerations: InterpolateAt's velocity is 4.39 m/s off SGP4 between samples | R12 |
| 27 | TriadAttitude.CreateLVLH and CreateProgradeWithSunTracking use the static body axes, not the spacecraft's instance axes | R13 |
| 28 | docs/tutorials/maneuvers.md:98 says ApogeeHeightManeuver raises the apogee 'to a target altitude', but the argument is a radius from the centre | R13 |
| 29 | A kernel-backed spacecraft names its frame <name>_FRAME whatever the loaded FK defines for id NaifId*1000: a twin with another name, or a mission FK, cannot be read | R14 |
| 30 | A spacecraft or instrument name with a space writes an FK that SPICE cannot load | R14 |
| 31 | Threshold window searches (GeometryFinder: distance, coordinate, illumination) lose short windows non-monotonically in the step: no extremes-then-crossings contract | R15 |
| 32 | Field-of-view search on a spacecraft with no attitude silently uses the identity attitude (body axes = ICRF) | R15 |
| 33 | Site illumination search has no observer: Emission and Phase are measured from the site itself, and the two paths disagree | R15 |
| 34 | Managed CoordinateSystem.Planetographic computes planetocentric coordinates (CSPICE's LATITUDINAL), unlike the gfposc path | R15 |
| 35 | A site built from coordinates, seen from a non-Earth observer in its own frame, throws SPICE(UNKNOWNFRAME) even with Aberration.None | R15 |
| 36 | The secondary's own HardBodyRadius is ignored: Pc 33 % high, NaN and negative radii accepted | R17 |
| 37 | EvaluateAvoidance resets the encounter's analysis settings to defaults | R17 |
| 38 | Analyze and AnalyzeAll accept the protected spacecraft as its own secondary | R17 |
| 39 | Analyze and AnalyzeAll accept a trajectory that belongs to another spacecraft | R18 |
| 40 | The TCA is labelled in the window's scale on the sampled path and in TDB on the polynomial path; the polynomial search is not inferred for RK7(8) pairs | R18 |
| 41 | CDM export writes MANEUVERABLE YES for any Spacecraft secondary, even one without an engine, next to INTRACK_THRUST false | R19 |
| 42 | ToEquatorial and Equatorial(StateVector) ignore the state's frame: the 'RightAscension' of an ITRF93 state is an Earth-fixed longitude | R20 |
| 43 | Opm.ToStateVector and ToSpacecraft without an observer ignore CENTER_NAME: a lunar OPM is read as an Earth orbit | R22 |
| 44 | Omm.IsTleCompatible and ToTle ignore CENTER_NAME, REF_FRAME and MEAN_ELEMENT_THEORY: a MOON/ICRF/DSST OMM becomes an Earth TEME TLE | R22 |
| 45 | A multi-message NDM loads its first message silently (OMM, OPM and CDM readers) | R22 |
| 46 | CombinedManeuver caches the current orbit's perigee radius on the target orbit | R23 |
| 47 | Time.AddDays/AddHours/AddMinutes(NaN) return the epoch unchanged and CreateFromJD(NaN) returns J2000, silently, while AddSeconds(NaN) throws | R24 |
| 48 | Time.Create(year, doy, sec) validates nothing: year 0 reads as 2000, and a day or seconds outside the day roll into the neighbouring day or year | R24 |
| 49 | Matrix.Equals treats NaN as equal to any value, and the hash codes then differ | R26 |
| 50 | Normalize() of a zero Vector3 or a zero Quaternion returns zero silently, so a degenerate axis or attitude becomes a zero vector | R26 |
| 51 | astro propagate ignores --earth-geopotential-degree: every run uses degree 10 | R29 |
| 52 | astro propagate --from-tle always throws 'must be defined in inertial frame' | R29 |
| 53 | astro sub-point throws NullReferenceException for a site id that ephemeris accepts | R29 |
| 54 | CelestialItem.ToString prints GM with a wrong unit and 7 significant digits | R29 |
| 55 | propagate writes kernel paths longer than 255 characters that SPICE cannot load, and the message points to stdout | R29 |

## H. Provenance

- Study: 98 scenarios written as a new user would, over ten areas (A onboarding and kernels, B propagation, C spacecraft, maneuvers and attitudes, D window searches and geometry, E conjunction and CDM, F TLE, OMM and orbital elements, G time, frames, math and units, H CLI, I documentation, J Framework Design Guidelines across the library); 105 findings, each verified adversarially by a second agent that re-ran or re-read it: 73 confirmed, 32 partially confirmed, none refuted, 38 further items found by the verifiers.
- Design: the 105 findings grouped into 29 recommendations; four cross-cutting decisions each judged between two opposed proposals (adoption first, rigor first) with the disputed points compiled or measured; each recommendation drafted, then reviewed adversarially against the source; a completeness reviewer over the whole (section C to F).
- Measurements ran on Linux x64, .NET SDK 10.0.404, the native library built from main with cmake (Release), the kernels of IO.Astrodynamics.Tests/Data/SolarSystem. Figures quoted for three platforms come from CI logs or are marked to be measured.
- The scratch programs (study/, verify/, work/, design/work/) live in the session that ran the study and are not committed; each figure names the program that produced it, and the implementation PR that relies on a figure measures it again in a test.
