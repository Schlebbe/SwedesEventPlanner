using SwedesEventPlanner.Domain.Common;
using SwedesEventPlanner.Domain.Events;

namespace SwedesEventPlanner.Domain.Tests.Events;

public sealed class EventSignupDefaultsTests
{
    [Fact]
    public void Signups_default_to_imported_google_forms_rows()
    {
        var signup = new EventSignup
        {
            RuneScapeName = "Zezima"
        };

        Assert.Equal(EventSignupStatuses.Imported, signup.Status);
        Assert.Equal(EventSignupSources.GoogleForms, signup.SourceSystem);
        Assert.Equal(JsonDefaults.Object, signup.MetadataJson);
    }
}
