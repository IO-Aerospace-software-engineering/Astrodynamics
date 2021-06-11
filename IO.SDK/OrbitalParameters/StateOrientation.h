#ifndef StateOrientation_H
#define StateOrientation_H

#include <Quaternion.h>
#include <Vector3D.h>
#include <TDB.h>
#include <Frames.h>

namespace IO::SDK::OrbitalParameters
{
	class StateOrientation
	{
	private:
		const IO::SDK::Time::TDB m_epoch;
		const IO::SDK::Frames::Frames m_frame;
		const IO::SDK::Math::Quaternion m_quaternion{};
		const IO::SDK::Math::Vector3D m_angularVelocity{};

	public:
		StateOrientation(const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame);
		StateOrientation(const IO::SDK::Math::Vector3D &axis, const double angle, const IO::SDK::Math::Vector3D &angularVelocity, const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame);
		StateOrientation(const double q0, const double q1, const double q2, const double q3, const double v0, const double v1, const double v2, const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame);
		StateOrientation(const IO::SDK::Math::Quaternion &quaternion, const IO::SDK::Math::Vector3D &angularVelocity, const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame);

		IO::SDK::Time::TDB GetEpoch() const;
		IO::SDK::Math::Quaternion GetQuaternion() const;
		IO::SDK::Math::Vector3D GetAngularVelocity() const;
	};
}
#endif // !StateOrientation_H
