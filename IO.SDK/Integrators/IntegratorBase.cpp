/**
 * @file IntegratorBase.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <IntegratorBase.h>

using namespace std::chrono_literals;

IO::SDK::Integrators::IntegratorBase::IntegratorBase(const IO::SDK::Time::TimeSpan &stepDuration) : m_stepDuration{stepDuration}, m_h{stepDuration.GetSeconds().count()}, m_half_h{m_h * 0.5}
{
}
