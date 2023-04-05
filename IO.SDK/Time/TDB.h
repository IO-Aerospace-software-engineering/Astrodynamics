/**
 * @file TDB.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-03-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef TDB_H
#define TDB_H

#include <DateTime.h>

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
		explicit TDB(std::chrono::duration<double> ellapsedSecondsFromJ2000);

		/**
		 * @brief Construct a new TDB object
		 * 
		 * @param string 
		 */
		explicit TDB(const std::string& string);

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
		 * @return IO::SDK::Time::TDB 
		 */
		[[nodiscard]] IO::SDK::Time::TDB Add(const IO::SDK::Time::TimeSpan &timespan) const;

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
		[[nodiscard]] UTC ToUTC() const;
	};
}
#endif // !TDB_H
