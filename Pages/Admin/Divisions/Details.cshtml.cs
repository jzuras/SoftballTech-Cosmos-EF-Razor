
using Sbt.Data;

namespace Sbt.Pages.Admin.Divisions;

public class DetailsModel : Sbt.Pages.Admin.AdminPageModel
{
    public DetailsModel(DemoContext context) : base(context)
    {
    }

    // Note - using base class version of OnGetAsync()

    // Note - no need for OnPostAsync() for details page
}
