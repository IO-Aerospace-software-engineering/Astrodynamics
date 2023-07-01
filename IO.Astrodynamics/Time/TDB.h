/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef TDB_H
#define TDB_H

#include <DateTime.h>

namespace IO::Astrodynamics::Time
{
	class UTC;
	class TDB final : public IO::Astrodynamics::Time::DateTime
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
		 * @return IO::Astrodynamics::Time::TDB
		 */
		[[nodiscard]] IO::Astrodynamics::Time::TDB Add(const IO::Astrodynamics::Time::TimeSpan &timespan) const;

		/**
		 * @brief Add TimeSpan to DateTime
		 * 
		 * @param timespan 
		 * @return IO::Astrodynamics::Time::TDB
		 */
		IO::Astrodynamics::Time::TDB operator+(const IO::Astrodynamics::Time::TimeSpan &timespan) const;

		/**
		 * @brief Substract TimeSpan
		 * 
		 * @param other 
		 * @return IO::Astrodynamics::Time::TDB
		 */
		IO::Astrodynamics::Time::TDB operator-(const IO::Astrodynamics::Time::TimeSpan &other) const;

		/**
		 * @brief Substract DateTime
		 * 
		 * @param other 
		 * @return IO::Astrodynamics::Time::TimeSpan
		 */
		IO::Astrodynamics::Time::TimeSpan operator-(const IO::Astrodynamics::Time::TDB &other) const;

		/**
		 * @brief Convert to UTC
		 * 
		 * @return UTC 
		 */
		[[nodiscard]] UTC ToUTC() const;
	};
}
#endif // !TDB_H
