/**
 * @file TDB.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-03-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef TDB_H
#define TDB_H

#include <DateTime.h>
#include <chrono>
#include <string>

namespace IO::SDK::Time
{
	class UTC;
	class TDB final : public IO::SDK::Time::DateTime
	{
	private:
	public:
		/**
		 * @brief Construct a new TDB object
		 * 
		 * @param ellapsedSecondsFromJ2000 
		 */
		TDB(const std::chrono::duration<double> ellapsedSecondsFromJ2000);

		/**
		 * @brief Construct a new TDB object
		 * 
		 * @param string 
		 */
		TDB(const std::string string);

		/**
		 * @brief Get string representation
		 * 
		 * @return std::string 
		 */
		std::string ToString() const override;

		/**
		 * @brief Add TimeSpan to datetime
		 * 
		 * @param timespan 
		 * @return IO::SDK::Time::TDB 
		 */
		IO::SDK::Time::TDB Add(const IO::SDK::Time::TimeSpan &timespan) const;

		/**
		 * @brief Add TimeSpan to DateTime
		 * 
		 * @param timespan 
		 * @return IO::SDK::Time::TDB 
		 */
		IO::SDK::Time::TDB operator+(const IO::SDK::Time::TimeSpan &timespan) const;

		/**
		 * @brief Substract TimeSpan
		 * 
		 * @param other 
		 * @return IO::SDK::Time::TDB 
		 */
		IO::SDK::Time::TDB operator-(const IO::SDK::Time::TimeSpan &other) const;

		/**
		 * @brief Substract DateTime
		 * 
		 * @param other 
		 * @return IO::SDK::Time::TimeSpan 
		 */
		IO::SDK::Time::TimeSpan operator-(const IO::SDK::Time::TDB &other) const;

		/**
		 * @brief Convert to UTC
		 * 
		 * @return UTC 
		 */
		UTC ToUTC() const;
	};
}
#endif // !TDB_H
