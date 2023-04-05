/**
 * @file OrbitalParameters.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-04-09
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef ORBITAL_PARAMETERS_H
#define ORBITAL_PARAMETERS_H

#include <memory>
#include <Frames.h>
#include <RADec.h>

namespace IO::SDK::Body
{
	//Forward declaration
	class CelestialBody;
}

namespace IO::SDK::OrbitalParameters
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
		const std::shared_ptr<IO::SDK::Body::CelestialBody> m_centerOfMotion;
		const IO::SDK::Time::TDB m_epoch;
		const IO::SDK::Frames::Frames m_frame;

	public:
		/**
		 * @brief Construct a new Orbital Parameters object
		 * 
		 * @param centerOfMotion 
		 * @param epoch 
		 * @param frame 
		 */
		OrbitalParameters(const std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion, const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame);

		virtual ~OrbitalParameters() = default;

		/**
		 * @brief Get the Center Of Motion
		 * 
		 * @return const std::shared_ptr<IO::SDK::Body::CelestialBody>& 
		 */
		[[nodiscard]] const std::shared_ptr<IO::SDK::Body::CelestialBody> &GetCenterOfMotion() const;

		/**
		 * @brief Get the Epoch
		 * 
		 * @return IO::SDK::Time::TDB 
		 */
		[[nodiscard]] IO::SDK::Time::TDB GetEpoch() const;

		/**
		 * @brief Get the Period
		 * 
		 * @return IO::SDK::Time::TimeSpan 
		 */
		[[nodiscard]] virtual IO::SDK::Time::TimeSpan GetPeriod() const = 0;

		/**
		 * @brief Get the Time To True Anomaly
		 * 
		 * @param trueAnomalyTarget True anomaly targeted
		 * @return IO::SDK::Time::TDB 
		 */
		[[nodiscard]] virtual IO::SDK::Time::TDB GetTimeToTrueAnomaly(double trueAnomalyTarget) const;

		/**
		 * @brief Get the Time To Mean Anomaly
		 * 
		 * @param meanAnomalyTarget 
		 * @return IO::SDK::Time::TDB 
		 */
		[[nodiscard]] virtual IO::SDK::Time::TDB GetTimeToMeanAnomaly(double meanAnomalyTarget) const;

		/**
		 * @brief Get the Mean Motion
		 * 
		 * @return double 
		 */
		[[nodiscard]] double GetMeanMotion() const;

		/**
		 * @brief Get the Specific Angular Momentum
		 * 
		 * @return IO::SDK::Math::Vector3D 
		 */
		[[nodiscard]] virtual IO::SDK::Math::Vector3D GetSpecificAngularMomentum() const = 0;

		/**
		 * @brief Get the State Vector at given epoch
		 * 
		 * @param epoch 
		 * @return StateVector 
		 */
		[[nodiscard]] virtual StateVector GetStateVector(const IO::SDK::Time::TDB &epoch) const = 0;

		/**
		 * @brief Get the State Vector at epoch
		 * 
		 * @return StateVector 
		 */
		[[nodiscard]] virtual StateVector GetStateVector() const;

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
		[[nodiscard]] virtual double GetEccentricAnomaly(const IO::SDK::Time::TDB& epoch) const;

		/**
		 * @brief Get the Mean Anomaly
		 * 
		 * @param epoch 
		 * @return double 
		 */
		[[nodiscard]] virtual double GetMeanAnomaly(const IO::SDK::Time::TDB& epoch) const;

		/**
		 * @brief Get the True Anomaly
		 * 
		 * @param epoch 
		 * @return double 
		 */
		[[nodiscard]] virtual double GetTrueAnomaly(const IO::SDK::Time::TDB& epoch) const;

		/**
		 * @brief Get the Frame
		 * 
		 * @return const IO::SDK::Frames::Frames& 
		 */
		[[nodiscard]] const IO::SDK::Frames::Frames &GetFrame() const;

		/**
		 * @brief Get the Eccentricity Vector
		 * 
		 * @return IO::SDK::Math::Vector3D 
		 */
		[[nodiscard]] IO::SDK::Math::Vector3D GetEccentricityVector() const;

		/**
		 * @brief Get the Perigee Vector
		 * 
		 * @return IO::SDK::Math::Vector3D 
		 */
		[[nodiscard]] IO::SDK::Math::Vector3D GetPerigeeVector() const;

		/**
		 * @brief Get the Apogee Vector
		 * 
		 * @return IO::SDK::Math::Vector3D 
		 */
		[[nodiscard]] IO::SDK::Math::Vector3D GetApogeeVector() const;

		/**
		 * @brief Get the State Vector from true anomalie
		 * 
		 * @param trueAnomalie 
		 * @return StateVector 
		 */
		[[nodiscard]] virtual IO::SDK::OrbitalParameters::StateVector GetStateVector(double trueAnomalie) const;

		/**
		 * @brief Get the Ascending Node Vector
		 * 
		 * @return IO::SDK::Math::Vector3D 
		 */
		[[nodiscard]] IO::SDK::Math::Vector3D GetAscendingNodeVector() const;

		/**
		 * @brief Get right ascension and declination
		 * 
		 * @return IO::SDK::Coordinates::RADec 
		 */
		[[nodiscard]] IO::SDK::Coordinates::RADec GetRADec() const;

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
		[[nodiscard]] double GetMeanLongitude(const IO::SDK::Time::TDB& epoch) const;

		/**
		 * @brief Get the True Longitude at epoch
		 * 
		 * @param epoch 
		 * @return double 
		 */
		[[nodiscard]] double GetTrueLongitude(const IO::SDK::Time::TDB& epoch) const;
		
	};
}
#endif
