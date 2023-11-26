using Microsoft.AspNetCore.Mvc;
using Sbt.Data;

namespace Sbt.Pages.Admin.Divisions;

public class EditModel : Sbt.Pages.Admin.AdminPageModel
{
    public EditModel(DemoContext context) : base(context)
    {
    }

    // Note - using base class version of OnGetAsync()

    public async Task<IActionResult> OnPostAsync(string organization, string id)
    {
        // submit button should be disbled if true, but protect against other entries
        if (base.DisableSubmitButton == true)
        {
            return Page();
        }

        if (organization == null)
        {
            return Page();
        }

        base.Organization = base.DivisionInfo.Organization = organization;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // overposting is not an issue for DivisionInfo class
        base.DivisionInfo.Updated = base.GetEasternTime();
        await base._context.SaveDivisionInfo(base.DivisionInfo);

        return RedirectToPage("./Index", new { organization = organization });
    }
}
