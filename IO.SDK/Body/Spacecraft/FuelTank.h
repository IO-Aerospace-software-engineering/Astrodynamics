/**
 * @file FuelTank.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-03-04
 * 
 * @copyright Copyright (c) 2021
 * 
 */

#ifndef FUEL_TANK_H
#define FUEL_TANK_H

#include <memory>
#include <vector>
#include <string>

namespace IO::SDK::Body::Spacecraft
{
    class Spacecraft;
    /**
     * @brief Fuel tank class
     * 
     */
    class FuelTank final
    {
    private:
        const std::string m_serialNumber{};
        const double m_capacity{};
        const double m_initialQuantity{};
        const IO::SDK::Body::Spacecraft::Spacecraft &m_spacecraft;

        double m_quantity{};

    public:
        /**
         * @brief Construct a new Fuel Tank object
         * 
         * @param id 
         * @param capacity 
         * @param quantity 
         */
        FuelTank(const std::string &serialNumber, const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft, double capacity, double quantity);
        
        /**
         * @brief Get the associated spacecraft
         * 
         * @return const IO::SDK::Body::Spacecraft::Spacecraft& 
         */
        [[nodiscard]] const IO::SDK::Body::Spacecraft::Spacecraft &GetSpacecraft() const;

        /**
         * @brief Get the fuel tank serial number
         * 
         * @return std::string 
         */
        [[nodiscard]] std::string GetSerialNumber() const;

        /**
         * @brief Get the current Quantity
         * 
         * @return double 
         */
        [[nodiscard]] double GetQuantity() const;

        /**
         * @brief Get the Initial Quantity
         * 
         * @return double 
         */
        [[nodiscard]] double GetInitialQuantity() const;

        /**
         * @brief Get the fuel tank capacity
         * 
         * @return double 
         */
        [[nodiscard]] double GetCapacity() const;

        /**
         * @brief Know if fuel tank is empty
         * 
         * @return true 
         * @return false 
         */
        [[nodiscard]] bool IsEmpty() const;

        /**
         * @brief Update the fuel tank quantity
         * 
         * @param quantityToAdd Quantity to add. Use negative number to remove fuel
         */
        void UpdateFuelQuantity(double quantityToAdd);
        
        bool operator==(const IO::SDK::Body::Spacecraft::FuelTank &other) const { return m_serialNumber == other.m_serialNumber; }
        bool operator!=(const IO::SDK::Body::Spacecraft::FuelTank &other) const { return !(m_serialNumber == other.m_serialNumber); }
        bool operator<(const IO::SDK::Body::Spacecraft::FuelTank &other) const { return m_serialNumber < other.m_serialNumber; }
        bool operator<=(const IO::SDK::Body::Spacecraft::FuelTank &other) const { return m_serialNumber <= other.m_serialNumber; }
        bool operator>(const IO::SDK::Body::Spacecraft::FuelTank &other) const { return m_serialNumber > other.m_serialNumber; }
        bool operator>=(const IO::SDK::Body::Spacecraft::FuelTank &other) const { return m_serialNumber >= other.m_serialNumber; }
    };
}

#endif