/**
 * @file OrientationKernel.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-06-11
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef ORIENTATION_KERNEL_H
#define ORIENTATION_KERNEL_H

#include<Kernel.h>
#include<vector>
#include<StateOrientation.h>

//Forward declaration
namespace IO::SDK::Body::Spacecraft
{
	class Spacecraft;
}

namespace IO::SDK::Kernels
{
	class OrientationKernel final :public IO::SDK::Kernels::Kernel
	{
	private:
		/**
		 * @brief Construct a new Orientation Kernel object
		 * 
		 * @param spacecraft 
		 */
		explicit OrientationKernel(const IO::SDK::Body::Spacecraft::Spacecraft& spacecraft);
		const IO::SDK::Body::Spacecraft::Spacecraft& m_spacecraft;		

	public:

		~OrientationKernel() override;

		/**
		 * @brief Write orientations data
		 * 
		 * @param orientations 
		 */
		void WriteOrientations(const std::vector<std::vector<IO::SDK::OrbitalParameters::StateOrientation>>& orientations) const;

		/**
		 * @brief Get the Coverage Window
		 * 
		 * @return IO::SDK::Time::Window<IO::SDK::Time::TDB> 
		 */
		[[nodiscard]] IO::SDK::Time::Window<IO::SDK::Time::TDB> GetCoverageWindow() const override;

		/**
		 * @brief Read state orientations
		 * 
		 * @param epoch 
		 * @param tolerance 
		 * @param frame 
		 * @return IO::SDK::OrbitalParameters::StateOrientation 
		 */
		[[nodiscard]] IO::SDK::OrbitalParameters::StateOrientation ReadStateOrientation(const IO::SDK::Time::TDB& epoch, const IO::SDK::Time::TimeSpan& tolerance, const IO::SDK::Frames::Frames& frame)const;

		friend class IO::SDK::Body::Spacecraft::Spacecraft;
	};
}
#endif // !ORIENTATION_KERNEL_H





