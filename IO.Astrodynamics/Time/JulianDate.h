/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by s.guillet on 26/07/2023.
//

#ifndef IO_JULIANDATE_H
#define IO_JULIANDATE_H

#include <DateTime.h>
#include <UTC.h>
#include <TDB.h>


namespace IO::Astrodynamics::Time
{
    class JulianDate final : public IO::Astrodynamics::Time::DateTime
    {
    private:
        const std::chrono::duration<double, std::ratio<86400>> m_julianDate{};
    public:
        explicit JulianDate(std::chrono::duration<double, std::ratio<86400>> julianDate);

        [[nodiscard]] inline std::chrono::duration<double, std::ratio<86400>> GetJulianDate() const
        { return m_julianDate; }

        /**
		 * @brief Get string representation
		 *
		 * @return std::string
		 */
        [[nodiscard]] std::string ToString() const override;

        /**
         * @brief Add TimeSpan to datetime
         *
         * @param timespan
         * @return IO::Astrodynamics::Time::TDB
         */
        [[nodiscard]] IO::Astrodynamics::Time::JulianDate Add(const IO::Astrodynamics::Time::TimeSpan &timespan) const;

        /**
         * @brief Add TimeSpan to DateTime
         *
         * @param timespan
         * @return IO::Astrodynamics::Time::TDB
         */
        JulianDate operator+(const IO::Astrodynamics::Time::TimeSpan &timespan) const;

        /**
         * @brief Substract TimeSpan
         *
         * @param other
         * @return IO::Astrodynamics::Time::TDB
         */
        JulianDate operator-(const IO::Astrodynamics::Time::TimeSpan &other) const;

        /**
         * @brief Substract DateTime
         *
         * @param other
         * @return IO::Astrodynamics::Time::TimeSpan
         */
        TimeSpan operator-(const IO::Astrodynamics::Time::JulianDate &other) const;

        /**
         * @brief Convert to UTC
         *
         * @return UTC
         */
        [[nodiscard]] IO::Astrodynamics::Time::UTC ToUTC() const;

        [[nodiscard]] IO::Astrodynamics::Time::TDB ToTDB() const;
    };
}


#endif //IO_JULIANDATE_H
