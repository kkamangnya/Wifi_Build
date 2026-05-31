using WiFiStudio.Core.Models;
using WiFiStudio.Core.Serialization;

namespace WiFiStudio.Tests.SerializationTests;

public sealed class ProjectHistoryTests
{
    [Fact]
    public void UndoRedo_Restores_ProjectSnapshots()
    {
        var project = ProjectFactory.CreateNewProject();
        var history = new ProjectHistory();
        history.Capture(project);

        project.FloorPlan.AccessPoints.Add(new AccessPoint { Name = "AP 1", Position = new PlanPoint(100, 100) });

        var undone = history.Undo(project);
        Assert.NotNull(undone);
        Assert.Empty(undone!.FloorPlan.AccessPoints);

        var redone = history.Redo(undone);
        Assert.NotNull(redone);
        Assert.Single(redone!.FloorPlan.AccessPoints);
    }
}
