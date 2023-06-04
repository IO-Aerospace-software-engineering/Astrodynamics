/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef RADEC_H
#define RADEC_H
namespace IO::SDK::Coordinates
{
	/**
	 * @brief Right ascending and declination coordinate
	 * 
	 */
	class Equatorial
	{
	private:
		const double _range, _ra, _dec;

	public:

		/**
		 * @brief Construct a new Equatorial object
		 * 
		 * @param ra 
		 * @param dec 
		 * @param range 
		 */
		Equatorial(double ra, double dec, double range) : _range{range }, _ra{ra }, _dec{dec }
		{

		}

		/**
		 * @brief Get the Range
		 * 
		 * @return double 
		 */
		[[nodiscard]] inline double GetRange() const { return this->_range; }

		/**
		 * @brief 
		 * 
		 * @return double 
		 */
		[[nodiscard]] inline double GetRA() const { return this->_ra; }

		/**
		 * @brief Get the Dec
		 * 
		 * @return double 
		 */
		[[nodiscard]] inline double GetDec() const { return this->_dec; }
	};
}
#endif // !RADEC_H


