#include "DataPoolMonitoring.h"

IO::SDK::DataPoolMonitoring IO::SDK::DataPoolMonitoring::m_instance;

IO::SDK::DataPoolMonitoring& IO::SDK::DataPoolMonitoring::Instance()
{
	return m_instance;
}

std::vector<std::string> IO::SDK::DataPoolMonitoring::GetStringProperty(const std::string& propertyName, int nbValuesExpected) const
{
	SpiceInt n{};
	SpiceChar values[10][100];
	SpiceBoolean found{ false };

	gcpool_c(propertyName.c_str(), 0, nbValuesExpected, 100, &n, values, &found);

	std::vector<std::string> res;

	if (found)
	{
		for (size_t i = 0; i < n; i++)
		{
			res.push_back(values[i]);
		}
	}

	return res;
}

std::vector<int> IO::SDK::DataPoolMonitoring::GetIntegerProperty(const std::string& propertyName, int nbValuesExpected) const
{
	SpiceInt n{};
	SpiceInt* values = new SpiceInt[nbValuesExpected];
	SpiceBoolean found{ false };

	gipool_c(propertyName.c_str(), 0, nbValuesExpected, &n, values, &found);

	std::vector<int> res;

	for (size_t i = 0; i < n; i++)
	{
		res.push_back(values[i]);
	}

	delete[] values;
	return res;
}

std::vector<double> IO::SDK::DataPoolMonitoring::GetDoubleProperty(const std::string& propertyName, int nbValuesExpected) const
{
	SpiceInt n{};
	SpiceDouble* values = new SpiceDouble[nbValuesExpected];
	SpiceBoolean found{ false };

	gdpool_c(propertyName.c_str(), 0, nbValuesExpected, &n, values, &found);

	std::vector<double> res;

	for (size_t i = 0; i < n; i++)
	{
		res.push_back(values[i]);
	}

	delete[] values;
	return res;
}
