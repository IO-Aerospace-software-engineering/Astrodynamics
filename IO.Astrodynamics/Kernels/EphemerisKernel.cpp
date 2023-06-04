/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <EphemerisKernel.h>
#include <SDKException.h>
#include <filesystem>
#include <utility>
#include <Parameters.h>
#include <SpiceUsr.h>
#include <Builder.h>
#include <InvalidArgumentException.h>

IO::Astrodynamics::Kernels::EphemerisKernel::EphemerisKernel(std::string filePath, int objectId) : Kernel(std::move(filePath)), m_objectId{objectId}
{

}

IO::Astrodynamics::OrbitalParameters::StateVector
IO::Astrodynamics::Kernels::EphemerisKernel::ReadStateVector(const IO::Astrodynamics::Body::CelestialBody &observer, const IO::Astrodynamics::Frames::Frames &frame, const IO::Astrodynamics::AberrationsEnum aberration,
                                                   const IO::Astrodynamics::Time::TDB &epoch) const
{
    SpiceDouble states[6];
    SpiceDouble lt;
    spkezr_c(std::to_string(m_objectId).c_str(), epoch.GetSecondsFromJ2000().count(), frame.ToCharArray(), IO::Astrodynamics::Aberrations::ToString(aberration).c_str(),
             observer.GetName().c_str(),
             states, &lt);
    for (double &state: states)
    {
        state = state * 1000.0;
    }

    return IO::Astrodynamics::OrbitalParameters::StateVector{std::make_shared<IO::Astrodynamics::Body::CelestialBody>(observer), states, epoch, frame};
}

IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> IO::Astrodynamics::Kernels::EphemerisKernel::GetCoverageWindow() const
{
    const SpiceInt MAXWIN{2};

    SpiceDouble SPICE_CELL_DIST[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell cnfine = IO::Astrodynamics::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_DIST);

    spkcov_c(m_filePath.c_str(), m_objectId, &cnfine);
    double start;
    double end;

    wnfetd_c(&cnfine, 0, &start, &end);

    return IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>{IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(start)), IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(end))};
}

void IO::Astrodynamics::Kernels::EphemerisKernel::WriteData(const std::vector<OrbitalParameters::StateVector> &states)
{

    if (states.size() <= 2)
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("State vector set must have 2 items or more");
    }

    auto frame = states.front().GetFrame();
    for (auto &&sv: states)
    {
        if (sv.GetFrame() != frame)
        {
            throw IO::Astrodynamics::Exception::InvalidArgumentException("State vectors must have the same frame");
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
    IO::Astrodynamics::Time::TimeSpan delta = states[1].GetEpoch() - first.GetEpoch();

    auto statesArray = new SpiceDouble[size][6];

    for (size_t i = 0; i < size; i++)
    {
        Math::Vector3D position = states[i].GetPosition();
        Math::Vector3D velocity = states[i].GetVelocity();

        statesArray[i][0] = position.GetX() / 1000.0;
        statesArray[i][1] = position.GetY() / 1000.0;
        statesArray[i][2] = position.GetZ() / 1000.0;
        statesArray[i][3] = velocity.GetX() / 1000.0;
        statesArray[i][4] = velocity.GetY() / 1000.0;
        statesArray[i][5] = velocity.GetZ() / 1000.0;
    }

    SpiceInt handle{};

    spkopn_c(m_filePath.c_str(), m_filePath.c_str(), IO::Astrodynamics::Parameters::CommentAreaSize, &handle);

    if (IsEvenlySpacedData(states))
    {
        spkw08_c(handle, m_objectId, first.GetCenterOfMotion()->GetId(), frame.ToCharArray(), first.GetEpoch().GetSecondsFromJ2000().count(),
                 last.GetEpoch().GetSecondsFromJ2000().count(), "Seg1", DefinePolynomialDegree(states.size(), IO::Astrodynamics::Parameters::MaximumEphemerisLagrangePolynomialDegree),
                 (SpiceInt) size, statesArray, first.GetEpoch().GetSecondsFromJ2000().count(), delta.GetSeconds().count());
    } else
    {
        auto epochs = new SpiceDouble[size];
        for (size_t i = 0; i < size; i++)
        {
            epochs[i] = states[i].GetEpoch().GetSecondsFromJ2000().count();
        }

        spkw09_c(handle, m_objectId, first.GetCenterOfMotion()->GetId(), frame.ToCharArray(), first.GetEpoch().GetSecondsFromJ2000().count(),
                 last.GetEpoch().GetSecondsFromJ2000().count(), "Seg1", DefinePolynomialDegree(states.size(), 15), (SpiceInt) size, statesArray, epochs);
        delete[] epochs;
    }
    spkcls_c(handle);

    furnsh_c(m_filePath.c_str());

    delete[] statesArray;
}

bool IO::Astrodynamics::Kernels::EphemerisKernel::IsEvenlySpacedData(const std::vector<OrbitalParameters::StateVector> &states)
{

    if (states.empty())
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("State set must have one or more");
    }

    if (states.size() == 1)
    {
        return true;
    }

    IO::Astrodynamics::Time::TimeSpan gap{states[1].GetEpoch() - states[0].GetEpoch()};
    for (size_t i = 1; i < states.size() - 1; i++)
    {
        if (gap != states[i + 1].GetEpoch() - states[i].GetEpoch())
        {
            return false;
        }
    }

    return true;
}


