//
// Created by s.guillet on 07/07/2023.
//

#include "Barycenter.h"
#include "CelestialBody.h"
#include "Constants.h"

IO::Astrodynamics::Body::Barycenter::Barycenter(int id) : CelestialItem(id, "", IO::Astrodynamics::Body::CelestialBody::ReadGM(id) / IO::Astrodynamics::Constants::G)
{
    SpiceBoolean found;
    SpiceChar name[32];
    bodc2n_c(id, 32, name, &found);
    if (!found)
    {
        throw IO::Astrodynamics::Exception::SDKException("Barycenter id" + std::to_string(id) + " can't be found");
    }

    const_cast<std::string &>(m_name) = name;
}

