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
            .Convert(static args => ParseOwnerCompanyFlag(args.Row));
    }

    private static bool ParseOwnerCompanyFlag(CsvHelper.IReaderRow row)
    {
        if (!row.TryGetField("owner_commpany_flag", out string? rawValue)
            && !row.TryGetField("owner_company_flag", out rawValue))
        {
            return false;
        }

        var value = rawValue?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("y", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }
}
