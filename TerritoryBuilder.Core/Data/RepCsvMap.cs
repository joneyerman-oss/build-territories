using CsvHelper.Configuration;
using TerritoryBuilder.Core.Models;

namespace TerritoryBuilder.Core.Data;

public sealed class RepCsvMap : ClassMap<RepRecord>
{
    public RepCsvMap()
    {
        Map(m => m.RepId).Name("rep_id");
        Map(m => m.RepName).Name("rep_name");
        Map(m => m.HomeLat).Name("home_lat");
        Map(m => m.HomeLon).Name("home_lon");
        Map(m => m.Active).Name("active");
    }
}
