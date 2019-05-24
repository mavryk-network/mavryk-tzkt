﻿using Microsoft.Extensions.Configuration;

namespace Tezzycat.Services
{
    public class ObserverConfig
    {
    }

    public static class ObserverConfigExt
    {
        public static ObserverConfig GetObserverConfig(this IConfiguration config)
        {
            return config.GetSection("Observer")?.Get<ObserverConfig>() ?? new ObserverConfig();
        }
    }
}
