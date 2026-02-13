using System.Globalization;
using CsvHelper;
using TerritoryBuilder.Core.Data;
using TerritoryBuilder.Core.Models;

namespace TerritoryBuilder.Tests.Unit;

public class DataIngestionTests
{
    [Fact]
    public void LightBoxCsvMap_AllowsUnexpectedOwnerCompanyFlagValues()
    {
        var csvText = string.Join(Environment.NewLine,
            "entity_category_id,name,latitude,longitude,address,city,county,state,zip,building_type,number_of_addresses,owner_commpany_flag",
            "business,id-1,1,2,123 Main,Grand Rapids,Kent,MI,49503,Unknown,1,2",
            "business,id-2,1,2,123 Main,Grand Rapids,Kent,MI,49503,Unknown,1,",
            "business,id-3,1,2,123 Main,Grand Rapids,Kent,MI,49503,Unknown,1,Yes",
            "business,id-4,1,2,123 Main,Grand Rapids,Kent,MI,49503,Unknown,1,0");

        using var reader = new StringReader(csvText);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap<LightBoxCsvMap>();

        var records = csv.GetRecords<LightBoxRecord>().ToList();

        Assert.Collection(records,
            first => Assert.False(first.OwnerCompanyFlag),
            second => Assert.False(second.OwnerCompanyFlag),
            third => Assert.True(third.OwnerCompanyFlag),
            fourth => Assert.False(fourth.OwnerCompanyFlag));
    }
}
