/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef CONSTRAINT_H
#define CONSTRAINT_H

#include <string>

namespace IO::Astrodynamics::Constraints
{
    class RelationalOperator
    {
    private:
        const std::string m_name;
        static RelationalOperator mGreaterThan;
        static RelationalOperator mLowerThan;
        static RelationalOperator mEqual;
        static RelationalOperator mAbsMin;
        static RelationalOperator mAbsMax;
        static RelationalOperator mLocalMin;
        static RelationalOperator mLocalMax;

    public:
        /**
         * @brief Construct a new RelationalOperator object
         * 
         * @param name 
         */
        explicit RelationalOperator(std::string name);

        RelationalOperator(const RelationalOperator &relationalOperator) = default;
        IO::Astrodynamics::Constraints::RelationalOperator &operator=(const RelationalOperator &other);

        /**
         * @brief Get char array constraint name
         * 
         * @return const char* 
         */
        [[nodiscard]] const char *ToCharArray() const;

        static IO::Astrodynamics::Constraints::RelationalOperator &GreaterThan();

        static IO::Astrodynamics::Constraints::RelationalOperator &LowerThan();

        static IO::Astrodynamics::Constraints::RelationalOperator &Equal();

        static IO::Astrodynamics::Constraints::RelationalOperator &AbsMin();

        static IO::Astrodynamics::Constraints::RelationalOperator &AbsMax();

        static IO::Astrodynamics::Constraints::RelationalOperator &LocalMin();

        static IO::Astrodynamics::Constraints::RelationalOperator &LocalMax();

        static IO::Astrodynamics::Constraints::RelationalOperator ToRelationalOperator(const std::string &relationalOperator);
    };

} // namespace IO::Astrodynamics

#endif