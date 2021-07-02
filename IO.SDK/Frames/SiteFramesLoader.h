/**
 * @file SiteFramesLoader.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef SITE_FRAMES_H
#define SITE_FRAMES_H

namespace IO::SDK::Frames
{
	class SiteFramesLoader final
	{
	private:
		static SiteFramesLoader m_instance;
		SiteFramesLoader();
	};
}

#endif // !SITE_FRAMES_H
