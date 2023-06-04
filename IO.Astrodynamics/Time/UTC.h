/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef UTC_H
#define UTC_H

#include <DateTime.h>

namespace IO::Astrodynamics::Time
{
    class TDB;
    class UTC final: public IO::Astrodynamics::Time::DateTime
    {
    private:
        /* data */
    public:
        explicit UTC(std::chrono::duration<double> ellapsedSecondsFromJ2000);

        explicit UTC(const std::string& string);
        ~UTC() override = default;

        [[nodiscard]] IO::Astrodynamics::Time::UTC Add(const IO::Astrodynamics::Time::TimeSpan &timespan) const;

        /**
         * @brief 
         * 
         * @param timespan 
         * @return IO::Astrodynamics::Time::UTC
         */
        IO::Astrodynamics::Time::UTC operator+(const IO::Astrodynamics::Time::TimeSpan &timespan) const;

        /**
         * @brief Get UTC string
         * 
         * @return std::string 
         */
        [[nodiscard]] std::string ToString() const override;

        [[nodiscard]] TDB ToTDB() const;
    };

} // namespace IO::Astrodynamics::Time

#endif