using CsvHelper.Configuration;
using TerritoryBuilder.Core.Models;

namespace TerritoryBuilder.Core.Data;

public sealed class LightBoxCsvMap : ClassMap<LightBoxRecord>
{
    public LightBoxCsvMap()
    {
        Map(m => m.EntityCategory).Name("entity_category").Optional();
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
        Map(m => m.OwnerCompanyFlag)
            .Name("owner_commpany_flag", "owner_company_flag")
            .Optional()
            .TypeConverterOption.BooleanValues(true, true, "Y", "Yes", "True", "1")
            .TypeConverterOption.BooleanValues(false, true, "N", "No", "False", "0");
    }
}
