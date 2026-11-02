using System;

namespace Harbor;

internal enum TaskbarStage { Base, Hover, Activated }
internal static class TaskbarTransparency
{
    internal static double Opacity(Settings settings, TaskbarStage stage) => 1 - Math.Clamp(stage switch { TaskbarStage.Hover => settings.TaskbarHoverTransparency, TaskbarStage.Activated => settings.TaskbarActiveTransparency, _ => settings.TaskbarBaseTransparency }, .5, 99.5) / 100;
    internal static TaskbarStage Stage(bool inside, bool activated, bool typing) => typing ? TaskbarStage.Hover : activated ? TaskbarStage.Activated : inside ? TaskbarStage.Hover : TaskbarStage.Base;
}
