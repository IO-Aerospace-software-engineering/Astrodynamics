/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef KERNEL_H
#define KERNEL_H
#include <Window.h>

namespace IO::Astrodynamics::Kernels
{
	class Kernel
	{
	protected:
		const std::string m_filePath;
		std::string m_comments;
		bool m_isLoaded{false};
		bool m_fileExists{false};
		explicit Kernel(std::string fileName);


	public:
		virtual ~Kernel();

		/**
		 * @brief Get the Path
		 * 
		 * @return std::string 
		 */
		[[nodiscard]] std::string GetPath() const;

		/**
		 * @brief 
		 * 
		 * @return true 
		 * @return false 
		 */
		[[nodiscard]] bool IsLoaded() const;

		/**
		 * @brief Get the Coverage Window
		 * 
		 * @return IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>
		 */
		[[nodiscard]] virtual IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> GetCoverageWindow() const = 0;

		/**
		 * @brief Add comment to kernel
		 * 
		 * @param comment 
		 */
		virtual void AddComment(const std::string &comment) const;

		/**
		 * @brief Read comment from kernel
		 * 
		 * @return std::string 
		 */
		[[nodiscard]] virtual std::string ReadComment() const;

        /**
		 * @brief Define the best Lagrange polynomial degree
		 *
		 * @param dataSize - Size of data set
		 * @return int
		 */
        static int DefinePolynomialDegree(int dataSize, int maximumDegree) ;
	};

}
#endif