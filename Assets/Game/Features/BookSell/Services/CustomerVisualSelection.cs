using System;
using System.Collections.Generic;
using Game.Configs;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    public interface ICustomerVisualSelector
    {
        string SelectNpcVisualId(ISalesRandom random);
    }

    public sealed class CustomerVisualSelector : ICustomerVisualSelector
    {
        private readonly IConfigsService _configs;

        public CustomerVisualSelector(IConfigsService configs)
            => _configs = configs ?? throw new ArgumentNullException(nameof(configs));

        public string SelectNpcVisualId(ISalesRandom random)
        {
            var configs = _configs.GetAll<CustomerVisualConfig>();
            if (configs == null || configs.Count == 0)
                return null;

            var active = new List<CustomerVisualConfig>(configs.Count);
            var totalWeight = 0f;
            for (var i = 0; i < configs.Count; i++)
            {
                var config = configs[i];
                if (config == null || string.IsNullOrWhiteSpace(config.Id) || config.Weight <= 0f)
                    continue;

                active.Add(config);
                totalWeight += config.Weight;
            }

            if (active.Count == 0 || totalWeight <= 0f)
                return null;

            var roll = (float)((random?.NextDouble() ?? 0d) * totalWeight);
            var cursor = 0f;
            for (var i = 0; i < active.Count; i++)
            {
                cursor += active[i].Weight;
                if (roll < cursor)
                    return active[i].Id;
            }

            return active[active.Count - 1].Id;
        }
    }
}
