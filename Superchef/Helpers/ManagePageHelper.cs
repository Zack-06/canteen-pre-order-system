using X.PagedList.Mvc.Core;

namespace Superchef.Helpers;

public class ManagePageHelper
{
    public static PagedListRenderOptions GetPagedOption()
    {
        return new PagedListRenderOptions
        {
            DisplayLinkToFirstPage = PagedListDisplayMode.Always,
            DisplayLinkToLastPage = PagedListDisplayMode.Always,
            DisplayLinkToPreviousPage = PagedListDisplayMode.Always,
            DisplayLinkToNextPage = PagedListDisplayMode.Always,
            MaximumPageNumbersToDisplay = 5,
            LinkToFirstPageFormat = "<div class='paging-symbol first'></div>",
            LinkToPreviousPageFormat = "<div class='paging-symbol prev'></div>",
            LinkToNextPageFormat = "<div class='paging-symbol next'></div>",
            LinkToLastPageFormat = "<div class='paging-symbol last'></div>"
        };
    }

    public static AjaxOptions GetAjaxOption()
    {
        return new AjaxOptions
        {
            HttpMethod = "GET",
            UpdateTargetId = "ajax-result-container"
        };
    }
}
