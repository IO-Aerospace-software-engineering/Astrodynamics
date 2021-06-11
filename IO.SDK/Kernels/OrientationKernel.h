#ifndef ORIENTATION_KERNEL_H
#define ORIENTATION_KERNEL_H

#include<Kernel.h>
#include<string>
#include<vector>
#include<StateOrientation.h>
#include<InertialFrames.h>
#include<Window.h>
#include<TDB.h>
#include<TimeSpan.h>

//Forward declaration
namespace IO::SDK::Body::Spacecraft
{
	class Spacecraft;
}

namespace IO::SDK::Kernels
{
	class OrientationKernel :public IO::SDK::Kernels::Kernel
	{
	private:
		/// <summary>
		/// Instanciate orientation kernel
		/// </summary>
		/// <param name="spacecraft"></param>
		OrientationKernel(const IO::SDK::Body::Spacecraft::Spacecraft& spacecraft);
		const IO::SDK::Body::Spacecraft::Spacecraft& m_spacecraft;

		

	public:

		virtual ~OrientationKernel();

		/// <summary>
		/// Write orientation data to the kernel
		/// </summary>
		/// <param name="orientations">Orientation data</param>
		/// <param name="frame">Inertial frame</param>
		void WriteOrientations(const std::vector<std::vector<IO::SDK::OrbitalParameters::StateOrientation>>& orientations, const IO::SDK::Frames::Frames& frame) const;

		/// <summary>
		/// Get kernel coverage window
		/// </summary>
		/// <returns></returns>
		IO::SDK::Time::Window<IO::SDK::Time::TDB> GetCoverageWindow() const override;

		/// <summary>
		/// read a state orientation
		/// </summary>
		/// <param name="epoch"></param>
		/// <param name="tolerance"></param>
		/// <param name="frame"></param>
		/// <returns></returns>
		IO::SDK::OrbitalParameters::StateOrientation ReadStateOrientation(const IO::SDK::Time::TDB& epoch, const IO::SDK::Time::TimeSpan& tolerance, const IO::SDK::Frames::Frames& frame)const;

		friend class IO::SDK::Body::Spacecraft::Spacecraft;
	};
}
#endif // !ORIENTATION_KERNEL_H





