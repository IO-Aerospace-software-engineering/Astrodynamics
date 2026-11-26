using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// #731: the state of a star carries its space velocity, the derivative of its straight line (#718), constant:
/// r0 (μα* e_α + μδ e_δ) per Julian year of TDB, r0 its catalog distance, e_α = (−sin α0, cos α0, 0) and
/// e_δ = (−sin δ0 cos α0, −sin δ0 sin α0, cos δ0) (decided by S. Guillet, 2026-11-17). Before, every state of a star, its
/// SPK included, carried the moving position with a zero velocity (a velocity 1 relative away from the reference).
/// </summary>
public class StarVelocityTests
{
    // The seconds of a Julian year of TDB
    private const double JulianYearSeconds = 365.25 * 86400.0;

    private readonly ITestOutputHelper _output;

    public StarVelocityTests(ITestOutputHelper output)
    {
        TestKernels.EnsureSolarSystem();
        _output = output;
    }

    /// <summary>
    /// A star of <c>tools/sofa_reference/starpm_ref.c --velocity</c> (the stars of <see cref="StarTests.StarpmCases"/>) at
    /// <see cref="Years"/> Julian years of TDB from its catalog epoch: the velocity of SOFA's <c>iauStarpm</c> (rv = 0, fed
    /// μα*/cos δ), the central difference of its position at dt ∓ 1 year in catalog distances (its direction times the ratio
    /// of the catalog parallax to its parallax), NaN where it cannot be fed (status −9, a declination of ±π/2) or does not
    /// move the star (status 2, faster than 0.5c); and the derivative of the straight line, μα* e_α + μδ e_δ, in long double;
    /// both in catalog distances per Julian year.
    /// </summary>
    internal sealed record VelocityCase(string Label, double RightAscension, double Declination, double RightAscensionProperMotion,
        double DeclinationProperMotion, double Parallax, double EpochDays, double Years, int Status, Vector3 Starpm, Vector3 StraightLine);

    // Printed by tools/sofa_reference/starpm_ref.c --velocity (SOFA 2023-10-11, gcc 13.3, x86-64 Linux), never edited by hand
    internal static readonly VelocityCase[] VelocityCases =
    {
        new("Barnard I/311", 4.7028598776368105, 0.081476992715215468, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 0.0, 0,
            new Vector3(-3.8325455181913146e-06, 4.1193987582477138e-06, 4.9905418202944806e-05),
            new Vector3(-3.8326167629516923e-06, 4.1119224385282053e-06, 4.9906028730234195e-05)),
        new("Barnard I/311", 4.7028598776368105, 0.081476992715215468, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 1.0, 0,
            new Vector3(-3.8325454608802319e-06, 4.1193986964160976e-06, 4.9905417454204062e-05),
            new Vector3(-3.8326167629516923e-06, 4.1119224385282053e-06, 4.9906028730234195e-05)),
        new("Barnard I/311", 4.7028598776368105, 0.081476992715215468, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 10.0, 0,
            new Vector3(-3.8325449434831724e-06, 4.1193981401158118e-06, 4.9905410715597529e-05),
            new Vector3(-3.8326167629516923e-06, 4.1119224385282053e-06, 4.9906028730234195e-05)),
        new("Barnard I/311", 4.7028598776368105, 0.081476992715215468, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 100.0, 0,
            new Vector3(-3.8325397681458917e-06, 4.1193925778496151e-06, 4.9905343330427494e-05),
            new Vector3(-3.8326167629516923e-06, 4.1119224385282053e-06, 4.9906028730234195e-05)),
        new("Barnard I/311", 4.7028598776368105, 0.081476992715215468, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 1000.0, 0,
            new Vector3(-3.832488091289367e-06, 4.1193370329473599e-06, 4.9904670418324448e-05),
            new Vector3(-3.8326167629516923e-06, 4.1119224385282053e-06, 4.9906028730234195e-05)),
        new("Barnard DR3", 4.7027635336371842, 0.082718484526907429, -3.8860289090702973e-06, 5.0238303802473693e-05, 0.54697590000000007, 5844, 0.0, 0,
            new Vector3(-3.8458226389131098e-06, 4.195654461948146e-06, 5.0065902356801912e-05),
            new Vector3(-3.8458952520689155e-06, 4.188110824765468e-06, 5.0066527811117171e-05)),
        new("Barnard DR3", 4.7027635336371842, 0.082718484526907429, -3.8860289090702973e-06, 5.0238303802473693e-05, 0.54697590000000007, 5844, 1.0, 0,
            new Vector3(-3.8458225806710668e-06, 4.1956543984789779e-06, 5.0065901598809733e-05),
            new Vector3(-3.8458952520689155e-06, 4.188110824765468e-06, 5.0066527811117171e-05)),
        new("Barnard DR3", 4.7027635336371842, 0.082718484526907429, -3.8860289090702973e-06, 5.0238303802473693e-05, 0.54697590000000007, 5844, 10.0, 0,
            new Vector3(-3.8458220568897463e-06, 4.1956538267809071e-06, 5.0065894776950669e-05),
            new Vector3(-3.8458952520689155e-06, 4.188110824765468e-06, 5.0066527811117171e-05)),
        new("Barnard DR3", 4.7027635336371842, 0.082718484526907429, -3.8860289090702973e-06, 5.0238303802473693e-05, 0.54697590000000007, 5844, 100.0, 0,
            new Vector3(-3.8458168166476936e-06, 4.1956481099055562e-06, 5.0065826559373249e-05),
            new Vector3(-3.8458952520689155e-06, 4.188110824765468e-06, 5.0066527811117171e-05)),
        new("Barnard DR3", 4.7027635336371842, 0.082718484526907429, -3.8860289090702973e-06, 5.0238303802473693e-05, 0.54697590000000007, 5844, 1000.0, 0,
            new Vector3(-3.8457644884665335e-06, 4.1955910219635436e-06, 5.0065145341099903e-05),
            new Vector3(-3.8458952520689155e-06, 4.188110824765468e-06, 5.0066527811117171e-05)),
        new("Polaris I/311", 0.66228513378259868, 1.5579531082148617, 2.156451253575216e-07, -5.745042121148002e-08, 0.0075399999999999998, -3195.9375, 0.0, 0,
            new Vector3(-8.7303674257692391e-08, 2.0537984118282974e-07, -7.4859887993355967e-10),
            new Vector3(-8.730356515985564e-08, 2.0537992625378576e-07, -7.3782803283496958e-10)),
        new("Polaris I/311", 0.66228513378259868, 1.5579531082148617, 2.156451253575216e-07, -5.745042121148002e-08, 0.0075399999999999998, -3195.9375, 1.0, 0,
            new Vector3(-8.7303674282776425e-08, 2.0537984115635234e-07, -7.4859881528800513e-10),
            new Vector3(-8.730356515985564e-08, 2.0537992625378576e-07, -7.3782803283496958e-10)),
        new("Polaris I/311", 0.66228513378259868, 1.5579531082148617, 2.156451253575216e-07, -5.745042121148002e-08, 0.0075399999999999998, -3195.9375, 10.0, 0,
            new Vector3(-8.7303674194721421e-08, 2.0537984117262257e-07, -7.4859875636161706e-10),
            new Vector3(-8.730356515985564e-08, 2.0537992625378576e-07, -7.3782803283496958e-10)),
        new("Polaris I/311", 0.66228513378259868, 1.5579531082148617, 2.156451253575216e-07, -5.745042121148002e-08, 0.0075399999999999998, -3195.9375, 100.0, 0,
            new Vector3(-8.730367400588686e-08, 2.0537984078963959e-07, -7.4859884605224178e-10),
            new Vector3(-8.730356515985564e-08, 2.0537992625378576e-07, -7.3782803283496958e-10)),
        new("Polaris I/311", 0.66228513378259868, 1.5579531082148617, 2.156451253575216e-07, -5.745042121148002e-08, 0.0075399999999999998, -3195.9375, 1000.0, 0,
            new Vector3(-8.730367237315928e-08, 2.0537983676085316e-07, -7.4859887307598093e-10),
            new Vector3(-8.730356515985564e-08, 2.0537992625378576e-07, -7.3782803283496958e-10)),
        new("Barnard I/311 at +89.5 deg", 4.7028598776368105, 1.562069680534925, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 0.0, 0,
            new Vector3(-3.394331526826495e-06, 5.0104916915819747e-05, 4.2945503711316543e-07),
            new Vector3(-3.3943321506154683e-06, 5.0104851456335464e-05, 4.3695629638094987e-07)),
        new("Barnard I/311 at +89.5 deg", 4.7028598776368105, 1.562069680534925, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 1.0, 0,
            new Vector3(-3.394331475900616e-06, 5.0104916164098057e-05, 4.2945503069929643e-07),
            new Vector3(-3.3943321506154683e-06, 5.0104851456335464e-05, 4.3695629638094987e-07)),
        new("Barnard I/311 at +89.5 deg", 4.7028598776368105, 1.562069680534925, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 10.0, 0,
            new Vector3(-3.3943310175719103e-06, 5.010490939858414e-05, 4.2945497277999665e-07),
            new Vector3(-3.3943321506154683e-06, 5.0104851456335464e-05, 4.3695629638094987e-07)),
        new("Barnard I/311 at +89.5 deg", 4.7028598776368105, 1.562069680534925, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 100.0, 0,
            new Vector3(-3.3943264343445779e-06, 5.0104841744079583e-05, 4.2945439281664608e-07),
            new Vector3(-3.3943321506154683e-06, 5.0104851456335464e-05, 4.3695629638094987e-07)),
        new("Barnard I/311 at +89.5 deg", 4.7028598776368105, 1.562069680534925, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 1000.0, 0,
            new Vector3(-3.3942806660434594e-06, 5.0104166142015212e-05, 4.2944860221980291e-07),
            new Vector3(-3.3943321506154683e-06, 5.0104851456335464e-05, 4.3695629638094987e-07)),
        new("Barnard I/311 at -89.5 deg", 4.7028598776368105, -1.562069680534925, -3.8716250946045326e-06, -5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 0.0, 0,
            new Vector3(-3.394331526826495e-06, 5.0104916915819747e-05, -4.2945503711316543e-07),
            new Vector3(-3.3943321506154683e-06, 5.0104851456335464e-05, -4.3695629638094987e-07)),
        new("Barnard I/311 at -89.5 deg", 4.7028598776368105, -1.562069680534925, -3.8716250946045326e-06, -5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 1.0, 0,
            new Vector3(-3.394331475900616e-06, 5.0104916164098057e-05, -4.2945503069929643e-07),
            new Vector3(-3.3943321506154683e-06, 5.0104851456335464e-05, -4.3695629638094987e-07)),
        new("Barnard I/311 at -89.5 deg", 4.7028598776368105, -1.562069680534925, -3.8716250946045326e-06, -5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 10.0, 0,
            new Vector3(-3.3943310175719103e-06, 5.010490939858414e-05, -4.2945497277999665e-07),
            new Vector3(-3.3943321506154683e-06, 5.0104851456335464e-05, -4.3695629638094987e-07)),
        new("Barnard I/311 at -89.5 deg", 4.7028598776368105, -1.562069680534925, -3.8716250946045326e-06, -5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 100.0, 0,
            new Vector3(-3.3943264343445779e-06, 5.0104841744079583e-05, -4.2945439281664608e-07),
            new Vector3(-3.3943321506154683e-06, 5.0104851456335464e-05, -4.3695629638094987e-07)),
        new("Barnard I/311 at -89.5 deg", 4.7028598776368105, -1.562069680534925, -3.8716250946045326e-06, -5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 1000.0, 0,
            new Vector3(-3.3942806660434594e-06, 5.0104166142015212e-05, -4.2944860221980291e-07),
            new Vector3(-3.3943321506154683e-06, 5.0104851456335464e-05, -4.3695629638094987e-07)),
        new("North pole", 1, 1.5707963267948966, 1.0000000000000001e-05, 2.0000000000000002e-05, 0.37919999999999998, 366.5, 0.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-1.9220755965441761e-05, -1.1426396637476534e-05, 1.2246467991473532e-21)),
        new("North pole", 1, 1.5707963267948966, 1.0000000000000001e-05, 2.0000000000000002e-05, 0.37919999999999998, 366.5, 1.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-1.9220755965441761e-05, -1.1426396637476534e-05, 1.2246467991473532e-21)),
        new("North pole", 1, 1.5707963267948966, 1.0000000000000001e-05, 2.0000000000000002e-05, 0.37919999999999998, 366.5, 10.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-1.9220755965441761e-05, -1.1426396637476534e-05, 1.2246467991473532e-21)),
        new("North pole", 1, 1.5707963267948966, 1.0000000000000001e-05, 2.0000000000000002e-05, 0.37919999999999998, 366.5, 100.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-1.9220755965441761e-05, -1.1426396637476534e-05, 1.2246467991473532e-21)),
        new("North pole", 1, 1.5707963267948966, 1.0000000000000001e-05, 2.0000000000000002e-05, 0.37919999999999998, 366.5, 1000.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-1.9220755965441761e-05, -1.1426396637476534e-05, 1.2246467991473532e-21)),
        new("South pole", 1, -1.5707963267948966, 1.0000000000000001e-05, -2.0000000000000002e-05, 0.37919999999999998, 366.5, 0.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-1.9220755965441761e-05, -1.1426396637476534e-05, -1.2246467991473532e-21)),
        new("South pole", 1, -1.5707963267948966, 1.0000000000000001e-05, -2.0000000000000002e-05, 0.37919999999999998, 366.5, 1.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-1.9220755965441761e-05, -1.1426396637476534e-05, -1.2246467991473532e-21)),
        new("South pole", 1, -1.5707963267948966, 1.0000000000000001e-05, -2.0000000000000002e-05, 0.37919999999999998, 366.5, 10.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-1.9220755965441761e-05, -1.1426396637476534e-05, -1.2246467991473532e-21)),
        new("South pole", 1, -1.5707963267948966, 1.0000000000000001e-05, -2.0000000000000002e-05, 0.37919999999999998, 366.5, 100.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-1.9220755965441761e-05, -1.1426396637476534e-05, -1.2246467991473532e-21)),
        new("South pole", 1, -1.5707963267948966, 1.0000000000000001e-05, -2.0000000000000002e-05, 0.37919999999999998, 366.5, 1000.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-1.9220755965441761e-05, -1.1426396637476534e-05, -1.2246467991473532e-21)),
        new("1 rad, 0.1 rad/yr", 1, 1, 0.10000000000000001, 0.10000000000000001, 0.37919999999999998, 366.5, 0.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-0.12961196982207374, -0.016777111240543149, 0.054030230586813978)),
        new("1 rad, 0.1 rad/yr", 1, 1, 0.10000000000000001, 0.10000000000000001, 0.37919999999999998, 366.5, 1.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-0.12961196982207374, -0.016777111240543149, 0.054030230586813978)),
        new("1 rad, 0.1 rad/yr", 1, 1, 0.10000000000000001, 0.10000000000000001, 0.37919999999999998, 366.5, 10.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-0.12961196982207374, -0.016777111240543149, 0.054030230586813978)),
        new("1 rad, 0.1 rad/yr", 1, 1, 0.10000000000000001, 0.10000000000000001, 0.37919999999999998, 366.5, 100.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-0.12961196982207374, -0.016777111240543149, 0.054030230586813978)),
        new("1 rad, 0.1 rad/yr", 1, 1, 0.10000000000000001, 0.10000000000000001, 0.37919999999999998, 366.5, 1000.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-0.12961196982207374, -0.016777111240543149, 0.054030230586813978)),
        new("1.5 rad, 0.1 and 0.05 rad/yr", 1, 1.5, 0.050000000000000003, 0.10000000000000001, 0.37919999999999998, 366.5, 0.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-0.095968433375802811, -0.056921193578458339, 0.0070737201667702915)),
        new("1.5 rad, 0.1 and 0.05 rad/yr", 1, 1.5, 0.050000000000000003, 0.10000000000000001, 0.37919999999999998, 366.5, 1.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-0.095968433375802811, -0.056921193578458339, 0.0070737201667702915)),
        new("1.5 rad, 0.1 and 0.05 rad/yr", 1, 1.5, 0.050000000000000003, 0.10000000000000001, 0.37919999999999998, 366.5, 10.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-0.095968433375802811, -0.056921193578458339, 0.0070737201667702915)),
        new("1.5 rad, 0.1 and 0.05 rad/yr", 1, 1.5, 0.050000000000000003, 0.10000000000000001, 0.37919999999999998, 366.5, 100.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-0.095968433375802811, -0.056921193578458339, 0.0070737201667702915)),
        new("1.5 rad, 0.1 and 0.05 rad/yr", 1, 1.5, 0.050000000000000003, 0.10000000000000001, 0.37919999999999998, 366.5, 1000.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-0.095968433375802811, -0.056921193578458339, 0.0070737201667702915)),
        new("-1.5 rad, -0.1 and 0.05 rad/yr", 1, -1.5, 0.050000000000000003, -0.10000000000000001, 0.37919999999999998, 366.5, 0.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-0.095968433375802811, -0.056921193578458339, -0.0070737201667702915)),
        new("-1.5 rad, -0.1 and 0.05 rad/yr", 1, -1.5, 0.050000000000000003, -0.10000000000000001, 0.37919999999999998, 366.5, 1.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-0.095968433375802811, -0.056921193578458339, -0.0070737201667702915)),
        new("-1.5 rad, -0.1 and 0.05 rad/yr", 1, -1.5, 0.050000000000000003, -0.10000000000000001, 0.37919999999999998, 366.5, 10.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-0.095968433375802811, -0.056921193578458339, -0.0070737201667702915)),
        new("-1.5 rad, -0.1 and 0.05 rad/yr", 1, -1.5, 0.050000000000000003, -0.10000000000000001, 0.37919999999999998, 366.5, 100.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-0.095968433375802811, -0.056921193578458339, -0.0070737201667702915)),
        new("-1.5 rad, -0.1 and 0.05 rad/yr", 1, -1.5, 0.050000000000000003, -0.10000000000000001, 0.37919999999999998, 366.5, 1000.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN),
            new Vector3(-0.095968433375802811, -0.056921193578458339, -0.0070737201667702915))
    };

    /// <summary>The star of a row, built as a caller builds one from its catalog values, and the epoch of its horizon.</summary>
    private static (Star Star, TimeSystem.Time At) StarOf(VelocityCase row, int index)
    {
        var epoch = TimeSystem.Time.CreateTDB(row.EpochDays * 86400.0);
        var star = new Star(731000 + index, $"star{731000 + index}", 1E+30, "spec", 2, row.Parallax, Equatorial.FromRightAscensionDeclination(row.RightAscension, row.Declination, epoch),
            row.DeclinationProperMotion, row.RightAscensionProperMotion, 0, 0, 0, 0, epoch);
        var at = epoch + TimeSpan.FromTicks((long)System.Math.Round(row.Years * 365.25 * TimeSpan.TicksPerDay));
        return (star, at);
    }

    /// <summary>
    /// The space velocity of a star written here, in m/s: r0 (μα* e_α + μδ e_δ) / (365.25 · 86400 s), its catalog
    /// distance r0 the star's <see cref="Star.Distance"/>.
    /// </summary>
    internal static Vector3 SpaceVelocity(Star star)
    {
        double a = star.EquatorialCoordinatesAtEpoch.RightAscension;
        double d = star.EquatorialCoordinatesAtEpoch.Declination;
        var east = new Vector3(-System.Math.Sin(a), System.Math.Cos(a), 0.0);
        var north = new Vector3(-System.Math.Sin(d) * System.Math.Cos(a), -System.Math.Sin(d) * System.Math.Sin(a), System.Math.Cos(d));
        return (east * star.RightAscensionProperMotion + north * star.DeclinationProperMotion) * (star.Distance / JulianYearSeconds);
    }

    /// <summary>
    /// Every star of <see cref="VelocityCases"/>, the stars at ±π/2 and the synthetic stars of the suite (about 1.2c) among
    /// them, at its catalog epoch and 1, 10, 100 and 1000 years after it: the velocity of
    /// <see cref="Star.GetGeometricStateRelativeTo"/> from the solar system barycenter, in catalog distances per Julian
    /// year, against the derivative of the straight line in long double; the internal
    /// <c>GetGeometricStateFromICRF</c> and <see cref="Star.GetEphemeris"/> from the barycenter without aberration give
    /// the same velocity, bit for bit. Before, a zero velocity: 1 relative.
    /// </summary>
    [Fact]
    public void GetGeometricStateRelativeTo_Velocity_IsTheDerivativeOfTheStraightLine()
    {
        double worst = 0.0;
        var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
        for (int i = 0; i < VelocityCases.Length; i++)
        {
            // Arrange
            var row = VelocityCases[i];
            var (star, at) = StarOf(row, i);

            // Act
            var state = star.GetGeometricStateRelativeTo(at, ssb);
            var fromIcrf = star.GetGeometricStateFromICRF(at).ToStateVector();
            var ephemeris = star.GetEphemeris(at, ssb, Frames.Frame.ICRF, Aberration.None);

            // Assert
            var velocity = state.Velocity * (JulianYearSeconds / star.Distance);
            double relative = (velocity - row.StraightLine).Magnitude() / row.StraightLine.Magnitude();
            _output.WriteLine($"MEASURE StarVelocityTests.IsTheDerivativeOfTheStraightLine ({row.Label}, {row.Years:F0} yr): relative velocity {relative:E3}");
            worst = System.Math.Max(worst, relative);
            Assert.Equal(state.Velocity, fromIcrf.Velocity);
            Assert.Equal(state.Velocity, ephemeris.Velocity);
        }

        _output.WriteLine($"MEASURE StarVelocityTests.IsTheDerivativeOfTheStraightLine: worst relative velocity {worst:E3}");
        Assert.True(worst <= StraightLineVelocityTolerance, $"{worst:E3}");
    }

    // The record against the derivative of the straight line in long double: twice the worst over Linux x64, Windows x64
    // and macOS arm64, rounded up at the second digit: 1.978e-16 on the three (CI run 44225490669). Before the fix (#731),
    // 1 (a zero velocity)
    internal const double StraightLineVelocityTolerance = 4.0e-16;

    /// <summary>
    /// The catalog stars of <see cref="VelocityCases"/> (Barnard's star from Hipparcos I/311 and Gaia DR3, Polaris, and
    /// Barnard's motion at ±89.5°), at their catalog epoch and 1, 10, 100 and 1000 years after it, against the derivative
    /// of SOFA's <c>iauStarpm</c> (rv = 0). <c>iauStarpm</c> differs from the straight line of the library by two terms of
    /// its own, which the test adds in closed form to the library's velocity v:
    /// <list type="bullet">
    /// <item><c>iauStarpv</c>'s inertial radial velocity for an observed radial velocity of 0 (<c>starpv.c</c>),
    /// v_r = −c β²/(√(1 − β²) + 1) ≈ −v_t²/(2c) along the catalog direction u0, β = |v|/c: −13.3 m/s for Barnard's star,
    /// 1.49e-4 of its 89 km/s, the term behind the distance departure of 7.6e-8 relative per decade that the tests of #718
    /// measure (rv = 0 is the library's model);</item>
    /// <item>its light time counted from the catalog epoch (a catalog position is the direction of the light that reaches
    /// the barycenter at its epoch, as <c>iauPmsafe</c> reads it), which scales its rate by 1 − (ṙ(dt) − ṙ(0))/c, ṙ the rate
    /// of the distance of the straight line v + v_r u0 (1.5e-8 per year for Barnard's star).</item>
    /// </list>
    /// So the reference is (v + v_r u0)(1 − (ṙ(dt) − v_r)/c); without those terms the library's velocity is 1.5e-4 away
    /// (Barnard's star), with them the resolution of the central difference and, after 1000 years, the terms of higher
    /// order of the light time. Before, a zero velocity: 1 relative.
    /// </summary>
    [Fact]
    public void GetGeometricStateRelativeTo_Velocity_IsTheDerivativeOfIauStarpm()
    {
        double worst = 0.0;
        var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
        int compared = 0;
        for (int i = 0; i < VelocityCases.Length; i++)
        {
            var row = VelocityCases[i];
            if (row.Status != 0)
            {
                continue;
            }

            // Arrange
            var (star, at) = StarOf(row, i);
            var u0 = new Vector3(System.Math.Cos(row.Declination) * System.Math.Cos(row.RightAscension),
                System.Math.Cos(row.Declination) * System.Math.Sin(row.RightAscension), System.Math.Sin(row.Declination));

            // Act
            var velocity = star.GetGeometricStateRelativeTo(at, ssb).Velocity;

            // Assert: iauStarpm's terms added, then in catalog distances per Julian year
            const double c = Astrodynamics.Constants.C;
            double beta = velocity.Magnitude() / c;
            double radial = -c * beta * beta / (System.Math.Sqrt(1.0 - beta * beta) + 1.0);
            var inertial = velocity + u0 * radial;
            var position = u0 * star.Distance + inertial * (row.Years * JulianYearSeconds);
            double rangeRate = inertial.Dot(position) / position.Magnitude();
            var expected = inertial * ((1.0 - (rangeRate - radial) / c) * (JulianYearSeconds / star.Distance));
            double relative = (expected - row.Starpm).Magnitude() / row.Starpm.Magnitude();
            double plain = (velocity * (JulianYearSeconds / star.Distance) - row.Starpm).Magnitude() / row.Starpm.Magnitude();
            _output.WriteLine(
                $"MEASURE StarVelocityTests.IsTheDerivativeOfIauStarpm ({row.Label}, {row.Years:F0} yr): relative velocity {relative:E3} (the library's velocity alone {plain:E3})");
            worst = System.Math.Max(worst, relative);
            compared++;
        }

        Assert.Equal(25, compared);
        _output.WriteLine($"MEASURE StarVelocityTests.IsTheDerivativeOfIauStarpm: worst relative velocity {worst:E3}");
        Assert.True(worst <= StarpmVelocityTolerance, $"{worst:E3}");
    }

    // The record against the derivative of iauStarpm with its two terms: twice the worst over Linux x64, Windows x64 and
    // macOS arm64, rounded up at the second digit: 3.622e-10 on the three, Polaris after 10 years, whose 0.046″/yr the central difference resolves to about
    // 1.1e-16 / (2 · 2.2e-7) (Barnard's star 1.7e-12 to 7.1e-12 up to 100 years, 3.4e-10 after 1000, the light time's terms
    // of higher order) (CI run 44225490669). Without the two terms the
    // library's velocity is 1.50e-4 away (Barnard's star from Gaia DR3), 1.51e-5 more after 1000 years; before the fix
    // (#731), 1 (a zero velocity)
    internal const double StarpmVelocityTolerance = 7.3e-10;

    /// <summary>
    /// The velocity of each star of <see cref="VelocityCases"/> is the derivative of its own positions
    /// (<see cref="Star.GetEquatorialCoordinates"/>): their central difference over ±1 Julian year at 0, 10 and 1000 years,
    /// exact for a straight line at constant speed but for the rounding of the two positions, about ε |p| / (2 yr): the
    /// difference times 2 yr, relative to the position |p|, is measured, so that every star, Polaris's 0.046″/yr as the
    /// synthetic stars' 0.14 rad/yr, is held to the rounding of its positions. Before, a zero velocity: the speed times
    /// 2 yr, relative to the position (2.3e-6 for Polaris, 0.28 for the synthetic stars at their catalog epoch).
    /// </summary>
    [Fact]
    public void GetGeometricStateRelativeTo_Velocity_IsTheRateOfThePosition()
    {
        double worst = 0.0;
        var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
        var year = TimeSpan.FromTicks((long)(365.25 * TimeSpan.TicksPerDay));
        for (int i = 0; i < VelocityCases.Length; i++)
        {
            var row = VelocityCases[i];
            if (row.Years is not (0.0 or 10.0 or 1000.0))
            {
                continue;
            }

            // Arrange
            var (star, at) = StarOf(row, i);

            // Act
            var velocity = star.GetGeometricStateRelativeTo(at, ssb).Velocity;
            var chord = star.GetEquatorialCoordinates(at + year).ToCartesian() - star.GetEquatorialCoordinates(at - year).ToCartesian();

            // Assert
            double relative = (velocity * (2.0 * JulianYearSeconds) - chord).Magnitude() / star.GetEquatorialCoordinates(at).Distance;
            _output.WriteLine($"MEASURE StarVelocityTests.IsTheRateOfThePosition ({row.Label}, {row.Years:F0} yr): velocity × 2 yr − chord, relative to the position {relative:E3}");
            worst = System.Math.Max(worst, relative);
        }

        _output.WriteLine($"MEASURE StarVelocityTests.IsTheRateOfThePosition: worst {worst:E3}");
        Assert.True(worst <= PositionRateTolerance, $"{worst:E3}");
    }

    // Twice the worst over Linux x64, Windows x64 and macOS arm64, rounded up at the second digit: 6.178e-16 on the three,
    // Barnard's star from I/311 after 10 years (CI run 44225490669). Before the fix (#731), the chord itself
    internal const double PositionRateTolerance = 1.3e-15;

    /// <summary>
    /// Barnard's star from its Gaia DR3 record (the example of <c>docs/reference/stars.md</c>) seen from the Earth on
    /// 2026-11-17 and over a year: the velocity of <see cref="Star.GetEphemeris"/> without aberration is the star's space
    /// velocity written here minus the Earth's velocity from the solar system barycenter read from SPICE
    /// (<see cref="SpiceApi.ReadEphemeris(TimeSystem.Time, int, int, Frames.Frame, Aberration)"/>), and so is the range rate
    /// along the direction. Before, the velocity was the opposite of the Earth's alone, 89 km/s (the star's speed) away.
    /// </summary>
    [Fact]
    public void GetEphemeris_FromTheEarth_IsTheSpaceVelocityMinusTheEarths()
    {
        // Arrange
        const double deg = System.Math.PI / 180.0;
        const double mas = System.Math.PI / 180.0 / 3600.0 / 1000.0;
        var j2016 = TimeSystem.Time.CreateTDB(5844.0 * 86400.0);
        var barnard = new Star(4472832, "Barnard", 2.9e29, "M4V", 9.5, 546.9759 / 1000.0,
            Equatorial.FromRightAscensionDeclination(269.44850252544 * deg, 4.73942005111 * deg, j2016),
            10362.394 * mas, -801.551 * mas, 0.0, 0.0, 0.0, 0.0, j2016);
        var earth = PlanetsAndMoons.EARTH_BODY;
        var starVelocity = SpaceVelocity(barnard);
        double worstVelocity = 0.0;
        double worstRangeRate = 0.0;
        for (int month = 0; month < 12; month++)
        {
            var epoch = new TimeSystem.Time(2026, 11, 17, TimeScale.TDB).AddDays(30.0 * month);

            // Act
            var state = barnard.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None);

            // Assert
            var earthVelocity = SpiceApi.Instance.ReadEphemeris(epoch, 0, 399, Frames.Frame.ICRF, Aberration.None).Velocity;
            var expected = starVelocity - earthVelocity;
            double velocity = (state.Velocity - expected).Magnitude() / expected.Magnitude();
            var direction = state.Position / state.Position.Magnitude();
            double rangeRate = System.Math.Abs(state.Velocity.Dot(direction) - expected.Dot(direction));
            _output.WriteLine($"MEASURE StarVelocityTests.FromTheEarth ({epoch}): relative velocity {velocity:E3}, range rate {rangeRate:E3} m/s ({expected.Dot(direction):F3} m/s)");
            worstVelocity = System.Math.Max(worstVelocity, velocity);
            worstRangeRate = System.Math.Max(worstRangeRate, rangeRate);
        }

        _output.WriteLine($"MEASURE StarVelocityTests.FromTheEarth: worst relative velocity {worstVelocity:E3}, range rate {worstRangeRate:E3} m/s");
        Assert.True(worstVelocity <= EarthRelativeVelocityTolerance, $"{worstVelocity:E3}");
        Assert.True(worstRangeRate <= RangeRateTolerance, $"{worstRangeRate:E3}");
    }

    // The space velocity and the Earth's are the same doubles on both sides (the same operations of the runtime), and their
    // difference rounds the same: 0 on Linux x64, Windows x64 and macOS arm64 (CI run 44225490669). The bounds are not a
    // record of that 0, which another
    // platform's rounding could leave, but the rounding of the difference, one unit roundoff per component, 1.9e-16 for
    // the vector, 2.3e-16 with margin, and for the range rate one unit roundoff of the 120 km/s of the two speeds,
    // 1.4e-11 m/s, 1e-10 m/s with margin. Before, the velocity was the Earth's
    // alone: 1.12 relative, the range rate 53.4 m/s away (the star's velocity along the line of sight from the Earth, which
    // its proper motion since 2016 turned by 0.5 mrad)
    internal const double EarthRelativeVelocityTolerance = 2.3e-16;
    internal const double RangeRateTolerance = 1.0e-10;

    /// <summary>Barnard's star from its Gaia DR3 record (the example of <c>docs/reference/stars.md</c>).</summary>
    private static Star BarnardDr3(int naifId)
    {
        const double deg = System.Math.PI / 180.0;
        const double mas = System.Math.PI / 180.0 / 3600.0 / 1000.0;
        var j2016 = TimeSystem.Time.CreateTDB(5844.0 * 86400.0);
        return new Star(naifId, $"Barnard{naifId}", 2.9e29, "M4V", 9.5, 546.9759 / 1000.0,
            Equatorial.FromRightAscensionDeclination(269.44850252544 * deg, 4.73942005111 * deg, j2016),
            10362.394 * mas, -801.551 * mas, 0.0, 0.0, 0.0, 0.0, j2016);
    }

    // A state about a star: the position and velocity of the review's probe, in m and m/s of ICRF
    private static readonly Vector3 AboutStarPosition = new(1e11, 2e11, 3e10);
    private static readonly Vector3 AboutStarVelocity = new(1000.0, -2000.0, 300.0);

    /// <summary>
    /// A state whose observer is a star (Barnard's star from Gaia DR3), converted to the Sun by
    /// <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.RelativeTo"/> on 2026-11-17 and over a year: its velocity is the
    /// state's velocity plus the star's space velocity written here minus the Sun's velocity from the solar system
    /// barycenter read from SPICE (<see cref="SpiceApi.ReadEphemeris(TimeSystem.Time, int, int, Frames.Frame, Aberration)"/>);
    /// its position the state's plus the star's position from the barycenter (<see cref="Star.GetEquatorialCoordinates"/>,
    /// which #731 does not change) minus the Sun's from SPICE. A behaviour change of #731 on every path where a star is an
    /// observer (Changed entry of 11.0.0): before, the star's velocity was zero, so the converted velocity lacked the star's
    /// 89 km/s (the state's own (1000, −2000, 300) m/s came back as (989.8, −2004.7, 298.2) m/s on a85098d, the Sun's
    /// barycentric velocity removed alone).
    /// </summary>
    [Fact]
    public void RelativeTo_AStateAboutAStar_TakesTheStarsSpaceVelocity()
    {
        // Arrange
        var barnard = BarnardDr3(731003);
        var sun = Stars.SUN_BODY;
        var starVelocity = SpaceVelocity(barnard);
        double worstVelocity = 0.0;
        double worstPosition = 0.0;
        for (int month = 0; month < 12; month++)
        {
            var epoch = new TimeSystem.Time(2026, 11, 17, TimeScale.TDB).AddDays(30.0 * month);
            var aboutStar = new IO.Astrodynamics.OrbitalParameters.StateVector(AboutStarPosition, AboutStarVelocity, barnard, epoch, Frames.Frame.ICRF);

            // Act
            var aboutSun = aboutStar.RelativeTo(sun, Aberration.None).ToStateVector();

            // Assert
            var sunSsb = SpiceApi.Instance.ReadEphemeris(epoch, 0, 10, Frames.Frame.ICRF, Aberration.None);
            var starSsb = barnard.GetEquatorialCoordinates(epoch).ToCartesian();
            var expectedVelocity = AboutStarVelocity + starVelocity - sunSsb.Velocity;
            var expectedPosition = AboutStarPosition + starSsb - sunSsb.Position;
            double velocity = (aboutSun.Velocity - expectedVelocity).Magnitude() / expectedVelocity.Magnitude();
            double position = (aboutSun.Position - expectedPosition).Magnitude() / expectedPosition.Magnitude();
            _output.WriteLine($"MEASURE StarVelocityTests.AboutAStar ({epoch}): relative velocity {velocity:E3}, relative position {position:E3}");
            worstVelocity = System.Math.Max(worstVelocity, velocity);
            worstPosition = System.Math.Max(worstPosition, position);
            Assert.Same(sun, aboutSun.Observer);
        }

        _output.WriteLine($"MEASURE StarVelocityTests.AboutAStar: worst relative velocity {worstVelocity:E3}, relative position {worstPosition:E3}");
        Assert.True(worstVelocity <= StarObserverVelocityTolerance, $"{worstVelocity:E3}");
        Assert.True(worstPosition <= StarObserverPositionTolerance, $"{worstPosition:E3}");
    }

    /// <summary>
    /// The Earth seen from a star (Barnard's star from Gaia DR3) on 2026-11-17 and over a year:
    /// <see cref="CelestialItem.GetEphemeris"/> without aberration gives the Earth's velocity from the solar system
    /// barycenter read from SPICE minus the star's space velocity written here, and the Earth's position from SPICE minus
    /// the star's (<see cref="Star.GetEquatorialCoordinates"/>). Before, the star's velocity was zero: the Earth's velocity
    /// alone, 89 km/s away.
    /// </summary>
    [Fact]
    public void GetEphemeris_FromAStar_IsTheTargetsVelocityMinusTheStars()
    {
        // Arrange
        var barnard = BarnardDr3(731004);
        var earth = PlanetsAndMoons.EARTH_BODY;
        var starVelocity = SpaceVelocity(barnard);
        double worstVelocity = 0.0;
        double worstPosition = 0.0;
        for (int month = 0; month < 12; month++)
        {
            var epoch = new TimeSystem.Time(2026, 11, 17, TimeScale.TDB).AddDays(30.0 * month);

            // Act
            var state = earth.GetEphemeris(epoch, barnard, Frames.Frame.ICRF, Aberration.None);

            // Assert
            var earthSsb = SpiceApi.Instance.ReadEphemeris(epoch, 0, 399, Frames.Frame.ICRF, Aberration.None);
            var expectedVelocity = earthSsb.Velocity - starVelocity;
            var expectedPosition = earthSsb.Position - barnard.GetEquatorialCoordinates(epoch).ToCartesian();
            double velocity = (state.Velocity - expectedVelocity).Magnitude() / expectedVelocity.Magnitude();
            double position = (state.Position - expectedPosition).Magnitude() / expectedPosition.Magnitude();
            _output.WriteLine($"MEASURE StarVelocityTests.FromAStar ({epoch}): relative velocity {velocity:E3}, relative position {position:E3}");
            worstVelocity = System.Math.Max(worstVelocity, velocity);
            worstPosition = System.Math.Max(worstPosition, position);
            Assert.Same(barnard, state.Observer);
        }

        _output.WriteLine($"MEASURE StarVelocityTests.FromAStar: worst relative velocity {worstVelocity:E3}, relative position {worstPosition:E3}");
        Assert.True(worstVelocity <= StarObserverVelocityTolerance, $"{worstVelocity:E3}");
        Assert.True(worstPosition <= StarObserverPositionTolerance, $"{worstPosition:E3}");
    }

    /// <summary>
    /// A spacecraft whose initial orbit is a state about a star (Barnard's star from Gaia DR3), propagated for 6 hours about
    /// the Sun (the central body the propagator resolves for a star observer): its state from the Sun at the window start,
    /// read from its stored states, is the initial state converted as above, its velocity the state's plus the star's space
    /// velocity minus the Sun's from SPICE. Before, the star's 89 km/s was missing from the initial velocity, and so from the
    /// whole trajectory (1.93e9 m along z after 6 hours, measured on a85098d).
    /// </summary>
    [Fact]
    public void Propagate_ASpacecraftAboutAStar_StartsWithTheStarsSpaceVelocity()
    {
        // Arrange
        var barnard = BarnardDr3(731005);
        var sun = Stars.SUN_BODY;
        var epoch = new TimeSystem.Time(2026, 11, 17, TimeScale.TDB);
        var aboutStar = new IO.Astrodynamics.OrbitalParameters.StateVector(AboutStarPosition, AboutStarVelocity, barnard, epoch, Frames.Frame.ICRF);
        var spacecraft = new IO.Astrodynamics.Body.Spacecraft.Spacecraft(-731005, "AboutBarnard", 1000.0, aboutStar, 10000.0, new IO.Astrodynamics.Body.Spacecraft.Clock("clk731", 256));

        // Act
        spacecraft.Propagate(new Window(epoch, epoch.AddHours(6.0)), new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(60.0) }, OutputStep = TimeSpan.FromSeconds(60.0), ThirdBodies = [sun] });
        var start = spacecraft.GetEphemeris(epoch, sun, Frames.Frame.ICRF, Aberration.None);

        // Assert
        var sunSsb = SpiceApi.Instance.ReadEphemeris(epoch, 0, 10, Frames.Frame.ICRF, Aberration.None);
        var expectedVelocity = AboutStarVelocity + SpaceVelocity(barnard) - sunSsb.Velocity;
        var expectedPosition = AboutStarPosition + barnard.GetEquatorialCoordinates(epoch).ToCartesian() - sunSsb.Position;
        double velocity = (start.Velocity - expectedVelocity).Magnitude() / expectedVelocity.Magnitude();
        double position = (start.Position - expectedPosition).Magnitude() / expectedPosition.Magnitude();
        _output.WriteLine($"MEASURE StarVelocityTests.SpacecraftAboutAStar: relative velocity {velocity:E3}, relative position {position:E3}");
        Assert.True(velocity <= StarObserverVelocityTolerance, $"{velocity:E3}");
        Assert.True(position <= StarObserverPositionTolerance, $"{position:E3}");
    }

    // The library and the test sum the same doubles in another order (the library v − (v_target − v_star), the test
    // v + v_star − v_target): 0 on Linux x64, Windows x64 and macOS arm64 (CI run 44225490669). The bounds are not a
    // record of that 0, which another platform's
    // rounding could leave, but the rounding of the two orders, two roundings on each side of terms as large as the
    // result (the star's 89 km/s, its 5.8e16 m from the Sun), 4 unit roundoffs per component, 7.7e-16 for the vector,
    // 8.0e-16 with margin. Before (#731), the star's velocity was missing: 0.999 relative for the state about the star
    // and the spacecraft, 0.84 to 1.12 for the Earth seen from it (measured on a85098d with these tests); the positions
    // unchanged (0)
    internal const double StarObserverVelocityTolerance = 8.0e-16;
    internal const double StarObserverPositionTolerance = 8.0e-16;

    /// <summary>
    /// The SPK of a propagated star holds its space velocity: <see cref="Star.WriteEphemeris"/> after
    /// <see cref="Star.Propagate"/>, loaded and read by SPICE (<c>spkezr</c> through <see cref="SpiceApi.ReadEphemeris(TimeSystem.Time, int, int, Frames.Frame, Aberration)"/>)
    /// at the epochs of the grid, the synthetic star of the suite and Barnard's star from Gaia DR3, against the space
    /// velocity written here. Before, a zero velocity in the SPK.
    /// </summary>
    [Fact]
    public void WriteEphemeris_TheSpkHoldsTheSpaceVelocity()
    {
        const double deg = System.Math.PI / 180.0;
        const double mas = System.Math.PI / 180.0 / 3600.0 / 1000.0;
        var j2016 = TimeSystem.Time.CreateTDB(5844.0 * 86400.0);
        var start = new TimeSystem.Time(2001, 1, 1, TimeScale.TDB);
        var stars = new[]
        {
            StarTests.NewStar(731001, start),
            new Star(731002, "Barnard731", 2.9e29, "M4V", 9.5, 546.9759 / 1000.0, Equatorial.FromRightAscensionDeclination(269.44850252544 * deg, 4.73942005111 * deg, j2016),
                10362.394 * mas, -801.551 * mas, 0.0, 0.0, 0.0, 0.0, j2016)
        };
        double worst = 0.0;
        foreach (var star in stars)
        {
            // Arrange
            var window = new Window(start, start.AddDays(4.0));
            var step = TimeSpan.FromHours(12.0);
            star.Propagate(window, step);
            var directory = Directory.CreateTempSubdirectory("star731");
            var spk = new FileInfo(Path.Combine(directory.FullName, $"star{star.CatalogNumber}.bsp"));

            // Act
            star.WriteEphemeris(spk);
            SpiceApi.Instance.LoadKernels(spk);
            try
            {
                // Assert
                var expected = SpaceVelocity(star);
                foreach (var epoch in StarTests.Grid(window, step))
                {
                    var read = SpiceApi.Instance.ReadEphemeris(epoch, 0, star.NaifId, Frames.Frame.ICRF, Aberration.None);
                    double relative = (read.Velocity - expected).Magnitude() / expected.Magnitude();
                    _output.WriteLine($"MEASURE StarVelocityTests.TheSpkHoldsTheSpaceVelocity ({star.Name}, {epoch}): relative velocity {relative:E3}");
                    worst = System.Math.Max(worst, relative);
                }
            }
            finally
            {
                SpiceApi.Instance.UnloadKernels(spk);
                directory.Delete(true);
            }
        }

        _output.WriteLine($"MEASURE StarVelocityTests.TheSpkHoldsTheSpaceVelocity: worst relative velocity {worst:E3}");
        Assert.True(worst <= SpkVelocityTolerance, $"{worst:E3}");
    }

    // Twice the worst over Linux x64, Windows x64 and macOS arm64, rounded up at the second digit: 1.839e-16 on the three,
    // the SPK's km/s read back in m/s (CI run 44225490669). Before the fix (#731), 1 (a zero velocity)
    internal const double SpkVelocityTolerance = 3.7e-16;
}
