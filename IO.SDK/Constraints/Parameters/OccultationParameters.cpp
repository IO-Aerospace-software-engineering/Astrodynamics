//
// Created by spacer on 3/1/23.
//

#include <OccultationParameters.h>

IO::SDK::Constraints::Parameters::OccultationParameters::OccultationParameters(const IO::SDK::Body::Body &observer,
                                                                               const IO::SDK::Body::CelestialBody &front, const IO::SDK::Body::Body &back,
                                                                               const IO::SDK::OccultationType &occultationType, const IO::SDK::AberrationsEnum aberration,
                                                                               const IO::SDK::Time::TimeSpan &initialStepSize) : m_observer{observer},
                                                                                                                                 m_front{front},
                                                                                                                                 m_back{back}, m_occultationType{occultationType},
                                                                                                                                 m_aberration{aberration},
                                                                                                                                 m_initialStepSize{initialStepSize}
{

}
