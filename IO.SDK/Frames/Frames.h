/**
 * @file Frames.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-04-29
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef FRAMES_H
#define FRAMES_H

#include <string>
#include <Matrix.h>
#include <TDB.h>
#include <Vector3D.h>

namespace IO::SDK::Frames
{
    /**
     * @brief Frames base class
     * 
     */
    class Frames
    {
    protected:
        std::string m_name;

    public:
        /**
         * @brief Construct a new Frames object
         * 
         * @param strView 
         */
        explicit Frames(std::string strView);

        /**
         * @brief Get frame name
         * 
         * @return const char* 
         */
        [[nodiscard]] const char *ToCharArray() const;

        /**
         * @brief Equality comparer
         * 
         * @param frame 
         * @return true 
         * @return false 
         */
        bool operator==(const IO::SDK::Frames::Frames &frame) const;

        /**
         * @brief Equality comparer
         * 
         * @param frame 
         * @return true 
         * @return false 
         */
        bool operator!=(const IO::SDK::Frames::Frames &frame) const;

        /**
         * @brief Equality comparer
         * 
         * @param frame 
         * @return true 
         * @return false 
         */
        bool operator==(IO::SDK::Frames::Frames &frame) const;

        /**
         * @brief Equality comparer
         * 
         * @param frame 
         * @return true 
         * @return false 
         */
        bool operator!=(IO::SDK::Frames::Frames &frame) const;

        /**
         * @brief Get the Name
         * 
         * @return std::string 
         */
        [[nodiscard]] std::string GetName() const;

        /**
         * @brief Get the 6X6 matrix to transform frame to another
         * 
         * @param frame 
         * @param epoch 
         * @return IO::SDK::Math::Matrix 
         */
        [[nodiscard]] IO::SDK::Math::Matrix ToFrame6x6(const Frames &frame, const IO::SDK::Time::TDB &epoch) const;

        /**
         * @brief Get the 3x3 matrix to transform frame to another
         * 
         * @param frame 
         * @param epoch 
         * @return IO::SDK::Math::Matrix 
         */
        [[nodiscard]] IO::SDK::Math::Matrix ToFrame3x3(const Frames &frame, const IO::SDK::Time::TDB &epoch) const;

        /**
         * @brief Transform vector from frame to another
         * 
         * @param from 
         * @param to 
         * @param vector 
         * @return IO::SDK::Math::Vector3D 
         */
        [[nodiscard]] IO::SDK::Math::Vector3D TransformVector(const Frames &to, const IO::SDK::Math::Vector3D &vector, const IO::SDK::Time::TDB &epoch) const;
    };
}
#endif