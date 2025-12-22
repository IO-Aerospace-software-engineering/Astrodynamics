using System;
using Xunit;
using IO.Astrodynamics;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Tests.Body
{
    /// <summary>
    /// Integration tests for NRLMSISE-00 atmospheric model with CelestialBody.
    /// Validates that NRLMSISE-00 works correctly when accessed through the CelestialBody API.
    /// Test cases based on NRLMSISE-00 reference documentation test suite.
    /// </summary>
    public class CelestialBodyNrlmsise00IntegrationTests
    {
        private const double Tolerance = 1e-6; // Relative tolerance (0.000001%)

        public CelestialBodyNrlmsise00IntegrationTests()
        {
            // Load SPICE kernels required for CelestialBody
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        /// <summary>
        /// Helper method to compare expected and actual values with relative tolerance.
        /// </summary>
        private void AssertScientificEqual(double expected, double actual, string parameterName)
        {
            if (System.Math.Abs(expected) < 1e-100) // Handle very small or zero values
            {
                Assert.True(System.Math.Abs(actual) < 1e-40,
                    $"{parameterName}: Expected ~0, but got {actual:E6}");
            }
            else
            {
                double relativeError = System.Math.Abs((actual - expected) / expected);
                Assert.True(relativeError < Tolerance,
                    $"{parameterName}: Expected {expected:E6}, got {actual:E6}, relative error {relativeError:E6}");
            }
        }

        /// <summary>
        /// Integration Test 1: Verify NRLMSISE-00 integration at 400 km altitude.
        /// Validates that atmospheric model works through CelestialBody API.
        /// </summary>
        [Fact]
        public void Nrlmsise00Integration_StandardConditions400km_ProducesReasonableValues()
        {
            // Arrange - Create space weather and atmospheric model
            var spaceWeather = new SpaceWeather
            {
                F107 = 150.0,
                F107A = 150.0,
                Ap = 4.0,
                ApArray = null
            };
            var atmosphericModel = new Nrlmsise00Model(spaceWeather);
            var earth = new CelestialBody(399, atmosphericModel: atmosphericModel);

            // Create atmospheric context with position and time
            var context = AtmosphericContext.FromPlanetodetic(
                altitude: 400000.0, // 400 km in meters
                geodeticLatitude: 60.0 * Constants.DEG_RAD, // 60° N
                geodeticLongitude: -70.0 * Constants.DEG_RAD, // 70° W
                epoch:  TimeSystem.Time.Create(2000, 172, 29000)
            );

            // Act - Get atmospheric properties through CelestialBody
            double density = earth.GetAirDensity(context);
            double temperature = earth.GetAirTemperature(context);

            // Assert - Verify values are in expected range for 400 km
            Assert.InRange(density, 1e-12, 1e-11); // Typical range at 400 km
            Assert.InRange(temperature, 800, 1200); // Temperature in Celsius
        }

        /// <summary>
        /// Integration Test 2: Verify hydrostatic relationship - density decreases exponentially with altitude.
        /// Validates that the atmospheric scale height follows physical expectations.
        /// </summary>
        [Fact]
        public void Nrlmsise00Integration_HydrostaticEquilibrium_ExponentialDensityDecay()
        {
            // Arrange
            var spaceWeather = SpaceWeather.Nominal;
            var atmosphericModel = new Nrlmsise00Model(spaceWeather);
            var earth = new CelestialBody(399, atmosphericModel: atmosphericModel);

            // Test at three different altitudes
            var context400km = AtmosphericContext.FromPlanetodetic(
                altitude: 400000.0, // 400 km
                geodeticLatitude: 60.0 * Constants.DEG_RAD,
                geodeticLongitude: -70.0 * Constants.DEG_RAD,
                epoch: TimeSystem.Time.Create(2000, 172, 29000)
            );

            var context600km = AtmosphericContext.FromPlanetodetic(
                altitude: 600000.0, // 600 km
                geodeticLatitude: 60.0 * Constants.DEG_RAD,
                geodeticLongitude: -70.0 * Constants.DEG_RAD,
                epoch: TimeSystem.Time.Create(2000, 172, 29000)
            );

            var context800km = AtmosphericContext.FromPlanetodetic(
                altitude: 800000.0, // 800 km
                geodeticLatitude: 60.0 * Constants.DEG_RAD,
                geodeticLongitude: -70.0 * Constants.DEG_RAD,
                epoch: TimeSystem.Time.Create(2000, 172, 29000)
            );

            // Act
            double density400 = earth.GetAirDensity(context400km);
            double density600 = earth.GetAirDensity(context600km);
            double density800 = earth.GetAirDensity(context800km);

            // Assert - Density must decrease with altitude
            Assert.True(density400 > density600,
                $"Density at 400km ({density400:E3}) must be greater than at 600km ({density600:E3})");
            Assert.True(density600 > density800,
                $"Density at 600km ({density600:E3}) must be greater than at 800km ({density800:E3})");

            // Calculate scale height from density ratio: H = Δz / ln(ρ1/ρ2)
            // For thermosphere, scale height is typically 40-60 km
            double scaleHeight1 = 200000.0 / System.Math.Log(density400 / density600); // 400-600 km
            double scaleHeight2 = 200000.0 / System.Math.Log(density600 / density800); // 600-800 km

            // Validate scale heights are physically reasonable for thermosphere
            Assert.InRange(scaleHeight1, 20000.0, 200000.0); // 20-200 km (very wide but physically bounded)
            Assert.InRange(scaleHeight2, 20000.0, 200000.0); // Scale height increases with altitude
        }

        /// <summary>
        /// Integration Test 3: Verify integration at low altitude (100 km).
        /// </summary>
        [Fact]
        public void Nrlmsise00Integration_LowAltitude100km_ProducesHigherDensity()
        {
            // Arrange
            var spaceWeather = SpaceWeather.Nominal;
            var atmosphericModel = new Nrlmsise00Model(spaceWeather);
            var earth = new CelestialBody(399, atmosphericModel: atmosphericModel);

            var context = AtmosphericContext.FromPlanetodetic(
                altitude: 100000.0, // 100 km
                geodeticLatitude: 60.0 * Constants.DEG_RAD,
                geodeticLongitude: -70.0 * Constants.DEG_RAD,
                epoch: TimeSystem.Time.Create(2000, 172, 29000)
            );

            // Act
            double density = earth.GetAirDensity(context);
            double temperature = earth.GetAirTemperature(context);

            // Assert - Density should be higher at lower altitude
            Assert.InRange(density, 1e-7, 1e-6); // Higher density at 100 km (typical range)
            Assert.InRange(temperature, -100, 300); // Thermospheric temperature
        }

        /// <summary>
        /// Integration Test 4: Verify solar minimum produces lower density.
        /// </summary>
        [Fact]
        public void Nrlmsise00Integration_LowSolarActivity_ProducesLowerDensity()
        {
            // Arrange - Use predefined solar minimum conditions
            var spaceWeather = SpaceWeather.SolarMinimum; // F107=70, Ap=4
            var atmosphericModel = new Nrlmsise00Model(spaceWeather);
            var earth = new CelestialBody(399, atmosphericModel: atmosphericModel);

            var context = AtmosphericContext.FromPlanetodetic(
                altitude: 400000.0,
                geodeticLatitude: 60.0 * Constants.DEG_RAD,
                geodeticLongitude: -70.0 * Constants.DEG_RAD,
                epoch: TimeSystem.Time.Create(2000, 172, 29000)
            );

            // Act
            double density = earth.GetAirDensity(context);

            // Assert - Density should be lower during solar minimum
            Assert.InRange(density, 3e-13, 2e-12); // Lower range for solar minimum
        }

        /// <summary>
        /// Integration Test 5: Verify high geomagnetic activity increases density.
        /// </summary>
        [Fact]
        public void Nrlmsise00Integration_HighGeomagneticActivity_ProducesHigherDensity()
        {
            // Arrange - High Ap index
            var spaceWeather = new SpaceWeather
            {
                F107 = 150.0,
                F107A = 150.0,
                Ap = 40.0, // High geomagnetic activity
                ApArray = null
            };
            var atmosphericModel = new Nrlmsise00Model(spaceWeather);
            var earth = new CelestialBody(399, atmosphericModel: atmosphericModel);

            var context = AtmosphericContext.FromPlanetodetic(
                altitude: 400000.0,
                geodeticLatitude: 60.0 * Constants.DEG_RAD,
                geodeticLongitude: -70.0 * Constants.DEG_RAD,
                epoch: TimeSystem.Time.Create(2000, 172, 29000)
            );

            // Act
            double density = earth.GetAirDensity(context);

            // Assert - High geomagnetic activity should increase density
            Assert.InRange(density, 2e-12, 1e-11); // Higher range for storm conditions
        }

        /// <summary>
        /// Integration Test 6: Verify equatorial location produces different results.
        /// </summary>
        [Fact]
        public void Nrlmsise00Integration_EquatorialLocation_ProducesValidValues()
        {
            // Arrange
            var spaceWeather = SpaceWeather.Nominal;
            var atmosphericModel = new Nrlmsise00Model(spaceWeather);
            var earth = new CelestialBody(399, atmosphericModel: atmosphericModel);

            var context = AtmosphericContext.FromPlanetodetic(
                altitude: 400000.0,
                geodeticLatitude: 0.0, // Equator
                geodeticLongitude: -70.0 * Constants.DEG_RAD,
                epoch: TimeSystem.Time.Create(2000, 172, 29000)
            );

            // Act
            double density = earth.GetAirDensity(context);
            double temperature = earth.GetAirTemperature(context);

            // Assert - Equatorial values should be in expected range
            Assert.InRange(density, 1e-12, 1e-11);
            Assert.InRange(temperature, 500, 1200); // Temperature can vary significantly by time of day
        }

        /// <summary>
        /// Integration Test 7: Verify time of day affects results with quantifiable variation.
        /// Validates diurnal variation is significant (typically 20-50% at thermospheric altitudes).
        /// </summary>
        [Fact]
        public void Nrlmsise00Integration_DiurnalVariation_SignificantDensityChange()
        {
            // Arrange
            var spaceWeather = SpaceWeather.Nominal;
            var atmosphericModel = new Nrlmsise00Model(spaceWeather);
            var earth = new CelestialBody(399, atmosphericModel: atmosphericModel);

            // Local noon (LST ~ 12:00)
            var contextNoon = AtmosphericContext.FromPlanetodetic(
                altitude: 400000.0,
                geodeticLatitude: 0.0, // Equator for clearer diurnal signal
                geodeticLongitude: 0.0,
                epoch: TimeSystem.Time.Create(2000, 172, 43200) // Noon UTC at 0° longitude
            );

            // Local midnight (LST ~ 00:00)
            var contextMidnight = AtmosphericContext.FromPlanetodetic(
                altitude: 400000.0,
                geodeticLatitude: 0.0, // Equator
                geodeticLongitude: 180.0 * Constants.DEG_RAD, // 180° longitude (opposite side)
                epoch: TimeSystem.Time.Create(2000, 172, 43200) // Same UTC time, different local time
            );

            // Act
            double densityNoon = earth.GetAirDensity(contextNoon);
            double densityMidnight = earth.GetAirDensity(contextMidnight);

            // Assert - Noon density should be higher than midnight (solar heating effect)
            Assert.True(densityNoon > densityMidnight,
                $"Noon density ({densityNoon:E3}) should be higher than midnight density ({densityMidnight:E3})");

            // Diurnal variation at 400 km is typically 20-50% for NRLMSISE-00
            double variation = (densityNoon - densityMidnight) / densityMidnight;
            Assert.InRange(variation, 0.1, 2.0); // Allow 10-200% variation (conservative range)
        }

        /// <summary>
        /// Integration Test 8: Verify simple altitude-only API still works.
        /// Tests backward compatibility of simple GetAirDensity(altitude) method.
        /// </summary>
        [Fact]
        public void Nrlmsise00Integration_SimpleAltitudeAPI_ThrowsArgumentException()
        {
            // Arrange - NRLMSISE-00 requires full context
            var atmosphericModel = new Nrlmsise00Model();
            var earth = new CelestialBody(399, atmosphericModel: atmosphericModel);

            // Act & Assert - Simple API should throw because NRLMSISE-00 needs time/position
            var exception = Assert.Throws<ArgumentException>(() => earth.GetAirDensity(400000.0));
            Assert.Contains("NRLMSISE-00 requires", exception.Message);
        }

        /// <summary>
        /// Integration Test 9: Verify solar activity effects follow quantitative relationships.
        /// Validates that density variations with F10.7 are physically reasonable (factor of 2-5 from min to max).
        /// </summary>
        [Fact]
        public void Nrlmsise00Integration_SolarActivityEffect_QuantitativeRelationship()
        {
            // Arrange - Create three models with different space weather
            var solarMin = new Nrlmsise00Model(SpaceWeather.SolarMinimum); // F10.7 = 70
            var nominal = new Nrlmsise00Model(SpaceWeather.Nominal); // F10.7 = 150
            var solarMax = new Nrlmsise00Model(SpaceWeather.SolarMaximum); // F10.7 = 250

            var earthMin = new CelestialBody(399, atmosphericModel: solarMin);
            var earthNom = new CelestialBody(399, atmosphericModel: nominal);
            var earthMax = new CelestialBody(399, atmosphericModel: solarMax);

            var context = AtmosphericContext.FromPlanetodetic(
                altitude: 400000.0,
                geodeticLatitude: 60.0 * Constants.DEG_RAD,
                geodeticLongitude: -70.0 * Constants.DEG_RAD,
                epoch: TimeSystem.Time.Create(2000, 172, 29000)
            );

            // Act
            double densityMin = earthMin.GetAirDensity(context);
            double densityNom = earthNom.GetAirDensity(context);
            double densityMax = earthMax.GetAirDensity(context);
            double tempMin = earthMin.GetAirTemperature(context);
            double tempMax = earthMax.GetAirTemperature(context);

            // Assert - Density should increase monotonically with solar activity
            Assert.True(densityMin < densityNom,
                $"Solar minimum density ({densityMin:E3}) should be less than nominal ({densityNom:E3})");
            Assert.True(densityNom < densityMax,
                $"Nominal density ({densityNom:E3}) should be less than solar maximum ({densityMax:E3})");

            // Validate quantitative relationship: density typically increases by factor of 2-20 from solar min to max
            double densityRatio = densityMax / densityMin;
            Assert.InRange(densityRatio, 1.5, 20.0);

            // Temperature should also increase with solar activity (but less dramatically than density)
            Assert.True(tempMax > tempMin,
                $"Solar maximum temperature ({tempMax:F1}°C) should be higher than solar minimum ({tempMin:F1}°C)");

            // Temperature increase is typically 100-500°C from solar min to max at 400 km
            double tempIncrease = tempMax - tempMin;
            Assert.InRange(tempIncrease, 50.0, 800.0);
        }

        /// <summary>
        /// Integration Test 10: Verify physical consistency using ideal gas law P = ρRT.
        /// Validates that density, temperature, and pressure outputs are physically consistent.
        /// </summary>
        [Fact]
        public void Nrlmsise00Integration_IdealGasLaw_PhysicalConsistency()
        {
            // Arrange
            var atmosphericModel = new Nrlmsise00Model();
            var earth = new CelestialBody(399, atmosphericModel: atmosphericModel);

            var context = AtmosphericContext.FromPlanetodetic(
                altitude: 400000.0,
                geodeticLatitude: 60.0 * Constants.DEG_RAD,
                geodeticLongitude: -70.0 * Constants.DEG_RAD,
                epoch: TimeSystem.Time.Create(2000, 172, 29000)
            );

            // Act
            double densityKgM3 = earth.GetAirDensity(context);
            double temperatureCelsius = earth.GetAirTemperature(context);
            double pressureKPa = earth.GetAirPressure(context);

            // Convert to SI units for ideal gas law
            double temperatureKelvin = temperatureCelsius + 273.15; // Convert Celsius to Kelvin
            double pressurePa = pressureKPa * 1000.0;

            // Assert - All values should be positive
            Assert.True(densityKgM3 > 0, "Density should be positive");
            Assert.True(temperatureKelvin > 0, "Temperature should be above absolute zero");
            Assert.True(pressurePa > 0, "Pressure should be positive");

            // For atmospheric models, the specific gas constant varies with composition
            // At high altitudes, the atmosphere is primarily atomic oxygen and helium
            // R_specific ranges from ~287 J/(kg·K) for air at sea level to ~2077 J/(kg·K) for atomic oxygen
            // We use a range to account for varying composition with altitude
            const double rOxygen = 259.8; // J/(kg·K) for O2
            const double rAtomicOxygen = 2077.0; // J/(kg·K) for O

            // Calculate pressure using ideal gas law: P = ρ * R * T
            // At 400 km, composition is mixed, so we test a range of possible R values
            double pLower = densityKgM3 * rOxygen * temperatureKelvin; // Lower bound
            double pUpper = densityKgM3 * rAtomicOxygen * temperatureKelvin; // Upper bound

            // The actual pressure should fall within this physically reasonable range
            // Allow 20% tolerance due to molecular composition variations
            Assert.True(pressurePa >= pLower * 0.8 && pressurePa <= pUpper * 1.2,
                $"Pressure {pressurePa:E3} Pa should be consistent with ideal gas law. " +
                $"Expected range: [{pLower * 0.8:E3}, {pUpper * 1.2:E3}] Pa");
        }


        [Fact]
        public void Nrlmsise00Integration_At_SeaLevel()
        {
            // Arrange
            var atmosphericModel = new Nrlmsise00Model();
            var earth = new CelestialBody(399, atmosphericModel: atmosphericModel);

            var context = AtmosphericContext.FromPlanetodetic(
                altitude: 0.0,
                geodeticLatitude: 60.0 * Constants.DEG_RAD,
                geodeticLongitude: -70.0 * Constants.DEG_RAD,
                epoch: TimeSystem.Time.Create(2000, 172, 29000)
            );

            // Act
            double densityKgM3 = earth.GetAirDensity(context);
            double temperatureCelsius = earth.GetAirTemperature(context);
            double pressureKPa = earth.GetAirPressure(context);

            //Convert hPa
            double pressurehPa = pressureKPa * 10.0;

            // Assert - All values should be positive
            Assert.Equal(1.2634279924969776, densityKgM3);
            Assert.Equal(8.3147576632156301, temperatureCelsius);
            Assert.Equal(1021.6413051731284, pressurehPa);
        }
    }
}