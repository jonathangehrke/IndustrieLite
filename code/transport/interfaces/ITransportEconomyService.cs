// SPDX-License-Identifier: MIT
namespace IndustrieLite.Transport.Interfaces
{
    public interface ITransportEconomyService
    {
        double GetCurrentMarketPrice(string product, City city);

        void ProcessTruckArrival(Truck truck);
    }
}
