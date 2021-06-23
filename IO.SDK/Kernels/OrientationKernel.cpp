#include "OrientationKernel.h"
#include <SpiceUsr.h>
#include <SDKException.h>
#include <SpiceUsr.h>
#include <array>
#include <limits>
#include <filesystem>
#include <InertialFrames.h>
#include <Spacecraft.h>
#include <SpacecraftClockKernel.h>
#include <Builder.h>

IO::SDK::Kernels::OrientationKernel::OrientationKernel(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft) : Kernel(spacecraft.GetFilesPath() + "/Orientations/" + spacecraft.GetName() + ".ck"), m_spacecraft{spacecraft}
{
}

IO::SDK::Kernels::OrientationKernel::~OrientationKernel()
{
}

void IO::SDK::Kernels::OrientationKernel::WriteOrientations(const std::vector<std::vector<IO::SDK::OrbitalParameters::StateOrientation>> &orientations) const
{
	if (orientations.size() <= 0)
	{
		throw IO::SDK::Exception::SDKException("Orientations array is empty");
	}

	auto frame = orientations.front().front().GetFrame();
	for (auto &&orientation : orientations)
	{
		for (auto &&orientationPart : orientation)
		{
			if (orientationPart.GetFrame() != frame)
			{
				throw IO::SDK::Exception::InvalidArgumentException("Orientations collection contains data with differents frames : " + frame.GetName() + " - " + orientationPart.GetFrame().GetName() + ". All orientations must have the same frame. ");
			}
		}
	}

	//Compute segment start date
	double begtime = m_spacecraft.GetClock().ConvertToEncodedClock(orientations.front().front().GetEpoch());

	//Compute segment end date
	double endtime = m_spacecraft.GetClock().ConvertToEncodedClock(orientations.back().back().GetEpoch());

	//Compute spacecraft frame id
	int id = m_spacecraft.GetId() * 1000.0;

	//Number of orientation data
	int n{};

	//numbers of intervels
	int nbIntervals = orientations.size();

	//Encoded clocks array
	std::vector<double> sclks;

	//Quaternion + angular velocity array
	std::vector<std::array<double, 7>> data;

	//intervals start date
	std::vector<double> intervalsStarts;

	//Used to define polynomial degree
	int minSize{std::numeric_limits<int>().max()};

	for (auto &interval : orientations)
	{
		if (interval.empty())
		{
			throw IO::SDK::Exception::InvalidArgumentException("Orientation array is empty");
		}

		//Add interval start date
		intervalsStarts.push_back(m_spacecraft.GetClock().ConvertToEncodedClock(interval.begin()->GetEpoch()));

		//Number of data
		int intervalSize = interval.size();

		if (intervalSize < minSize)
		{
			minSize = intervalSize;
		}
		n += intervalSize;
		for (auto &orientation : interval)
		{
			//Add encoded clock			
			sclks.push_back(m_spacecraft.GetClock().ConvertToEncodedClock(orientation.GetEpoch()));			

			//Add orientation data
			data.push_back({orientation.GetQuaternion().GetQ0(), orientation.GetQuaternion().GetQ1(), orientation.GetQuaternion().GetQ2(), orientation.GetQuaternion().GetQ3(), orientation.GetAngularVelocity().GetX(), orientation.GetAngularVelocity().GetY(), orientation.GetAngularVelocity().GetZ()});
		}
	}

	int degree = DefinePolynomialDegree(minSize, IO::SDK::Parameters::MaximumOrientationLagrangePolynomialDegree);

	if (std::filesystem::exists(m_filePath))
	{
		unload_c(m_filePath.c_str());
		std::filesystem::remove(m_filePath);
	}

	//Write data
	SpiceInt handle;
	ckopn_c(m_filePath.c_str(), "CK_file", 5000, &handle);
	ckw05_c(handle, SpiceCK05Subtype::C05TP3, degree, begtime, endtime, id, frame.ToCharArray(), true, "Seg1", n, &sclks[0], &data[0], IO::SDK::Parameters::ClockAccuracy, nbIntervals, &intervalsStarts[0]);
	ckcls_c(handle);

	furnsh_c(m_filePath.c_str());
}

IO::SDK::OrbitalParameters::StateOrientation IO::SDK::Kernels::OrientationKernel::ReadStateOrientation(const IO::SDK::Time::TDB &epoch, const IO::SDK::Time::TimeSpan &tolerance, const IO::SDK::Frames::Frames &frame) const
{
	//Build plateform id
	SpiceInt id = m_spacecraft.GetId() * 1000;

	//Get encoded clock
	SpiceDouble sclk = m_spacecraft.GetClock().ConvertToEncodedClock(epoch);

	//Build tolerance
	SpiceDouble tol = m_spacecraft.GetClock().GetTicksPerSeconds() * tolerance.GetSeconds().count();

	SpiceDouble cmat[3][3];
	SpiceDouble av[3];
	SpiceDouble clkout;
	SpiceBoolean found;

	//Get orientation and angular velocity
	ckgpav_c(id, sclk, tol, frame.ToCharArray(), cmat, av, &clkout, &found);

	if (!found)
	{
		throw IO::SDK::Exception::SDKException("No orientation found");
	}

	//Build array pointers
	double **arrayCmat;
	arrayCmat = new double *[3];
	for (int i = 0; i < 3; i++)
	{
		arrayCmat[i] = new double[3]{};
	}

	for (size_t i = 0; i < 3; i++)
	{
		for (size_t j = 0; j < 3; j++)
		{
			arrayCmat[i][j] = cmat[i][j];
		}
	}

	IO::SDK::Math::Quaternion q(IO::SDK::Math::Matrix(3, 3, arrayCmat));

	//Free memory
	for (int i = 0; i < 3; i++)
		delete[] arrayCmat[i];

	delete[] arrayCmat;

	IO::SDK::Math::Vector3D angularVelocity{av[0], av[1], av[2]};
	IO::SDK::Time::TDB tdb = m_spacecraft.GetClock().ConvertToTDB(clkout);

	//return state orientation
	return IO::SDK::OrbitalParameters::StateOrientation(q, angularVelocity, tdb, frame);
}

IO::SDK::Time::Window<IO::SDK::Time::TDB> IO::SDK::Kernels::OrientationKernel::GetCoverageWindow() const
{
	SpiceDouble SPICE_CELL_CKCOV[SPICE_CELL_CTRLSZ + 2];
	SpiceCell cnfine = IO::SDK::Spice::Builder::CreateDoubleCell(2, SPICE_CELL_CKCOV);

	ckcov_c(m_filePath.c_str(), m_spacecraft.GetId() * 1000, false, "SEGMENT", 0.0, "TDB", &cnfine);
	double start;
	double end;

	wnfetd_c(&cnfine, 0, &start, &end);

	return IO::SDK::Time::Window<IO::SDK::Time::TDB>(IO::SDK::Time::TDB(std::chrono::duration<double>(start)), IO::SDK::Time::TDB(std::chrono::duration<double>(end)));
}
