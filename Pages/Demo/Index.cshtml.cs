using Microsoft.AspNetCore.Mvc.RazorPages;
using Sbt.Data;
using Sbt.Models;

namespace Sbt.Pages.Demo;

public class IndexModel : PageModel
{
    private readonly DemoContext _context = default!;

    public string Organization { get; private set; } = string.Empty;

    public IList<DivisionInfo> DivisionsList { get; set; } = default!;

    public IndexModel(DemoContext context)
    {
        this._context = context;
    }

    public async Task OnGetAsync(string organization)
    {
        if (organization == null)
        {
            this.Organization = "[Missing Organization]";
        }
        else
        {
            this.Organization = organization;
            this.DivisionsList = await this._context.GetDivisionList(organization);
        }
    }
}
