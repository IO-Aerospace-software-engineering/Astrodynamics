/**
 * @file AzimuthRange.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-05-06
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef AZIMUTH_RANGE_H
#define AZIMUTH_RANGE_H

namespace IO::SDK::Coordinates
{
    class AzimuthRange
    {
    private:
        const double m_start;
        const double m_end;
        const double m_span{};

    public:
        /**
         * @brief Construct a new Azimuth Range object
         * 
         * @param start Start range [-2pi;2pi]
         * @param end End range [-2pi;2pi]
         */
        AzimuthRange(double start, double end);

        /**
         * @brief Know if angle is in range
         * 
         * @param angle 
         * @return true 
         * @return false 
         */
        [[nodiscard]] bool IsInRange(double angle) const;

        /**
         * @brief Get the Start
         * 
         * @return double 
         */
        [[nodiscard]] double GetStart() const { return m_start; }

        /**
         * @brief Get the End
         * 
         * @return double 
         */
        [[nodiscard]] double GetEnd() const { return m_end; }

        /**
         * @brief Get the Span
         * 
         * @return double 
         */
        [[nodiscard]] double GetSpan() const { return m_span; }

        /**
         * @brief Know if an azimuth range intersects this azimuth range
         * 
         * @param azimuthRange 
         * @return true 
         * @return false 
         */
        [[nodiscard]] bool IsIntersected(const AzimuthRange& azimuthRange) const;
    };

}
#endif