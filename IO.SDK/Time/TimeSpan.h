/**
 * @file TimeSpan.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef TIMESPAN_H
#define TIMESPAN_H
#include<string>
#include<chrono>

namespace IO::SDK::Time
{
	using namespace std::literals::chrono_literals;
	class TimeSpan
	{
	private:
		std::chrono::duration<double> m_seconds{};

	public:

		TimeSpan();
		/// <summary>
		/// Instanciate time span
		/// </summary>
		/// <param name="period">Period is defined with literal in h - min - s - ms - us - ns. If literal is not specified second will be used</param>
		TimeSpan(const std::chrono::duration<double> period);

		std::chrono::duration<double, std::nano> GetNanoseconds() const;
		std::chrono::duration<double, std::micro> GetMicroseconds()const;
		std::chrono::duration<double, std::milli> GetMilliseconds()const;
		std::chrono::duration<double> GetSeconds() const;
		std::chrono::duration<double, std::ratio<60>> GetMinutes()const;
		std::chrono::duration<double, std::ratio<3600>> GetHours()const;

		IO::SDK::Time::TimeSpan operator+(const IO::SDK::Time::TimeSpan& ts) const;
		IO::SDK::Time::TimeSpan operator+(const double val) const;
		IO::SDK::Time::TimeSpan operator-(const IO::SDK::Time::TimeSpan& ts) const;
		IO::SDK::Time::TimeSpan operator*(const double ts) const;
		IO::SDK::Time::TimeSpan operator/(const double ts) const;
		bool operator==(const IO::SDK::Time::TimeSpan& ts) const;
		bool operator!=(const IO::SDK::Time::TimeSpan& ts) const;
		bool operator<(const IO::SDK::Time::TimeSpan& ts) const;
		bool operator>(const IO::SDK::Time::TimeSpan& ts) const;
		bool operator>=(const IO::SDK::Time::TimeSpan& ts) const;
		bool operator<=(const IO::SDK::Time::TimeSpan& ts) const;
	};
}
#endif
