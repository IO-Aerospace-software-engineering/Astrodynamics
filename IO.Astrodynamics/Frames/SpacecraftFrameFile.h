/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef SPACECRAFT_FRAME_H
#define SPACECRAFT_FRAME_H

#include <FrameFile.h>

//Forward declaration
namespace IO::Astrodynamics::Body::Spacecraft {
    class Spacecraft;
}

namespace IO::Astrodynamics::Frames {
    /**
     * @brief Spacecraft frame file
     *
     */
    class SpacecraftFrameFile final : public IO::Astrodynamics::Frames::FrameFile {
    private:
        const int m_id;
        const IO::Astrodynamics::Body::Spacecraft::Spacecraft &m_spacecraft;

        void BuildFrame() override;

        /**
         * @brief Construct a new Spacecraft Frame File object
         *
         * @param spacecraft
         */
        explicit SpacecraftFrameFile(const IO::Astrodynamics::Body::Spacecraft::Spacecraft &spacecraft);

    public:
        friend class IO::Astrodynamics::Body::Spacecraft::Spacecraft;

        [[nodiscard]] inline int GetId() const { return m_id; }
    };
}
#endif
