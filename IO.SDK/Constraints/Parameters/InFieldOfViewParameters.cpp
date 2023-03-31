//
// Created by sylvain guillet on 3/7/23.
//

#include "InFieldOfViewParameters.h"

IO::SDK::Constraints::Parameters::InFieldOfViewParameters::InFieldOfViewParameters(const SDK::Instruments::Instrument &instrument, const SDK::Body::Body &targetBody,
                                                                                   IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TimeSpan &initialStepSize)
        : m_instrument{instrument}, m_targetBody{targetBody}, m_aberration{aberration}, m_initialStepSize{initialStepSize}
{}
