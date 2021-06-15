/**
 * @file Payload.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-03-04
 * 
 * @copyright Copyright (c) 2021
 * 
 */

#ifndef PAYLOAD_H
#define PAYLOAD_H

#include <string>

namespace IO::SDK::Body::Spacecraft
{
	/**
	 * @brief Payload class
	 * 
	 */
	class Payload
	{
	private:
		const std::string m_serialNumber{};
		const std::string m_name{};
		const double m_mass{};

	public:
		/**
		 * @brief Construct a new Payload object
		 * 
		 * @param name 
		 * @param mass 
		 */
		Payload(const std::string &serialNumber, const std::string& name, const double mass);

		/**
		 * @brief Get the Name object
		 * 
		 * @return std::string 
		 */
		std::string GetName() const;

		/**
		 * @brief Get the Mass object
		 * 
		 * @return double 
		 */
		double GetMass() const;

		/**
		 * @brief Get the Serial Number object
		 * 
		 * @return std::string 
		 */
		std::string GetSerialNumber() const;

		bool operator==(const IO::SDK::Body::Spacecraft::Payload &other) const { return m_serialNumber == other.m_serialNumber; };
        bool operator!=(const IO::SDK::Body::Spacecraft::Payload &other) const { return !(m_serialNumber == other.m_serialNumber); };
	};
}
#endif
