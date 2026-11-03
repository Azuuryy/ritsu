using osu.Framework.Graphics;

namespace osu.Game.Overlays.Changelog
{
    public partial class RitsuLocalChangelogContent : ChangelogContent
    {
        public RitsuLocalChangelogContent(string version, string notes)
        {
            Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING, Vertical = 20 };
            Add(new ChangelogMarkdownContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Text = $"# Ritsu {version}\n\n{notes}",
            });
        }
    }
}
