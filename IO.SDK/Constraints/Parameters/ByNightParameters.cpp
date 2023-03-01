//
// Created by spacer on 3/1/23.
//

#include <ByNightParameters.h>

IO::SDK::Constraints::Parameters::ByNightParameters::ByNightParameters(const IO::SDK::Time::Window<IO::SDK::Time::UTC>& window, const double twilightDefinition) :
        m_window{window},
        m_twilightDefinition{twilightDefinition}
{

}
