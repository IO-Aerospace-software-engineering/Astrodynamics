/**
 * @file Body.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-03-22
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef BODY_H
#define BODY_H

#include <memory>
#include <string>
#include <vector>
#include <string_view>

#include <Constants.h>
#include <SDKException.h>
#include <Parameters.h>
#include <OrbitalParameters.h>
#include <SpiceUsr.h>
#include <Frames.h>
#include <Aberrations.h>
#include <Window.h>
#include <TDB.h>
#include <Constraint.h>
#include <TimeSpan.h>
#include <OccultationType.h>

namespace IO::SDK::OrbitalParameters
{
	class OrbitalParameters;
	class StateVector;
}

namespace IO::SDK::Body
{
	/**
	 * @brief Body class
	 * 
	 */
	class Body : public std::enable_shared_from_this<IO::SDK::Body::Body>
	{
	private:
	protected:
		std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> m_orbitalParametersAtEpoch{};
		std::vector<IO::SDK::Body::Body *> m_satellites{};
		const int m_id{};
		const std::string m_name{};
		double m_mass{};
		double m_mu{};

	public:
		/**
		 * @brief Construct a new Body object
		 * 
		 * @param id 
		 * @param name 
		 * @param mass kg
		 */
		Body(const int id, const std::string &name, const double mass);

		/**
		 * @brief Construct a new Body object
		 * 
		 * @param id 
		 * @param name 
		 * @param mass kg
		 * @param orbitalParameters 
		 */
		Body(const int id, const std::string &name, const double mass, std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParametersAtEpoch);

		/**
		 * @brief Construct a new Body object
		 * 
		 * @param id 
		 * @param name 
		 * @param mass 
		 * @param centerOfMotion 
		 */
		Body(const int id, const std::string &name, const double mass, std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion);

		Body(const Body &body);

		virtual ~Body() = default;

		/**
		 * @brief Get the body identifier
		 * 
		 * @return const int 
		 */
		const int GetId() const;

		/**
		 * @brief Get the body name
		 * 
		 * @return const std::string 
		 */
		const std::string GetName() const;

		/**
		 * @brief Get the Mass 
		 * 
		 * @return double 
		 */
		virtual double GetMass() const;

		/**
		 * @brief Get the Mu value
		 * 
		 * @return double 
		 */
		double GetMu() const;

		/**
		 * @brief Get body orbital parameters
		 * 
		 * @return std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> 
		 */
		const std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> &GetOrbitalParametersAtEpoch() const;

		/**
		 * @brief Get the Satellites
		 * 
		 * @return const std::vector<IO::SDK::Body::Body*>& 
		 */
		const std::vector<IO::SDK::Body::Body *> &GetSatellites() const;

		/**
		 * @brief Get the State Vector relative to its center of motion
		 * 
		 * @param frame 
		 * @param aberration 
		 * @param epoch 
		 * @return IO::SDK::OrbitalParameters::StateVector 
		 */
		virtual IO::SDK::OrbitalParameters::StateVector ReadEphemeris(const IO::SDK::Frames::Frames &frame, const IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TDB &epoch) const;

		/**
		 * @brief Get state vector from ephemeris relative to another body
		 * 
		 * @param epoch 
		 * @return IO::SDK::OrbitalParameters::StateVector 
		 */
		virtual IO::SDK::OrbitalParameters::StateVector ReadEphemeris( const IO::SDK::Frames::Frames &frame, const IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TDB &epoch,const IO::SDK::Body::CelestialBody &relativeTo) const;

		virtual bool operator==(const IO::SDK::Body::Body &rhs) const;
		virtual bool operator!=(const IO::SDK::Body::Body &rhs) const;

		std::shared_ptr<IO::SDK::Body::Body> GetSharedPointer();

		/**
		 * @brief Find windows when distance constraint occurs
		 * 
		 * @param targetBody Target body
		 * @param oberver Observer
		 * @param constraint Constraint operator
		 * @param aberration Aberration
		 * @param value Target value
		 * @param searchWindow Time window where constraint is evaluated
		 * @param step Step size (should be shorter than the shortest of these intervals. WARNING : A short step size could increase compute time)
		 * @return std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>> 
		 */
		std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>> FindWindowsOnDistanceConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> searchWindow, const Body &targetBody, const Body &oberver, const IO::SDK::Constraint &constraint, const IO::SDK::AberrationsEnum aberration, const double value, const IO::SDK::Time::TimeSpan &step) const;

		std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>> FindWindowsOnOccultationConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> searchWindow, const IO::SDK::Body::CelestialBody &targetBody, const IO::SDK::Body::CelestialBody &frontBody, const IO::SDK::OccultationType &occultationType, const IO::SDK::AberrationsEnum aberration,const IO::SDK::Time::TimeSpan& stepSize) const;
	};
}
#endif
