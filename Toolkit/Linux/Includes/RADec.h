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
		const double _range, _ra, _dec;

	public:

		/// <summary>
		/// Instanciate Right anscension and declination coordinates
		/// </summary>
		/// <param name="range"></param>
		/// <param name="ra"></param>
		/// <param name="dec"></param>
		RADec( const double ra, const double dec, const double range) :_range{ range }, _ra{ ra }, _dec{ dec }
		{

		}

		/// <summary>
		/// Get yhe range
		/// </summary>
		/// <returns></returns>
		double GetRange() const { return this->_range; }

		/// <summary>
		/// Get the right ascension in radians
		/// </summary>
		/// <returns></returns>
		double GetRA() const { return this->_ra; }

		/// <summary>
		/// get the declination in radians
		/// </summary>
		/// <returns></returns>
		double GetDec() const { return this->_dec; }
	};
}
#endif // !RADEC_H


