//
// Created by sylvain guillet on 3/1/23.
//

#include <BodyVisibilityFromSiteParameters.h>

IO::SDK::Constraints::Parameters::BodyVisibilityFromSiteParameters::BodyVisibilityFromSiteParameters(const IO::SDK::Sites::Site &site, const IO::SDK::Body::Body &target,
                                                                                                     IO::SDK::AberrationsEnum aberration) : m_site{site}, m_target{target},
                                                                                                                                                  m_aberration{aberration}
{

}
