#ifndef RADEC_H
#define RADEC_H
namespace IO::SDK::Coordinates
{
	/// <summary>
	/// Right ascension and declination coordinates
	/// </summary>
	class RADec
	{
	private:
		double _range{}, _ra{}, _dec{};

	public:

		/// <summary>
		/// Instanciate Right anscension and declination coordinates
		/// </summary>
		/// <param name="range"></param>
		/// <param name="ra"></param>
		/// <param name="dec"></param>
		RADec( double ra, double dec, double range) :_range{ range }, _ra{ ra }, _dec{ dec }
		{

		}

		/// <summary>
		/// Get yhe range
		/// </summary>
		/// <returns></returns>
		double GetRange() { return this->_range; }

		/// <summary>
		/// Get the right ascension in radians
		/// </summary>
		/// <returns></returns>
		double GetRA() { return this->_ra; }

		/// <summary>
		/// get the declination in radians
		/// </summary>
		/// <returns></returns>
		double GetDec() { return this->_dec; }
	};
}
#endif // !RADEC_H


