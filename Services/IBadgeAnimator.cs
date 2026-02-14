using System;
using System.Threading.Tasks;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Strategy interface for animating badge appearances.
    /// Allows decoupling the animation logic (Storyboard vs Layout-based vs Mock) from the orchestration service.
    /// </summary>
    public interface IBadgeAnimator
    {
        /// <summary>
        /// Executes the badge animation for a specific item.
        /// </summary>
        /// <param name="item">The item to animate.</param>
        /// <param name="delayMs">Delay before starting the animation in milliseconds.</param>
        /// <param name="durationMs">Duration of the animation in milliseconds.</param>
        /// <param name="startingOffsetX">The initial X offset to slide in from (e.g. -20).</param>
        void Animate(WindowItem item, int delayMs, int durationMs, double startingOffsetX);
    }
}
