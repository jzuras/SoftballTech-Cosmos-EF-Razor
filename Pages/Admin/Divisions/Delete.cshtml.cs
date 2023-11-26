using Microsoft.AspNetCore.Mvc;
using Sbt.Data;

namespace Sbt.Pages.Admin.Divisions;

public class DeleteModel : Sbt.Pages.Admin.AdminPageModel
{
    public DeleteModel(DemoContext context) : base(context)
    {
    }

    // Note - using base class version of OnGetAsync()

    public async Task<IActionResult> OnPostAsync()
    {
        // submit button should be disbled if true, but protect against other entries
        if (base.DisableSubmitButton == true)
        {
            return Page();
        }

        if (base._context == null)
        {
            return NotFound();
        }

        // overposting is not an issue for DivisionInfo class
        await base._context.SaveDivisionInfo(base.DivisionInfo, deleteDivision: true);

        return RedirectToPage("./Index", new { organization = base.DivisionInfo.Organization });
    }
}
