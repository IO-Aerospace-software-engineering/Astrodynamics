//
// Created by spacer on 3/1/23.
//

#include <BodyVisibilityParameters.h>

IO::SDK::Constraints::Parameters::BodyVisibilityParameters::BodyVisibilityParameters(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &window, const IO::SDK::Body::Body target,
                                                                                     const IO::SDK::AberrationsEnum aberration) : m_window{window}, m_target{target},
                                                                                                                                  m_aberration{aberration}
{

}
