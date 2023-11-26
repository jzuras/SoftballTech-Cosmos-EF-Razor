using Microsoft.AspNetCore.Mvc;
using Sbt.Data;

namespace Sbt.Pages.Admin.Divisions;

public class IndexModel : Sbt.Pages.Admin.AdminPageModel
{
    public IndexModel(DemoContext context) : base(context)
    {
    }

    public IList<Sbt.Models.DivisionInfo> DivisionsList { get; set; } = default!;

    override public async Task<IActionResult> OnGetAsync(string organization, string id = "")
    {
        await base.OnGetAsync(organization, id);

        this.DivisionsList = await this._context.GetDivisionList(organization);

        return Page();
    }
}
