/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef WINDOW_H
#define WINDOW_H

#include <DateTime.h>
#include <TimeSpan.h>
#include <type_traits>
#include <SDKException.h>
#include <UTC.h>
#include <TDB.h>

namespace IO::Astrodynamics::Time
{

    template<typename T>
    class Window
    {

    private:
        static_assert(std::is_base_of<DateTime, T>::value, "T must extend DateTime");
        const T m_start;
        const T m_end;
        const TimeSpan m_length;

    public:
        Window(T startdate, TimeSpan length) : m_start{startdate}, m_end{m_start + length}, m_length{length}
        {};

        Window(T startdate, T endDate) : m_start{startdate}, m_end{endDate}, m_length{endDate - startdate}
        {};

        Window(const Window<T> &window) : m_start{window.m_start}, m_end{window.m_end}, m_length{window.m_length}
        {
        }

        T GetStartDate() const
        { return m_start; }

        T GetEndDate() const
        { return m_end; }

        [[nodiscard]] TimeSpan GetLength() const
        { return m_length; }

        bool operator==(const Window<T> &window) const
        { return m_start == window.m_start && m_end == window.m_end; }

        bool operator!=(const Window<T> &window) const
        { return !(*this == window); }

        bool Intersects(const Window<T> &window) const
        {
            return !(window.GetStartDate() >= m_end || window.GetEndDate() <= m_start);
        }


        bool Contains(const T &epoch) const
        {
            return epoch > m_start && epoch < m_end;
        }

        Window<T> GetIntersection(const Window<T> &window) const
        {
            if (!Intersects(window))
            {
                throw IO::Astrodynamics::Exception::SDKException("Windows don't intersect");
            }

            T min = m_start > window.m_start ? m_start : window.m_start;
            T max = m_end < window.m_end ? m_end : window.m_end;
            return Window<T>(min, max);
        }

        Window<T> Merge(const Window<T> &window) const
        {
            T min = m_start < window.m_start ? m_start : window.m_start;
            T max = m_end > window.m_end ? m_end : window.m_end;
            return Window<T>(min, max);
        }

        Window<T> &operator=(const Window<T> &window)
        {
            // Guard self assignment
            if (this == &window)
                return *this;

            const_cast<T &>(m_start) = window.m_start;
            const_cast<T &>(m_end) = window.m_end;
            const_cast<IO::Astrodynamics::Time::TimeSpan &>(m_length) = window.m_length;
            return *this;
        }
    };
}
#endif // !WINDOW_H
