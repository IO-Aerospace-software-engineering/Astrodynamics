//
// Created by sylvain guillet on 3/1/23.
//

#include <ByDayParameters.h>

IO::SDK::Constraints::Parameters::ByDayParameters::ByDayParameters(const IO::SDK::Sites::Site &site, const double twilightDefinition) :
        m_twilightDefinition{twilightDefinition},
        m_site{site}
{

}