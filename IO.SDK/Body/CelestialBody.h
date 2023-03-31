/**
 * @file CelestialBody.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-03-26
 * 
 * @copyright Copyright (c) 2021
 * 
 */

#ifndef CELESTIAL_BODY_H
#define CELESTIAL_BODY_H

#include <string>
#include <cmath>

#include <Body.h>
#include <BodyFixedFrames.h>
#include <TDB.h>
#include <Planetographic.h>

namespace IO::SDK::Body
{
	class CelestialBody final : public IO::SDK::Body::Body
    {
	private:
		
		const double m_sphereOfInfluence{};		
		const double m_hillSphere{};
		const IO::SDK::Frames::BodyFixedFrames m_BodyFixedFrame;
		double ReadGM(int id);

	public:
		CelestialBody(int id, std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion);
		explicit CelestialBody(int id);
		double GetSphereOfInfluence() const;
		double GetHillSphere() const;
		/**
		 * @brief Get the Relative Statevector from this celestial body to a given target
		 * 
		 * @param targetStateVector 
		 * @return IO::SDK::OrbitalParameters::StateVector 
		 */
		IO::SDK::OrbitalParameters::StateVector GetRelativeStatevector(const IO::SDK::OrbitalParameters::StateVector &targetStateVector) const;

		/**
		 * @brief Know if a target is in sphere of influence
		 * 
		 * @param targetStateVector 
		 * @return true 
		 * @return false 
		 */
		bool IsInSphereOfInfluence(const IO::SDK::OrbitalParameters::StateVector &targetStateVector) const;

		/**
		 * @brief Know if a target is in hill sphere
		 * 
		 * @param targetStateVector 
		 * @return true 
		 * @return false 
		 */
		bool IsInHillSphere(const IO::SDK::OrbitalParameters::StateVector &targetStateVector) const;

		/**
		 * @brief Get the Body Fixed Frame
		 * 
		 * @return const IO::SDK::Frames::BodyFixedFrames& 
		 */
		const IO::SDK::Frames::BodyFixedFrames &GetBodyFixedFrame() const;

		/**
		 * @brief Get the Radius
		 * 
		 * @return IO::SDK::Math::Vector3D 
		 */
		IO::SDK::Math::Vector3D GetRadius() const;

		/**
		 * @brief Get the Flattening
		 * 
		 * @return double 
		 */
		double GetFlattening() const;

		/**
		 * @brief Get the Angular Velocity
		 * 
		 * @param epoch 
		 * @return double 
		 */
		double GetAngularVelocity(const IO::SDK::Time::TDB &epoch) const;

		/**
		 * @brief Get the Sideral Rotation Period
		 * 
		 * @param epoch 
		 * @return IO::SDK::Time::TimeSpan 
		 */
		IO::SDK::Time::TimeSpan GetSideralRotationPeriod(const IO::SDK::Time::TDB &epoch) const;

        static bool IsSun(int celestialBodyId);
        static bool IsPlanet(int celestialBodyId);
        static bool IsAsteroid(int celestialBodyId);
        static bool IsMoon(int celestialBodyId);
        static int FindCenterOfMotionId(int celestialBodyNaifId);

	};
	/**
	 * @brief Compute sphere of influence radius
	 * 
	 * @param a Semi major axis
	 * @param majorMass Major body
	 * @param minorMass Minor body
	 * @return double 
	 */
	double SphereOfInfluence(double a, double majorMass, double minorMass);

	/**
	 * @brief Compute Hill sphere radius
	 * 
	 * @param a Semi major axis
	 * @param majorMass Major body
	 * @param minorMass minor body 
	 * @return double 
	 */
	double HillSphere(double a, double e, double majorMass, double minorMass);



}
#endif
