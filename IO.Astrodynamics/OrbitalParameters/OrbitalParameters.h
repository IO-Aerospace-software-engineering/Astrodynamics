/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef ORBITAL_PARAMETERS_H
#define ORBITAL_PARAMETERS_H

#include <memory>
#include <Frames.h>
#include <Equatorial.h>

namespace IO::Astrodynamics::Body
{
	//Forward declaration
	class CelestialBody;
}

namespace IO::Astrodynamics::OrbitalParameters
{
	//Forward declaration
	class StateVector;

	/**
	 * @brief Orbital parameters
	 * 
	 */
	class OrbitalParameters
	{

	protected:
		const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> m_centerOfMotion;
		const IO::Astrodynamics::Time::TDB m_epoch;
		const IO::Astrodynamics::Frames::Frames m_frame;

	public:
		/**
		 * @brief Construct a new Orbital Parameters object
		 * 
		 * @param centerOfMotion 
		 * @param epoch 
		 * @param frame 
		 */
		OrbitalParameters(const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion, IO::Astrodynamics::Time::TDB epoch, IO::Astrodynamics::Frames::Frames frame);

		virtual ~OrbitalParameters() = default;

		/**
		 * @brief Get the Center Of Motion
		 * 
		 * @return const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody>&
		 */
		[[nodiscard]] const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &GetCenterOfMotion() const;

		/**
		 * @brief Get the Epoch
		 * 
		 * @return IO::Astrodynamics::Time::TDB
		 */
		[[nodiscard]] IO::Astrodynamics::Time::TDB GetEpoch() const;

		/**
		 * @brief Get the Period
		 * 
		 * @return IO::Astrodynamics::Time::TimeSpan
		 */
		[[nodiscard]] virtual IO::Astrodynamics::Time::TimeSpan GetPeriod() const = 0;

		/**
		 * @brief Get the Time To True Anomaly
		 * 
		 * @param trueAnomalyTarget True anomaly targeted
		 * @return IO::Astrodynamics::Time::TDB
		 */
		[[nodiscard]] virtual IO::Astrodynamics::Time::TDB GetTimeToTrueAnomaly(double trueAnomalyTarget) const;

		/**
		 * @brief Get the Time To Mean Anomaly
		 * 
		 * @param meanAnomalyTarget 
		 * @return IO::Astrodynamics::Time::TDB
		 */
		[[nodiscard]] virtual IO::Astrodynamics::Time::TDB GetTimeToMeanAnomaly(double meanAnomalyTarget) const;

		/**
		 * @brief Get the Mean Motion
		 * 
		 * @return double 
		 */
		[[nodiscard]] double GetMeanMotion() const;

		/**
		 * @brief Get the Specific Angular Momentum
		 * 
		 * @return IO::Astrodynamics::Math::Vector3D
		 */
		[[nodiscard]] virtual IO::Astrodynamics::Math::Vector3D GetSpecificAngularMomentum() const = 0;

		/**
		 * @brief Get the State Vector at given epoch
		 * 
		 * @param epoch 
		 * @return StateVector 
		 */
		[[nodiscard]] virtual StateVector ToStateVector(const IO::Astrodynamics::Time::TDB &epoch) const = 0;

		/**
		 * @brief Get the State Vector at epoch
		 * 
		 * @return StateVector 
		 */
		[[nodiscard]] virtual StateVector ToStateVector() const;

        /**
		 * @brief Get the State Vector from true anomaly
		 *
		 * @param trueAnomaly
		 * @return StateVector
		 */
        [[nodiscard]] virtual IO::Astrodynamics::OrbitalParameters::StateVector ToStateVector(double trueAnomaly) const;

		/**
		 * @brief Is elliptical ?
		 * 
		 * @return true 
		 * @return false 
		 */
		[[nodiscard]] bool IsElliptical() const;

		/**
		 * @brief Is parabolic ?
		 * 
		 * @return true 
		 * @return false 
		 */
		[[nodiscard]] bool IsParabolic() const;

		/**
		 * @brief Is hyperbolic ?
		 * 
		 * @return true 
		 * @return false 
		 */
		[[nodiscard]] bool IsHyperbolic() const;

		/**
		 * @brief Is circular ?
		 * 
		 * @return true 
		 * @return false 
		 */
		[[nodiscard]] bool IsCircular() const;

		/**
		 * @brief Get the Eccentricity
		 * 
		 * @return double 
		 */
		[[nodiscard]] virtual double GetEccentricity() const = 0;

		/**
		 * @brief Get the Semi Major Axis
		 * 
		 * @return double 
		 */
		[[nodiscard]] virtual double GetSemiMajorAxis() const = 0;

		/**
		 * @brief Get the Inclination
		 * 
		 * @return double 
		 */
		[[nodiscard]] virtual double GetInclination() const = 0;

		/**
		 * @brief Get the Periapsis Argument
		 * 
		 * @return double 
		 */
		[[nodiscard]] virtual double GetPeriapsisArgument() const = 0;

		/**
		 * @brief Get the Right Ascending Node Longitude
		 * 
		 * @return double 
		 */
		[[nodiscard]] virtual double GetRightAscendingNodeLongitude() const = 0;

		/**
		 * @brief Get the Mean Anomaly
		 * 
		 * @return double 
		 */
		[[nodiscard]] virtual double GetMeanAnomaly() const = 0;

		/**
		 * @brief Get the True Anomaly
		 * 
		 * @return double 
		 */
		[[nodiscard]] virtual double GetTrueAnomaly() const;

		/**
		 * @brief Get the Specific Orbital Energy
		 * 
		 * @return double 
		 */
		[[nodiscard]] virtual double GetSpecificOrbitalEnergy() const = 0;

		/**
		 * @brief Get the Eccentric Anomaly
		 * 
		 * @param epoch 
		 * @return double 
		 */
		[[nodiscard]] virtual double GetEccentricAnomaly(const IO::Astrodynamics::Time::TDB& epoch) const;

		/**
		 * @brief Get the Mean Anomaly
		 * 
		 * @param epoch 
		 * @return double 
		 */
		[[nodiscard]] virtual double GetMeanAnomaly(const IO::Astrodynamics::Time::TDB& epoch) const;

		/**
		 * @brief Get the True Anomaly
		 * 
		 * @param epoch 
		 * @return double 
		 */
		[[nodiscard]] virtual double GetTrueAnomaly(const IO::Astrodynamics::Time::TDB& epoch) const;

		/**
		 * @brief Get the Frame
		 * 
		 * @return const IO::Astrodynamics::Frames::Frames&
		 */
		[[nodiscard]] const IO::Astrodynamics::Frames::Frames &GetFrame() const;

		/**
		 * @brief Get the Eccentricity Vector
		 * 
		 * @return IO::Astrodynamics::Math::Vector3D
		 */
		[[nodiscard]] IO::Astrodynamics::Math::Vector3D GetEccentricityVector() const;

		/**
		 * @brief Get the Perigee Vector
		 * 
		 * @return IO::Astrodynamics::Math::Vector3D
		 */
		[[nodiscard]] IO::Astrodynamics::Math::Vector3D GetPerigeeVector() const;

		/**
		 * @brief Get the Apogee Vector
		 * 
		 * @return IO::Astrodynamics::Math::Vector3D
		 */
		[[nodiscard]] IO::Astrodynamics::Math::Vector3D GetApogeeVector() const;



		/**
		 * @brief Get the Ascending Node Vector
		 * 
		 * @return IO::Astrodynamics::Math::Vector3D
		 */
		[[nodiscard]] IO::Astrodynamics::Math::Vector3D GetAscendingNodeVector() const;

		/**
		 * @brief Get right ascension and declination
		 * 
		 * @return IO::Astrodynamics::Coordinates::Equatorial
		 */
		[[nodiscard]] IO::Astrodynamics::Coordinates::Equatorial ToEquatorialCoordinates() const;

		/**
		 * @brief Get the Velocity at Perigee
		 * 
		 * @return double 
		 */
		[[nodiscard]] double GetVelocityAtPerigee() const;

		/**
		 * @brief Get the Velocity At Apogee
		 * 
		 * @return double 
		 */
		[[nodiscard]] double GetVelocityAtApogee() const;

		/**
		 * @brief Get the True Longitude
		 * 
		 * @return double 
		 */
		[[nodiscard]] double GetTrueLongitude() const;
		
		/**
		 * @brief Get the Mean Longitude
		 * 
		 * @return double 
		 */
		[[nodiscard]] double GetMeanLongitude() const;

		/**
		 * @brief Get the Mean Longitude at epoch
		 * 
		 * @param epoch 
		 * @return double 
		 */
		[[nodiscard]] double GetMeanLongitude(const IO::Astrodynamics::Time::TDB& epoch) const;

		/**
		 * @brief Get the True Longitude at epoch
		 * 
		 * @param epoch 
		 * @return double 
		 */
		[[nodiscard]] double GetTrueLongitude(const IO::Astrodynamics::Time::TDB& epoch) const;

        static double ConvertTrueAnomalyToMeanAnomaly(double trueAnomaly,double eccentricity);
    };
}
#endif
