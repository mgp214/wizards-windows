using System;
using System.Collections.Generic;
using Godot;
#pragma warning disable CS0649, IDE0044

public class Overseer : Node {

    [Export]
    // A list of names of Managers that aren't ready yet. Values are removed as they report ready. 
    List<string> unreadyManagerNames;
    List<Manager> readyManagers = new List<Manager>();
    public static Overseer Instance { get; private set; }

    public override void _EnterTree() {
        GD.Randomize();
        Instance = this;
    }

    /// <summary>
    /// Called from a Manager when it is ready.
    /// </summary>
    /// <param name="m">The implementation of Manager reporting ready.</param>
    /// <param name="managerName">The name of the Manager to be removed from unreadyManagerNames</param>
    public void ReportReady(Manager m) {
        var managerName = m.GetType().Name;
        if (unreadyManagerNames.Contains(managerName)) {
            unreadyManagerNames.Remove(managerName);
            readyManagers.Add(m);
        } else {
            throw new ArgumentException($"{managerName} isn't present in unready manager names: [{String.Join(",", unreadyManagerNames.ToArray())}]");
        }

        if (unreadyManagerNames.Count == 0) {
            GD.Print("All Managers reporting ready, invoking their OnAllReady methods.");
            foreach (var rm in readyManagers) {
                rm.OnAllReady();
            }
        }
    }
    public void Reset() {
        GD.Print("Overseer resetting...");
        for (var i = 0; i < readyManagers.Count;) {
            var m = readyManagers[i] as Node;
            var isDead = readyManagers[i].Reset();
            if (isDead) {
                var ps = readyManagers[i].GetPackedScene();
                GD.Print($"Deleting manager {m.Name}.");
                unreadyManagerNames.Add(m.Name);
                readyManagers.RemoveAt(i);
                if (ps != null) {
                    InstantiateManager(ps);
                    GD.Print($"Instantiating replacement {m.Name} in 100ms");
                }
                continue;
            }
            i++;
        }
    }

    async void InstantiateManager(PackedScene ps) {
        await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
        AddChild(ps.Instance());
    }
}
