/**
 * @file UTC.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-05-07
 * 
 * @copyright Copyright (c) 2021
 * 
 */

#ifndef UTC_H
#define UTC_H

#include <string>
#include <chrono>

#include <DateTime.h>

namespace IO::SDK::Time
{
    class TDB;
    class UTC final: public IO::SDK::Time::DateTime
    {
    private:
        /* data */
    public:
        UTC(const std::chrono::duration<double> ellapsedSecondsFromJ2000);

        UTC(const std::string string);
        ~UTC() = default;

        IO::SDK::Time::UTC Add(const IO::SDK::Time::TimeSpan &timespan) const;

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
        std::string ToString() const override;

        TDB ToTDB() const;
    };

} // namespace IO::SDK::Time

#endif