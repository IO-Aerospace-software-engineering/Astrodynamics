/**
 * @file EphemerisKernel.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <EphemerisKernel.h>
#include <SDKException.h>
#include <filesystem>
#include <Parameters.h>
#include <SpiceUsr.h>

IO::SDK::Kernels::EphemerisKernel::EphemerisKernel(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft) : Kernel(spacecraft.GetFilesPath() + "/Ephemeris/" + spacecraft.GetName() + ".spk"), m_spacecraft{spacecraft}
{
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::Kernels::EphemerisKernel::ReadStateVector(const IO::SDK::Body::CelestialBody &observer, const IO::SDK::Frames::Frames &frame, const IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TDB &epoch) const
{
	SpiceDouble states[6];
	SpiceDouble lt;
	IO::SDK::Aberrations a{};
	spkezr_c(std::to_string(m_spacecraft.GetId()).c_str(), epoch.GetSecondsFromJ2000().count(), frame.ToCharArray(), a.ToString(aberration).c_str(), observer.GetName().c_str(), states, &lt);
	return IO::SDK::OrbitalParameters::StateVector(std::make_shared<IO::SDK::Body::CelestialBody>(observer), states, epoch, frame);
}

IO::SDK::Time::Window<IO::SDK::Time::TDB> IO::SDK::Kernels::EphemerisKernel::GetCoverageWindow() const
{
	SPICEDOUBLE_CELL(cover, 2);

	spkcov_c(m_filePath.c_str(), m_spacecraft.GetId(), &cover);
	double start;
	double end;

	wnfetd_c(&cover, 0, &start, &end);

	return IO::SDK::Time::Window<IO::SDK::Time::TDB>(IO::SDK::Time::TDB(std::chrono::duration<double>(start)), IO::SDK::Time::TDB(std::chrono::duration<double>(end)));
}

void IO::SDK::Kernels::EphemerisKernel::WriteData(const std::vector<OrbitalParameters::StateVector> &states)
{

	if (states.size() <= 2)
	{
		throw IO::SDK::Exception::InvalidArgumentException("State vector set must have 2 items or more");
	}

	auto frame = states.front().GetFrame();
	for (auto &&sv : states)
	{
		if (sv.GetFrame() != frame)
		{
			throw IO::SDK::Exception::InvalidArgumentException("State vectors must have the same frame");
			break;
		}
	}

	if (std::filesystem::exists(m_filePath))
	{
		unload_c(m_filePath.c_str());
		std::filesystem::remove(m_filePath);
	}

	size_t size = states.size();
	const OrbitalParameters::StateVector &first = states.front();
	const OrbitalParameters::StateVector &last = states.back();
	IO::SDK::Time::TimeSpan delta = states[1].GetEpoch() - first.GetEpoch();

	auto statesArray = new SpiceDouble[size][6];

	for (size_t i = 0; i < size; i++)
	{
		Math::Vector3D position = states[i].GetPosition();
		Math::Vector3D velocity = states[i].GetVelocity();

		statesArray[i][0] = position.GetX();
		statesArray[i][1] = position.GetY();
		statesArray[i][2] = position.GetZ();
		statesArray[i][3] = velocity.GetX();
		statesArray[i][4] = velocity.GetY();
		statesArray[i][5] = velocity.GetZ();
	};

	SpiceInt handle{};

	spkopn_c(m_filePath.c_str(), m_filePath.c_str(), IO::SDK::Parameters::CommentAreaSize, &handle);

	if (IsEvenlySpacedData(states))
	{
		spkw08_c(handle, m_spacecraft.GetId(), first.GetCenterOfMotion()->GetId(), frame.ToCharArray(), first.GetEpoch().GetSecondsFromJ2000().count(), last.GetEpoch().GetSecondsFromJ2000().count(), "Seg1", DefinePolynomialDegree(states.size(), IO::SDK::Parameters::MaximumEphemerisLagrangePolynomialDegree), (SpiceInt)size, statesArray, first.GetEpoch().GetSecondsFromJ2000().count(), delta.GetSeconds().count());
	}
	else
	{
		SpiceDouble *epochs = new SpiceDouble[size];
		for (size_t i = 0; i < size; i++)
		{
			epochs[i] = states[i].GetEpoch().GetSecondsFromJ2000().count();
		}

		spkw09_c(handle, m_spacecraft.GetId(), first.GetCenterOfMotion()->GetId(), frame.ToCharArray(), first.GetEpoch().GetSecondsFromJ2000().count(), last.GetEpoch().GetSecondsFromJ2000().count(), "Seg1", DefinePolynomialDegree(states.size(), 15), (SpiceInt)size, statesArray, epochs);
		delete[] epochs;
	}
	spkcls_c(handle);

	furnsh_c(m_filePath.c_str());

	delete[] statesArray;
}

bool IO::SDK::Kernels::EphemerisKernel::IsEvenlySpacedData(const std::vector<OrbitalParameters::StateVector> &states) const
{

	if (states.size() < 1)
	{
		throw IO::SDK::Exception::InvalidArgumentException("State set must have one or more");
	}

	if (states.size() == 1)
	{
		return true;
	}

	IO::SDK::Time::TimeSpan gap{states[1].GetEpoch() - states[0].GetEpoch()};
	for (size_t i = 1; i < states.size() - 1; i++)
	{
		if (gap != states[i + 1].GetEpoch() - states[i].GetEpoch())
		{
			return false;
		}
	}

	return true;
}
