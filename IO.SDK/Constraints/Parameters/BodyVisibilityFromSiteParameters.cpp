//
// Created by spacer on 3/1/23.
//

#include <BodyVisibilityFromSiteParameters.h>

IO::SDK::Constraints::Parameters::BodyVisibilityFromSiteParameters::BodyVisibilityFromSiteParameters(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &window,
                                                                                                     const IO::SDK::Sites::Site &site, const IO::SDK::Body::Body target,
                                                                                                     const IO::SDK::AberrationsEnum aberration) : m_window{window},
                                                                                                                                                  m_target{target},
                                                                                                                                                  m_aberration{aberration},
                                                                                                                                                  m_site{site}
{

}
