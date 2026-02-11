using CsvHelper.Configuration;
using TerritoryBuilder.Core.Models;

namespace TerritoryBuilder.Core.Data;

public sealed class LightBoxCsvMap : ClassMap<LightBoxRecord>
{
    public LightBoxCsvMap()
    {
        Map(m => m.EntityCategoryId).Name("entity_category_id");
        Map(m => m.Name).Name("name");
        Map(m => m.Latitude).Name("latitude");
        Map(m => m.Longitude).Name("longitude");
        Map(m => m.Address).Name("address");
        Map(m => m.City).Name("city");
        Map(m => m.County).Name("county");
        Map(m => m.State).Name("state");
        Map(m => m.Zip).Name("zip");
        Map(m => m.BuildingType).Name("building_type");
        Map(m => m.NumberOfAddresses).Name("number_of_addresses").Optional();
        Map(m => m.OwnerCompanyFlag).Name("owner_commpany_flag").Optional();
    }
}
