/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef DATETIME_H
#define DATETIME_H

#include <string>
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
		DateTime(const DateTime& datetime);
		virtual ~DateTime() = default;

		/// <summary>
		/// Instanciate from seconds ellapsed from J2000
		/// </summary>
		/// <param name="secondsFromJ2000"></param>
		/// <param name="timeSystem"></param>
		explicit DateTime(const std::chrono::duration<double>& secondsFromJ2000);

	public:
		/// <summary>
		/// Ellapsed seconds from J2000
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] std::chrono::duration<double> GetSecondsFromJ2000() const;

		/// <summary>
		/// Get ISO UTC string
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] virtual std::string ToString() const = 0;

		/// <summary>
		/// Substract two DateTimes
		/// </summary>
		/// <param name="datetime">Substracted this datetime</param>
		/// <returns></returns>
		[[nodiscard]] IO::SDK::Time::TimeSpan Subtract(const IO::SDK::Time::DateTime &datetime) const;

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

		[[nodiscard]] virtual double ToJulian() const;

		[[nodiscard]] double CenturiesFromJ2000() const;

		DateTime &operator=(const DateTime &datetime);
	};
}
#endif
