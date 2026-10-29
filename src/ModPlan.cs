using System;
using System.Collections.Generic;
using System.Linq;

namespace Harbor;

internal sealed class ModPlan
{
    internal List<ModDefinition> Enable { get; } = new();
    internal List<ModDefinition> Conflicts { get; } = new();
    internal List<string> Errors { get; } = new();
    internal static bool Conflict(ModDefinition a, ModDefinition b) => a.Id != b.Id &&
        (a.Conflicts.Contains(b.Id, StringComparer.OrdinalIgnoreCase) || b.Conflicts.Contains(a.Id, StringComparer.OrdinalIgnoreCase) ||
         (a.Kind == "tweak" && b.Kind == "tweak" && a.Component == b.Component && !Tweaks.WindowComponents.Contains(a.Component)));
    internal static ModPlan Build(Mods mods, IEnumerable<ModDefinition> requested, IEnumerable<ModDefinition> existing)
    {
        var plan = new ModPlan(); var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in requested) Visit(mod);
        foreach (var mod in plan.Enable)
        {
            foreach (var other in plan.Enable.Where(other => Conflict(mod, other)))
                if (string.Compare(mod.Id, other.Id, StringComparison.OrdinalIgnoreCase) < 0) plan.Errors.Add(mod.Name + " conflicts with " + other.Name + ".");
            foreach (var other in existing.Where(other => Conflict(mod, other))) if (plan.Conflicts.All(c => c.Id != other.Id)) plan.Conflicts.Add(other);
        }
        return plan;
        void Visit(ModDefinition mod)
        {
            if (done.Contains(mod.Id)) return;
            if (!visiting.Add(mod.Id)) { plan.Errors.Add("Dependency cycle involving " + mod.Name + "."); return; }
            if (System.Version.Parse(mod.MinimumHarborVersion) > System.Version.Parse(Program.Version)) plan.Errors.Add(mod.Name + " requires Harbor " + mod.MinimumHarborVersion + " or newer.");
            foreach (var dependency in mod.Dependencies)
            {
                var found = mods.Catalog.FirstOrDefault(m => m.Id.Equals(dependency.Id, StringComparison.OrdinalIgnoreCase));
                if (found == null) plan.Errors.Add("Missing dependency: " + dependency.Id + ".");
                else if (System.Version.Parse(found.Version) < System.Version.Parse(dependency.MinimumVersion)) plan.Errors.Add(found.Name + " must be updated to " + dependency.MinimumVersion + " or newer.");
                else Visit(found);
            }
            visiting.Remove(mod.Id); done.Add(mod.Id); plan.Enable.Add(mod);
        }
    }
}
