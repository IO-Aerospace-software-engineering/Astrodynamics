/**
 * @file DateTime.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-12
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef DATETIME_H
#define DATETIME_H

#include <string>
#include <SpiceUsr.h>
#include <TimeSpan.h>
#include <chrono>
#include <SpiceUsr.h>

namespace IO::SDK::Time
{
	class DateTime
	{
	private:
	protected:
		const std::chrono::duration<double> m_secondsFromJ2000{};
		DateTime();
		virtual ~DateTime() = default;

		/// <summary>
		/// Instanciate from seconds ellapsed from J2000
		/// </summary>
		/// <param name="secondsFromJ2000"></param>
		/// <param name="timeSystem"></param>
		DateTime(const std::chrono::duration<double> secondsFromJ2000);

	public:
		/// <summary>
		/// Ellapsed seconds from J2000
		/// </summary>
		/// <returns></returns>
		std::chrono::duration<double> GetSecondsFromJ2000() const;

		/// <summary>
		/// Get ISO UTC string
		/// </summary>
		/// <returns></returns>
		virtual std::string ToString() const = 0;

		/// <summary>
		/// Substract two DateTimes
		/// </summary>
		/// <param name="datetime">Substracted this datetime</param>
		/// <returns></returns>
		IO::SDK::Time::TimeSpan Substract(const IO::SDK::Time::DateTime &datetime) const;

		/// <summary>
		/// Substract two DateTimes
		/// </summary>
		/// <param name="other">Substracted this datetime</param>
		/// <returns></returns>
		IO::SDK::Time::TimeSpan operator-(const IO::SDK::Time::DateTime &other) const;

		bool operator==(const IO::SDK::Time::DateTime &other) const;
		bool operator!=(const IO::SDK::Time::DateTime &other) const;
		bool operator>(const IO::SDK::Time::DateTime &other) const;
		bool operator>=(const IO::SDK::Time::DateTime &other) const;
		bool operator<(const IO::SDK::Time::DateTime &other) const;
		bool operator<=(const IO::SDK::Time::DateTime &other) const;

		virtual double ToJulian() const;

		double CenturiesFromJ2000() const;

		DateTime &operator=(const DateTime &datetime);
	};
}
#endif
