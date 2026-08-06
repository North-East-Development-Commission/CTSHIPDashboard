using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Text;
using System.Linq;
using System.Text.Encodings.Web;
using CTSHIPDashboard.Helpers;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CTSHIPDashboard.TagHelpers
{
    [HtmlTargetElement("presenting-complaints", Attributes = "asp-for")]
    public class PresentingComplaintsTagHelper : TagHelper
    {
        [HtmlAttributeName("asp-for")]
        public ModelExpression For { get; set; } = default!;

        [HtmlAttributeName("asp-other-for")]
        public ModelExpression? OtherFor { get; set; }

        [ViewContext]
        public ViewContext ViewContext { get; set; } = default!;

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "presenting-complaints-control");

            var selected = For.Model as IEnumerable<string> ?? Enumerable.Empty<string>();
            var sb = new StringBuilder();

            // Render select multiple using Select2 friendly classes
            var selectId = For.Name.Replace('.', '_');
            sb.AppendLine($"<select id=\"{selectId}\" name=\"{For.Name}\" multiple class=\"form-select select2\" data-placeholder=\"Select presenting complaints...\">\n");

            foreach (var item in EncounterPresentingComplaintsCatalog.All)
            {
                var isSelected = selected.Any(s => string.Equals(s, item, StringComparison.OrdinalIgnoreCase));
                sb.AppendLine($"  <option value=\"{HtmlEncoder.Default.Encode(item)}\" {(isSelected ? "selected" : "")}> {HtmlEncoder.Default.Encode(item)} </option>");
            }

            sb.AppendLine("</select>");

            // Render Other input
            if (OtherFor != null)
            {
                var otherId = OtherFor.Name.Replace('.', '_');
                var otherValue = OtherFor.Model?.ToString() ?? string.Empty;
                sb.AppendLine($"<div id=\"{otherId}_row\" style=\"display:none;margin-top:.5rem;\">\n");
                sb.AppendLine($"  <input type=\"text\" id=\"{otherId}\" name=\"{OtherFor.Name}\" class=\"form-control\" placeholder=\"If Other, specify...\" value=\"{HtmlEncoder.Default.Encode(otherValue)}\" />\n");
                sb.AppendLine("</div>");
            }

            output.Content.SetHtmlContent(sb.ToString());
        }
    }
}
