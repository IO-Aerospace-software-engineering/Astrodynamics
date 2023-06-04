/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef UTC_H
#define UTC_H

#include <DateTime.h>

namespace IO::SDK::Time
{
    class TDB;
    class UTC final: public IO::SDK::Time::DateTime
    {
    private:
        /* data */
    public:
        explicit UTC(std::chrono::duration<double> ellapsedSecondsFromJ2000);

        explicit UTC(const std::string& string);
        ~UTC() override = default;

        [[nodiscard]] IO::SDK::Time::UTC Add(const IO::SDK::Time::TimeSpan &timespan) const;

        /**
         * @brief 
         * 
         * @param timespan 
         * @return IO::SDK::Time::UTC 
         */
        IO::SDK::Time::UTC operator+(const IO::SDK::Time::TimeSpan &timespan) const;

        /**
         * @brief Get UTC string
         * 
         * @return std::string 
         */
        [[nodiscard]] std::string ToString() const override;

        [[nodiscard]] TDB ToTDB() const;
    };

} // namespace IO::SDK::Time

#endif