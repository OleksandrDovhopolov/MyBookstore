using Game.Configs.Models;

namespace Book.Sell.Services
{
    public interface IDemandGenreWeightProvider
    {
        double GetWeight(string genre, LocationConfig location);
    }
}
