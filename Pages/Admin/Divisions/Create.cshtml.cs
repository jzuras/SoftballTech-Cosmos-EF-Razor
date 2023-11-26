using Microsoft.AspNetCore.Mvc;
using Sbt.Data;
using static Sbt.Data.DemoContext;

namespace Sbt.Pages.Admin.Divisions;
public class CreateModel : Sbt.Pages.Admin.AdminPageModel
{
    public CreateModel(DemoContext context) : base(context)
    {
    }

    // Note - using base class version of OnGetAsync()

    public async Task<IActionResult> OnPostAsync(string organization)
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

        var delMe = base.DivisionInfo;

        base.Organization = base.DivisionInfo.Organization = organization;

        if (!ModelState.IsValid || base._context == null)
        {
            return Page();
        }

        // overposting is not an issue for DivisionInfo class
        base.DivisionInfo.Updated = base.GetEasternTime();
        try
        {
            await this._context.SaveDivisionInfo(base.DivisionInfo, createDivision: true);
        }
        catch (DivisionExistsException)
        {
            ModelState.AddModelError(string.Empty, "This Division ID already exists.");
            return Page();
        }

        return RedirectToPage("./Index", new { organization = organization });
    }
}
