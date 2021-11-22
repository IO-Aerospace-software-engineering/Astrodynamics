/**
 * @file Instrument.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-02-23
 * 
 * @copyright Copyright (c) 2021
 * 
 */

#ifndef INSTRUMENT_H
#define INSTRUMENT_H
#include <memory>
#include <string>
#include <vector>

#include <SpiceUsr.h>

#include <Vector3D.h>
#include <Spacecraft.h>
#include <FOVShapes.h>
#include <InstrumentKernel.h>
#include <Window.h>
#include <TDB.h>
#include <TimeSpan.h>

namespace IO::SDK::Body::Spacecraft
{
	class Spacecraft;
}

namespace IO::SDK::Frames
{
	class InstrumentFrameFile;
}

namespace IO::SDK::Kernels
{

	class CircularInstrumentKernel;
	class RectangularInstrumentKernel;
	class EllipticalInstrumentKernel;
	class InstrumentKernel;
}

namespace IO::SDK::Instruments
{
	class Instrument
	{
	private:
		const IO::SDK::Body::Spacecraft::Spacecraft &m_spacecraft;
		const int m_id{};
		const std::string m_name;
		const std::string m_filesPath;
		const std::unique_ptr<IO::SDK::Frames::InstrumentFrameFile> m_frame{nullptr};
		const IO::SDK::Math::Vector3D m_orientation{};
		const IO::SDK::Instruments::FOVShapeEnum m_fovShape{};
		const IO::SDK::Math::Vector3D m_boresight{};
		const IO::SDK::Math::Vector3D m_fovRefVector{};
		const double m_fovAngle{};
		const std::unique_ptr<IO::SDK::Kernels::InstrumentKernel> m_kernel{nullptr};
		const double m_crossAngle{};

		/**
		 * @brief Construct a new circular instrument object
		 * 
		 * @param spacecraft 
		 * @param id 
		 * @param name 
		 * @param orientation 
		 * @param boresight 
		 * @param fovRefVector 
		 * @param fovAngle 
		 */
		Instrument(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft, const unsigned short id, const std::string &name, const IO::SDK::Math::Vector3D &orientation, const IO::SDK::Math::Vector3D &boresight, const IO::SDK::Math::Vector3D &fovRefVector, const double fovAngle);

		/**
		 * @brief Construct a new rectangular or elliptical instrument object
		 * 
		 * @param spacecraft 
		 * @param id 
		 * @param name 
		 * @param orientation 
		 * @param fovShape 
		 * @param boresight 
		 * @param fovRefVector 
		 * @param fovAngle 
		 * @param crossAngle 
		 */
		Instrument(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft, const unsigned short id, const std::string &name, const IO::SDK::Math::Vector3D &orientation, const IO::SDK::Instruments::FOVShapeEnum fovShape, const IO::SDK::Math::Vector3D &boresight, const IO::SDK::Math::Vector3D &fovRefVector, const double fovAngle, const double crossAngle);

	public:
		~Instrument() = default;

		/**
		 * @brief Get the Files Path object
		 * 
		 * @return std::string 
		 */
		std::string GetFilesPath() const;

		/**
		 * @brief Get the Name object
		 * 
		 * @return std::string 
		 */
		std::string GetName() const;

		/**
		 * @brief Get the Id object
		 * 
		 * @return int 
		 */
		int GetId() const;

		/**
		 * @brief Get the Spacecraft object
		 * 
		 * @return const IO::SDK::Body::Spacecraft& 
		 */
		const IO::SDK::Body::Spacecraft::Spacecraft &GetSpacecraft() const;

		/**
		 * @brief Get the Frame object
		 * 
		 * @return const std::unique_ptr<IO::SDK::Frames::InstrumentFrame>& 
		 */
		const std::unique_ptr<IO::SDK::Frames::InstrumentFrameFile> &GetFrame() const;

		/**
		 * @brief Get the Boresight object
		 * 
		 * @return IO::SDK::Math::Vector3D 
		 */
		IO::SDK::Math::Vector3D GetBoresight() const;

		/**
		 * @brief Get the FOV shape
		 * 
		 * @return IO::SDK::Instruments::FOVShapeEnum 
		 */
		IO::SDK::Instruments::FOVShapeEnum GetFOVShape() const;

		/**
		 * @brief Get the FOV boundaries
		 * 
		 * @return std::vector<IO::SDK::Math::Vector3D> 
		 */
		std::vector<IO::SDK::Math::Vector3D> GetFOVBoundaries() const;

		/**
		 * @brief Find window where target body is in field of view
		 * 
		 * @param searchWindow 
		 * @param targetBody 
		 * @param stepSize 
		 * @return std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>> 
		 */
		std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>> FindWindowsWhereInFieldOfView(const IO::SDK::Time::Window<IO::SDK::Time::TDB>& searchWindow,const IO::SDK::Body::Body& targetBody,const IO::SDK::Time::TimeSpan& stepSize,const IO::SDK::AberrationsEnum& aberration) const;

		friend class IO::SDK::Body::Spacecraft::Spacecraft;
	};
}

#endif // !INSTRUMENT_H
