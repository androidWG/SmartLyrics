using Android.Views.Animations;

namespace SmartLyrics.Animation
{
    internal class Animations
    {
        public static Android.Views.Animations.Animation BlinkingAnimation(int duration, int repeatCount)
        {
            Android.Views.Animations.Animation anim = new AlphaAnimation(0.2f, 1.0f)
            {
                Duration = duration, //You can manage the blinking time with this parameter
                RepeatMode = RepeatMode.Reverse,
                RepeatCount = repeatCount
            };

            return anim;
        }

        public static Android.Views.Animations.Animation BlinkingImageAnimation(int duration, int repeatCount)
        {
            Android.Views.Animations.Animation anim = new AlphaAnimation(1f, 0.2f)
            {
                Duration = duration, //You can manage the blinking time with this parameter
                RepeatMode = RepeatMode.Reverse,
                RepeatCount = repeatCount,
                FillAfter = true
            };

            return anim;
        }
    }
}