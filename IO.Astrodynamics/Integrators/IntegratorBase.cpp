/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <IntegratorBase.h>

using namespace std::chrono_literals;

IO::SDK::Integrators::IntegratorBase::IntegratorBase(const IO::SDK::Time::TimeSpan &stepDuration) : m_stepDuration{stepDuration}, m_h{stepDuration.GetSeconds().count()}, m_half_h{m_h * 0.5}
{
}
