using Spectre.Console;
using Spectre.Console.Rendering;

namespace PlayDisneyParksUnpacker;

public class TotalColumn : ProgressColumn
{
	/// <summary>
	/// Gets or sets the style for a non-complete task.
	/// </summary>
	public Style Style { get; set; } = Style.Plain;

	/// <summary>
	/// Gets or sets the style for a completed task.
	/// </summary>
	public Style CompletedStyle { get; set; } = new Style(foreground: Color.Green);

	/// <inheritdoc/>
	public override IRenderable Render(RenderContext context, ProgressTask task, TimeSpan deltaTime)
	{
		var style = (int)task.Percentage == 100 ? CompletedStyle : Style ?? Style.Plain;
		return new Text($"{task.Value:N0}/{task.MaxValue:N0}", style).RightAligned();
	}

	/// <inheritdoc/>
	public override int? GetColumnWidth(RenderContext context)
	{
		return null;
	}
}