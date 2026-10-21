using System.Collections.Generic;
using System.Linq;
using NAS.Core.Models;
using NAS.Core.Vehicles.Dtos;
using UnityEngine;

namespace NAS.Core.Vehicles
{
    internal static class VehicleDtoMapper
    {
        public static CarData ToCarData(VehicleDto dto)
        {
            var car = ScriptableObject.CreateInstance<CarData>();
            car.id = dto.id;
            car.carName = dto.name;
            car.year = dto.year;
            car.retailPrice = dto.basePrice;
            car.type = dto.bodyType;
            car.category = dto.powertrain;
            car.imageUrl = dto.imageUrl;
            car.tigrisModelKey = dto.tigrisModelKey;
            car.exteriorColors = Options(dto.exteriorColors);
            car.wheels = Options(dto.wheels);
            car.interiors = Options(dto.interiors);
            car.trimLevels = Options(dto.trimLevels);
            return car;
        }

        private static List<CarColorOption> Options(VehicleColorDto[] dtos) =>
            (dtos ?? System.Array.Empty<VehicleColorDto>())
                .Select(c => new CarColorOption { id = c.id, name = c.name, hexCode = c.hexCode, priceAdjustment = c.priceAdjustment })
                .ToList();
    }
}
