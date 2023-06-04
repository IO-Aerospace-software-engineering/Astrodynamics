/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef TIMESPAN_H
#define TIMESPAN_H
#include<chrono>

namespace IO::Astrodynamics::Time
{
	using namespace std::literals::chrono_literals;
	class TimeSpan
	{
	private:
		std::chrono::duration<double> m_seconds{};

	public:

		TimeSpan();
		/// <summary>
		/// Instantiate time span
		/// </summary>
		/// <param name="period">Period is defined with literal in h - min - s - ms - us - ns. If literal is not specified second will be used</param>
		explicit TimeSpan(std::chrono::duration<double> period);


		explicit TimeSpan(double period);

		[[nodiscard]] std::chrono::duration<double, std::nano> GetNanoseconds() const;
		[[nodiscard]] std::chrono::duration<double, std::micro> GetMicroseconds()const;
		[[nodiscard]] std::chrono::duration<double, std::milli> GetMilliseconds()const;
		[[nodiscard]] std::chrono::duration<double> GetSeconds() const;
		[[nodiscard]] std::chrono::duration<double, std::ratio<60>> GetMinutes()const;
		[[nodiscard]] std::chrono::duration<double, std::ratio<3600>> GetHours()const;

		IO::Astrodynamics::Time::TimeSpan operator+(const IO::Astrodynamics::Time::TimeSpan& ts) const;
		IO::Astrodynamics::Time::TimeSpan operator+(double val) const;
		IO::Astrodynamics::Time::TimeSpan operator-(const IO::Astrodynamics::Time::TimeSpan& ts) const;
		IO::Astrodynamics::Time::TimeSpan operator*(double ts) const;
		IO::Astrodynamics::Time::TimeSpan operator/(double ts) const;
		bool operator==(const IO::Astrodynamics::Time::TimeSpan& ts) const;
		bool operator!=(const IO::Astrodynamics::Time::TimeSpan& ts) const;
		bool operator<(const IO::Astrodynamics::Time::TimeSpan& ts) const;
		bool operator>(const IO::Astrodynamics::Time::TimeSpan& ts) const;
		bool operator>=(const IO::Astrodynamics::Time::TimeSpan& ts) const;
		bool operator<=(const IO::Astrodynamics::Time::TimeSpan& ts) const;
	};
}
#endif
