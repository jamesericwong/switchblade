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
        /// <param name="cancellationToken">
        /// Token for this trigger cycle: a newer trigger cancels it. Implementations must stop before touching any
        /// badge once it is cancelled — the superseding cycle owns those badges, and no work of a superseded cycle may
        /// hide or re-apply state to them.
        /// </param>
        void Animate(WindowItem item, int delayMs, int durationMs, double startingOffsetX, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pulses the badge of an item whose shortcut number changed while it was on screen.
        /// Implementations should apply a brief, subtle opacity fade so the renumber reads as intentional,
        /// and must not disturb any in-flight entry animation (option C).
        /// </summary>
        void PulseRenumber(WindowItem item);
    }
}
