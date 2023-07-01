/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <DataPoolMonitoring.h>
#include <SpiceUsr.h>

IO::Astrodynamics::DataPoolMonitoring IO::Astrodynamics::DataPoolMonitoring::m_instance;

IO::Astrodynamics::DataPoolMonitoring &IO::Astrodynamics::DataPoolMonitoring::Instance()
{
    return m_instance;
}

std::vector<std::string> IO::Astrodynamics::DataPoolMonitoring::GetStringProperty(const std::string &propertyName, int nbValuesExpected)
{
    SpiceInt n{};
    SpiceChar values[10][100];
    SpiceBoolean found{false};

    gcpool_c(propertyName.c_str(), 0, nbValuesExpected, 100, &n, values, &found);

    std::vector<std::string> res;

    if (found)
    {
        for (int i = 0; i < n; i++)
        {
            res.emplace_back(values[i]);
        }
    }

    return res;
}

std::vector<int> IO::Astrodynamics::DataPoolMonitoring::GetIntegerProperty(const std::string &propertyName, int nbValuesExpected)
{
    SpiceInt n{};
    auto values = new SpiceInt[nbValuesExpected];
    SpiceBoolean found{false};

    gipool_c(propertyName.c_str(), 0, nbValuesExpected, &n, values, &found);

    std::vector<int> res;

    res.reserve(n);
    for (int i = 0; i < n; i++)
    {
        res.push_back(values[i]);
    }

    delete[] values;
    return res;
}

std::vector<double> IO::Astrodynamics::DataPoolMonitoring::GetDoubleProperty(const std::string &propertyName, int nbValuesExpected)
{
    SpiceInt n{};
    auto values = new SpiceDouble[nbValuesExpected];
    SpiceBoolean found{false};

    gdpool_c(propertyName.c_str(), 0, nbValuesExpected, &n, values, &found);

    std::vector<double> res;

    res.reserve(n);
    for (int i = 0; i < n; i++)
    {
        res.push_back(values[i]);
    }

    delete[] values;
    return res;
}
