using WiFiStudio.Core.Models;

namespace WiFiStudio.Core.Serialization;

public sealed class ProjectHistory
{
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();

    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;

    public void Capture(ProjectModel project)
    {
        _undo.Push(ProjectJsonSerializer.Serialize(project));
        _redo.Clear();
    }

    public ProjectModel? Undo(ProjectModel current)
    {
        if (_undo.Count == 0)
        {
            return null;
        }

        _redo.Push(ProjectJsonSerializer.Serialize(current));
        return ProjectJsonSerializer.Deserialize(_undo.Pop());
    }

    public ProjectModel? Redo(ProjectModel current)
    {
        if (_redo.Count == 0)
        {
            return null;
        }

        _undo.Push(ProjectJsonSerializer.Serialize(current));
        return ProjectJsonSerializer.Deserialize(_redo.Pop());
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
