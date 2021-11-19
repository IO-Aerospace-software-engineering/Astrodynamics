/**
 * @file Instrument.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <Instrument.h>
#include <CircularInstrumentKernel.h>
#include <InstrumentFrameFile.h>
#include <SDKException.h>
#include <InvalidArgumentException.h>
#include <EllipticalInstrumentKernel.h>
#include <RectangularInstrumentKernel.h>
#include <CircularInstrumentKernel.h>
#include <SpiceUsr.h>
#include<Builder.h>


std::string IO::SDK::Instruments::Instrument::GetFilesPath() const
{
	return m_filesPath;
}

std::string IO::SDK::Instruments::Instrument::GetName() const
{
	return m_name;
}

int IO::SDK::Instruments::Instrument::GetId() const
{
	return m_id;
}

const IO::SDK::Body::Spacecraft::Spacecraft &IO::SDK::Instruments::Instrument::GetSpacecraft() const
{
	return m_spacecraft;
}

const std::unique_ptr<IO::SDK::Frames::InstrumentFrameFile> &IO::SDK::Instruments::Instrument::GetFrame() const
{
	return m_frame;
}

IO::SDK::Instruments::Instrument::Instrument(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft, const unsigned short id, const std::string &name, const IO::SDK::Math::Vector3D &orientation, const IO::SDK::Math::Vector3D &boresight, const IO::SDK::Math::Vector3D &fovRefVector, const double fovAngle) : m_spacecraft{spacecraft},
																																																																													m_id{id < 1000 ? spacecraft.GetId() * 1000 - id : throw IO::SDK::Exception::InvalidArgumentException("Instrument Id must be a positive number < 1000")},
																																																																													m_name{name},
																																																																													m_filesPath{spacecraft.GetFilesPath() + "/Instruments/" + name},
																																																																													m_frame(new IO::SDK::Frames::InstrumentFrameFile(*this, orientation)),
																																																																													m_orientation{orientation},
																																																																													m_fovShape{IO::SDK::Instruments::FOVShapeEnum::Circular},
																																																																													m_boresight{boresight},
																																																																													m_fovRefVector{fovRefVector},
																																																																													m_fovAngle{fovAngle}
{

	const_cast<std::unique_ptr<IO::SDK::Kernels::InstrumentKernel> &>(m_kernel).reset(new IO::SDK::Kernels::CircularInstrumentKernel(*this, boresight, fovRefVector, fovAngle));
}

IO::SDK::Instruments::Instrument::Instrument(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft, const unsigned short id, const std::string &name, const IO::SDK::Math::Vector3D &orientation, const IO::SDK::Instruments::FOVShapeEnum fovShape, const IO::SDK::Math::Vector3D &boresight, const IO::SDK::Math::Vector3D &fovRefVector, const double fovAngle, const double crossAngle) : m_spacecraft{spacecraft},
																																																																																																m_id{id < 1000 ? spacecraft.GetId() * 1000 - id : throw IO::SDK::Exception::InvalidArgumentException("Instrument Id must be a positive number < 1000")},
																																																																																																m_name{name},
																																																																																																m_filesPath{spacecraft.GetFilesPath() + "/Instruments/" + name},
																																																																																																m_frame(new IO::SDK::Frames::InstrumentFrameFile(*this, orientation)),
																																																																																																m_orientation{orientation},
																																																																																																m_fovShape{fovShape},
																																																																																																m_boresight{boresight},
																																																																																																m_fovRefVector{fovRefVector},
																																																																																																m_fovAngle{fovAngle},
																																																																																																m_crossAngle{crossAngle}
{

	if (fovShape == IO::SDK::Instruments::FOVShapeEnum::Circular)
	{
		throw IO::SDK::Exception::SDKException("This constructor can't be used with circular field of view instrument");
	}

	if (fovShape == IO::SDK::Instruments::FOVShapeEnum::Rectangular)
	{
		const_cast<std::unique_ptr<IO::SDK::Kernels::InstrumentKernel> &>(m_kernel).reset(new IO::SDK::Kernels::RectangularInstrumentKernel(*this, boresight, fovRefVector, fovAngle, crossAngle));
	}
	else if (fovShape == IO::SDK::Instruments::FOVShapeEnum::Elliptical)
	{
		const_cast<std::unique_ptr<IO::SDK::Kernels::InstrumentKernel> &>(m_kernel).reset(new IO::SDK::Kernels::EllipticalInstrumentKernel(*this, boresight, fovRefVector, fovAngle, crossAngle));
	}
}

IO::SDK::Math::Vector3D IO::SDK::Instruments::Instrument::GetBoresight() const
{
	return m_boresight;
}

IO::SDK::Instruments::FOVShapeEnum IO::SDK::Instruments::Instrument::GetFOVShape() const
{
	return m_fovShape;
}

std::vector<IO::SDK::Math::Vector3D> IO::SDK::Instruments::Instrument::GetFOVBoundaries() const
{
	SpiceChar shape[20];
	SpiceChar frame[50];
	SpiceDouble boresight[3];
	SpiceDouble bounds[4][3];
	SpiceInt n;

	getfov_c(m_id, 4, 20, 50, shape, frame, boresight, &n, bounds);

	std::vector<IO::SDK::Math::Vector3D> res;
	for (int i = 0; i < n; i++)
	{
		res.push_back({bounds[i][0], bounds[i][1], bounds[i][2]});
	}

	return res;
}

std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>> IO::SDK::Instruments::Instrument::FindWindowsWhereInFieldOfView(const IO::SDK::Time::Window<IO::SDK::Time::TDB> searchWindow, const IO::SDK::Body::Body &targetBody, const IO::SDK::Time::TimeSpan &stepSize, const IO::SDK::AberrationsEnum &aberration) const
{
	std::string shape{"POINT"};
	std::string frame{""};

	const IO::SDK::Body::CelestialBody *celestialBody = dynamic_cast<const IO::SDK::Body::CelestialBody *>(&targetBody);
	if (celestialBody)
	{
		shape = "ELLIPSOID";
		frame = celestialBody->GetBodyFixedFrame().GetName();
	}

	std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>> windows;
	SpiceDouble windowStart;
	SpiceDouble windowEnd;

	Aberrations abe;

	const SpiceInt MAXWIN{200000};

	SpiceDouble SPICE_CELL_OCCLT[SPICE_CELL_CTRLSZ + MAXWIN];
	SpiceCell cnfine = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_OCCLT);

	SpiceDouble SPICE_CELL_OCCLT_RESULT[SPICE_CELL_CTRLSZ + MAXWIN];
	SpiceCell results = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_OCCLT_RESULT);

	wninsd_c(searchWindow.GetStartDate().GetSecondsFromJ2000().count(), searchWindow.GetEndDate().GetSecondsFromJ2000().count(), &cnfine);

	furnsh_c("Data/Chaser_mission01/Instruments/Camera600/Frames/Camera600.tf");
	furnsh_c("Data/Chaser_mission01/Frames/Chaser.tf");
	furnsh_c("Data/Chaser_mission01/Instruments/Camera600/Kernels/Camera600.ti");
	gftfov_c(std::to_string(m_id).c_str(), targetBody.GetName().c_str(), shape.c_str(), frame.c_str(), abe.ToString(aberration).c_str(), m_spacecraft.GetName().c_str(), stepSize.GetSeconds().count(), &cnfine, &results);

	for (int i = 0; i < wncard_c(&results); i++)
	{
		wnfetd_c(&results, i, &windowStart, &windowEnd);
		windows.push_back(IO::SDK::Time::Window<IO::SDK::Time::TDB>(IO::SDK::Time::TDB(std::chrono::duration<double>(windowStart)), IO::SDK::Time::TDB(std::chrono::duration<double>(windowEnd))));
	}
	return windows;
}
