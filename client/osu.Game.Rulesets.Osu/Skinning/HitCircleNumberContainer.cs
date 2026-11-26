using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;

namespace osu.Game.Rulesets.Osu.Skinning
{
    internal partial class HitCircleNumberContainer : Container
    {
        private Bindable<bool> showNumbers = null!;

        public HitCircleNumberContainer(Drawable number)
        {
            Name = "Hit circle number";
            Anchor = Origin = Anchor.Centre;
            Child = number;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            showNumbers = config.GetBindable<bool>(OsuSetting.ShowHitCircleNumbers);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            showNumbers.BindValueChanged(value => Alpha = value.NewValue ? 1 : 0, true);
        }
    }
}
